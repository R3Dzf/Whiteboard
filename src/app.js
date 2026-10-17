/* Bosha Whiteboard — no cloud account, tracking, CDN or runtime dependency. */
(() => {
'use strict';
const M = window.WhiteboardModel;
const $ = id => document.getElementById(id);
const svgNS = 'http://www.w3.org/2000/svg';
const el = (tag, attrs = {}, text) => {
  const node = document.createElementNS(svgNS, tag);
  Object.entries(attrs).forEach(([k,v]) => { if (v !== null && v !== undefined) node.setAttribute(k, String(v)); });
  if (text !== undefined) node.textContent = String(text);
  return node;
};
const colors = ['#252b3b','#6366f1','#3b82f6','#06a6a6','#34a365','#efbd3b','#f18b32','#ed5c82','#a276e8','#ffffff'];
const noteColors = ['#fff0a9','#ffcdd9','#c7e6ff','#d5f4dc','#e6d7ff','#ffddbe'];
const finite = n => Number.isFinite(n) ? n : 0;
const safeColor = (v, fallback = '#414d63') => /^#[0-9a-fA-F]{3,8}$/.test(v || '') ? v : fallback;
const esc = M.escapeXml;
const fmtDate = t => new Date(t).toLocaleDateString(undefined, {month:'short',day:'numeric',year:'numeric'});
const clamp = (n,a,b) => Math.min(b,Math.max(a,n));
let db, active=null, documents = new Map(), tool='select', ink='#252b3b', width=3;
let selected = new Set(), gesture=null, undoStack=[], redoStack=[], selectionSaveTimer=null, saveTimer=null, clipboard=[];
let spaceHeld=false, dialogDismiss=null, toastTimer=null, initialized=false;
let pointerContacts = new Map();
let pinch=null;
let snapEnabled=false, rulerPos={x:0,y:0,deg:0}, rulerDrag=null;
const templates = [
  ['brainstorm','☀','Brainstorm','Explore ideas around a central theme'],
  ['kanban','▤','Kanban board','To do · In progress · Done'],
  ['swot','⊞','SWOT analysis','Strengths · Weaknesses · Opportunities · Threats'],
  ['mindmap','◇','Mind map','Build branches from a central idea'],
  ['retro','◷','Retrospective','Went well · Improve · Actions'],
  ['lesson','✦','Lesson plan','Objective · Explain · Practice · Review']
];
function toast(message) { const n=$('toast'); n.textContent=message; n.hidden=false; clearTimeout(toastTimer); toastTimer=setTimeout(()=>n.hidden=true,2900); }
function setStatus(message) { $('saveStatus').textContent=message; }
const boardStore = {
  async init() {
    return new Promise(resolve => {
      if (!window.indexedDB) { toast('IndexedDB unavailable — export your work before closing'); resolve(); return; }
      const req=indexedDB.open('bosha-whiteboard-v1',1);
      req.onupgradeneeded=()=>{ if (!req.result.objectStoreNames.contains('boards')) req.result.createObjectStore('boards',{keyPath:'id'}); };
      req.onsuccess=()=>{db=req.result;resolve();};
      req.onerror=()=>{toast('Local storage unavailable — export your work');resolve();};
    });
  },
  all() { return new Promise(resolve=>{ if(!db)return resolve([]); const tx=db.transaction('boards','readonly'); const r=tx.objectStore('boards').getAll();r.onsuccess=()=>resolve(r.result||[]);r.onerror=()=>resolve([]);}); },
  put(board) { return new Promise((resolve,reject)=>{if(!db)return reject(new Error('Offline storage unavailable'));const tx=db.transaction('boards','readwrite');tx.objectStore('boards').put(M.clone(board));tx.oncomplete=resolve;tx.onerror=()=>reject(tx.error);}); },
  remove(id) { return new Promise((resolve,reject)=>{if(!db)return reject(new Error('Offline storage unavailable'));const tx=db.transaction('boards','readwrite');tx.objectStore('boards').delete(id);tx.oncomplete=resolve;tx.onerror=()=>reject(tx.error);}); }
};
function scheduleSave() {
  if(!active)return;
  active.updatedAt=Date.now(); documents.set(active.id,active);
  setStatus('◌ Saving…');clearTimeout(saveTimer);
  saveTimer=setTimeout(async()=>{
    try { await boardStore.put(active); setStatus('● Saved locally'); }
    catch(e){setStatus('⚠ Not saved'); toast('Save failed. Export a backup!'); console.error(e);}
  },480);
}
async function flushSave(){clearTimeout(saveTimer);if(active){try{await boardStore.put(active);setStatus('● Saved locally');}catch(e){toast('Unable to save. Export backup.');}}}
function snapshot() {
  if(!active)return;
  undoStack.push(JSON.stringify(active.objects));if(undoStack.length>80)undoStack.shift();redoStack=[];updateHistoryButtons();
}
function undo() {if(!active||!undoStack.length)return;redoStack.push(JSON.stringify(active.objects));active.objects=JSON.parse(undoStack.pop());selected.clear();render();scheduleSave();updateHistoryButtons();}
function redo() {if(!active||!redoStack.length)return;undoStack.push(JSON.stringify(active.objects));active.objects=JSON.parse(redoStack.pop());selected.clear();render();scheduleSave();updateHistoryButtons();}
function updateHistoryButtons(){$('undoBtn').disabled=!undoStack.length;$('redoBtn').disabled=!redoStack.length;}
function getView(){return active?.view || {x:0,y:0,zoom:1};}
function screenPoint(e){const r=$('canvas').getBoundingClientRect();return {x:e.clientX-r.left,y:e.clientY-r.top};}
function worldPoint(e){const s=screenPoint(e),v=getView();return {x:(s.x-v.x)/v.zoom,y:(s.y-v.y)/v.zoom};}
function svgLine(points,attrs){return el('polyline',{points:points.map(p=>p.join(',')).join(' '),fill:'none','stroke-linejoin':'round','stroke-linecap':'round',...attrs});}
function addText(parent,obj,x,y,w,fontSize,fill,bold=false){
  const t=el('text',{x,y,'font-family':'Segoe UI, Arial, sans-serif','font-size':fontSize,'font-weight':bold?700:500,fill:fill||'#243047','dominant-baseline':'hanging','xml:space':'preserve'});
  const rows=M.textLines(obj.text||'',Math.max(4,Math.floor((w-4)/(fontSize*.56))));
  const max=Math.max(1,Math.floor(((obj.h||100)-14)/(fontSize*1.36)));
  rows.slice(0,max).forEach((row,i)=>t.appendChild(el('tspan',{x,dy:i?fontSize*1.36:0},row)));
  parent.appendChild(t);
}
function drawObject(obj,parent) {
  const group=el('g',{'data-object-id':obj.id});
  if(obj.rotation && obj.type!=='stroke') group.setAttribute('transform',`rotate(${obj.rotation} ${obj.x+obj.w/2} ${obj.y+obj.h/2})`);
  const x=finite(obj.x),y=finite(obj.y),w=finite(obj.w),h=finite(obj.h);
  const s=safeColor(obj.color), f=safeColor(obj.fill,'#ffffff');
  const thick=clamp(Number(obj.thickness)||3,1,30);
  if(obj.type==='stroke') {
    const pts=obj.points||[];
    if(pts.length){
      if(pts.some(pt=>pt.length===3)&&pts.length>1){
        for(let i=1;i<pts.length;i++){
          const pressure=((pts[i-1][2]??.5)+(pts[i][2]??.5))/2;
          group.appendChild(el('line',{x1:pts[i-1][0],y1:pts[i-1][1],x2:pts[i][0],y2:pts[i][1],stroke:s,'stroke-width':(thick*(.55+.9*pressure)).toFixed(2),'stroke-linecap':'round',opacity:obj.highlight?.38:1}));
        }
      }else group.appendChild(svgLine(pts.length===1?[pts[0],[pts[0][0]+.1,pts[0][1]+.1]]:pts.map(p=>p.slice(0,2)),{stroke:s,'stroke-width':thick,opacity:obj.highlight?.38:1}));
    }
  } else if(['line','arrow','doubleArrow'].includes(obj.type)) {
    group.appendChild(el('line',{x1:x,y1:y,x2:x+w,y2:y+h,stroke:s,'stroke-width':thick,'stroke-linecap':'round'}));
    if(obj.type==='arrow'||obj.type==='doubleArrow') {
      const angle=Math.atan2(h,w), len=clamp(thick*3+9,12,27);
      group.appendChild(svgLine([[x+w-len*Math.cos(angle-.48),y+h-len*Math.sin(angle-.48)],[x+w,y+h],[x+w-len*Math.cos(angle+.48),y+h-len*Math.sin(angle+.48)]],{stroke:s,'stroke-width':thick}));
      if(obj.type==='doubleArrow')group.appendChild(svgLine([[x+len*Math.cos(angle-.48),y+len*Math.sin(angle-.48)],[x,y],[x+len*Math.cos(angle+.48),y+len*Math.sin(angle+.48)]],{stroke:s,'stroke-width':thick}));
    }
    group.appendChild(el('line',{x1:x,y1:y,x2:x+w,y2:y+h,stroke:'transparent','stroke-width':Math.max(16,thick)}));
  } else if(obj.type==='rect'){
    group.appendChild(el('rect',{x,y,width:Math.max(1,w),height:Math.max(1,h),rx:obj.rounded?18:9,fill:f,stroke:s,'stroke-width':thick}));
  } else if(obj.type==='ellipse'){
    group.appendChild(el('ellipse',{cx:x+w/2,cy:y+h/2,rx:Math.max(1,w/2),ry:Math.max(1,h/2),fill:f,stroke:s,'stroke-width':thick}));
  } else if(obj.type==='diamond'){
    group.appendChild(el('polygon',{points:`${x+w/2},${y} ${x+w},${y+h/2} ${x+w/2},${y+h} ${x},${y+h/2}`,fill:f,stroke:s,'stroke-width':thick,'stroke-linejoin':'round'}));
  } else if(obj.type==='sticky') {
    group.appendChild(el('rect',{x,y,width:Math.max(1,w),height:Math.max(1,h),rx:11,fill:safeColor(obj.fill,noteColors[0]),stroke:'#00000013','stroke-width':1}));
    group.appendChild(el('path',{d:`M ${x+w-31} ${y} L ${x+w-11} ${y} Q ${x+w} ${y} ${x+w} ${y+11} L ${x+w} ${y+31} Z`,fill:'#ffffff55'}));
    addText(group,obj,x+16,y+20,Math.max(40,w-33),clamp(obj.fontSize||18,10,58),'#253047');
  } else if(obj.type==='text') {
    group.appendChild(el('rect',{x,y,width:Math.max(20,w),height:Math.max(28,h),fill:'transparent'}));
    addText(group,obj,x+4,y+4,Math.max(24,w-8),clamp(obj.fontSize||23,10,90),s,obj.bold);
  } else if(obj.type==='emoji'){
    group.appendChild(el('rect',{x,y,width:Math.max(1,w),height:Math.max(1,h),fill:'transparent'}));
    group.appendChild(el('text',{x:x+w/2,y:y+h*.76,'font-size':Math.max(18,Math.min(w,h)*.8),'text-anchor':'middle'},obj.text||'👍'));
  } else if(obj.type==='image' && /^data:image\/(png|jpeg|webp|gif);base64,/.test(obj.src||'')) {
    group.appendChild(el('image',{x,y,width:Math.max(1,w),height:Math.max(1,h),href:obj.src,preserveAspectRatio:'xMidYMid meet'}));
  }
  parent.appendChild(group);
}
function renderGrid(){
  const v=getView(),host=$('canvasHost'); host.classList.toggle('dark',active?.theme==='dark');
  const dark=active?.theme==='dark', mode=active?.background||'dots';
  const col=dark?'#3c4354':'#e2e5ec';
  const size=Math.max(5,24*v.zoom);
  host.style.backgroundSize=`${size}px ${size}px`;
  host.style.backgroundPosition=`${v.x}px ${v.y}px`;
  host.style.backgroundColor=dark?'#1e2431':'#fcfcfe';
  host.style.backgroundImage= mode==='plain'?'none':mode==='dots'?`radial-gradient(${col} 1.1px,transparent 1.1px)`:mode==='lines'?`linear-gradient(to bottom,${col} 1px,transparent 1px)`: `linear-gradient(to right,${col} 1px,transparent 1px),linear-gradient(to bottom,${col} 1px,transparent 1px)`;
}
function render() {
  if(!active)return;
  const v=getView();$('world').setAttribute('transform',`translate(${v.x} ${v.y}) scale(${v.zoom})`);
  const items=$('items');items.replaceChildren();
  active.objects.forEach(o=>drawObject(o,items));
  const overlay=$('selectionOverlays');overlay.replaceChildren();
  for(const id of selected){
    const obj=active.objects.find(o=>o.id===id); if(!obj)continue;
    const r=M.rectFor(obj), gap=5/v.zoom;
    overlay.appendChild(el('rect',{x:r.x-gap,y:r.y-gap,width:r.w+gap*2,height:r.h+gap*2,fill:'none',stroke:'#6b68e8','stroke-width':1.6/v.zoom,'stroke-dasharray':`${5/v.zoom},${4/v.zoom}`,'pointer-events':'none'}));
    if(selected.size===1 && obj.type!=='stroke'){
      overlay.appendChild(el('line',{x1:r.x+r.w/2,y1:r.y-5/v.zoom,x2:r.x+r.w/2,y2:r.y-30/v.zoom,stroke:'#6764e3','stroke-width':1.5/v.zoom,'pointer-events':'none'}));
      overlay.appendChild(el('circle',{cx:r.x+r.w/2,cy:r.y-34/v.zoom,r:7/v.zoom,fill:'#ffffff',stroke:'#6764e3','stroke-width':2/v.zoom,'data-rotate-id':obj.id,style:'cursor:grab'}));
      overlay.appendChild(el('rect',{x:r.x+r.w-5/v.zoom,y:r.y+r.h-5/v.zoom,width:11/v.zoom,height:11/v.zoom,rx:2/v.zoom,fill:'#ffffff',stroke:'#6764e3','stroke-width':2/v.zoom,'data-resize-id':obj.id,style:'cursor:nwse-resize'}));
    }
  }
  renderGrid();
  $('emptyHint').hidden=active.objects.length>0;
  $('boardTitle').firstChild.nodeValue=`${active.name} `;
  $('zoomDisplay').textContent=`${Math.round(v.zoom*100)}%`;
  $('canvasHost').dataset.cursor=gesture?.kind==='pan'||tool==='hand'||spaceHeld?'pan':gesture?.kind==='move'?'move':['pen','highlighter','eraser','line','arrow','rect','ellipse','diamond','laser','doubleArrow','lasso'].includes(tool)?'draw':'default';
  updateInspector();
}
function setTool(next){tool=next;document.querySelectorAll('.tool[data-tool]').forEach(b=>b.classList.toggle('active',b.dataset.tool===tool));$('penBar').hidden=['select','hand','eraser','laser','sticky','text'].includes(tool);$('styleLabel').textContent=tool==='highlighter'?'Highlighter':'Ink';const hints={lasso:'Draw a loop around objects to select them · Shift to add',select:'Click to select · Drag to box-select · Shift+click to add',hand:'Drag to pan · Scroll to zoom',pen:'Draw with mouse, pen or touch · Hold Shift for straight lines',highlighter:'Highlight with semi-transparent ink',eraser:'Drag over ink strokes to erase',laser:'Temporary pointer; nothing is saved',sticky:'Click anywhere to place a note',text:'Click anywhere to place text'};$('toolHint').textContent=hints[tool]||'Drag on canvas to draw this shape';render();}
function renderColors(){const c=$('colors');c.replaceChildren();colors.forEach(color=>{const b=document.createElement('button');b.className='swatch'+(ink===color?' active':'');b.style.background=color;b.title=color;b.setAttribute('aria-label',`Color ${color}`);b.addEventListener('click',()=>{ink=color;renderColors();if(selected.size)updateStyle('color',ink);});c.appendChild(b);});}
function newObject(type,point,delta={x:0,y:0}){
  const defaults={id:M.uid(),type,x:point.x,y:point.y,w:delta.x||150,h:delta.y||100,color:ink,fill:'#ffffff',thickness:width};
  if(type==='sticky')return {...defaults,w:220,h:180,text:'Double-click to edit',fill:noteColors[Math.floor(Math.random()*noteColors.length)],fontSize:18};
  if(type==='text')return {...defaults,w:260,h:90,text:'Double-click to edit',fontSize:23};
  if(type==='emoji')return {...defaults,w:72,h:72,text:'👍'};
  if(type==='image')return {...defaults,w:240,h:160};
  if(type==='stroke')return {...defaults,w:1,h:1,points:[[point.x,point.y]],highlight:tool==='highlighter'};
  if(['line','arrow','doubleArrow'].includes(type))return {...defaults,w:delta.x||150,h:delta.y||70};
  return defaults;
}
function findHit(p){for(let i=active.objects.length-1;i>=0;i--){if(M.hit(active.objects[i],p.x,p.y,6/getView().zoom))return active.objects[i];}return null;}
function pointAt(pos,e){return Math.hypot(pos.x-e.x,pos.y-e.y);}
function pointerDown(e) {
  if(!active||e.button===2)return;
  $('contextMenu').hidden=true;
  if(e.button!==0&&e.button!==1)return;
  if(e.pointerType==='touch'){
    pointerContacts.set(e.pointerId,screenPoint(e));
    if(pointerContacts.size===2){
      const coords=[...pointerContacts.values()];pinch={a:coords,view:M.clone(active.view)};gesture=null;
      $('canvas').setPointerCapture(e.pointerId);return;
    }
  }
  $('canvas').setPointerCapture(e.pointerId);
  const s=screenPoint(e),p=worldPoint(e);
  if(e.button===1||tool==='hand'||spaceHeld){gesture={kind:'pan',start:s,view:M.clone(active.view)};render();return;}
  if(tool==='laser'){gesture={kind:'laser'};showLaser(s);return;}
  if(tool==='lasso') {if(!e.shiftKey)selected.clear();gesture={kind:'lasso',points:[[p.x,p.y]]};render();return;}
  if(tool==='select') {
    const rotate=e.target?.closest?.('[data-rotate-id]');
    if(rotate){const obj=active.objects.find(o=>o.id===rotate.getAttribute('data-rotate-id'));
      if(obj){snapshot();const cx=obj.x+obj.w/2,cy=obj.y+obj.h/2;gesture={kind:'rotate',id:obj.id,startAngle:Math.atan2(p.y-cy,p.x-cx),oldAngle:obj.rotation||0,cx,cy};return;}}
    const handle=e.target?.closest?.('[data-resize-id]');
    if(handle){const obj=active.objects.find(o=>o.id===handle.getAttribute('data-resize-id'));if(obj){snapshot();gesture={kind:'resize',id:obj.id,start:p,old:M.clone(obj),rect:M.rectFor(obj)};return;}}
    const hit=findHit(p);
    if(hit){
      const groupIds=M.expandGrouped(active.objects,new Set([hit.id]));
      if(e.shiftKey){if([...groupIds].every(id=>selected.has(id))) groupIds.forEach(id=>selected.delete(id));else groupIds.forEach(id=>selected.add(id));}
      else if(!selected.has(hit.id)){selected=groupIds;}
      gesture={kind:'move',start:p,ids:[...selected],old:active.objects.filter(o=>selected.has(o.id)).map(M.clone),changed:false};
    }else{if(!e.shiftKey)selected.clear();gesture={kind:'marquee',start:p,end:p};}
    render();return;
  }
  if(tool==='eraser'){gesture={kind:'erase',changed:false};eraseAt(p);return;}
  if(['pen','highlighter'].includes(tool)){snapshot();const obj=newObject('stroke',p);if(tool==='highlighter')obj.thickness=Math.max(width*2,12);obj.points[0].push(e.pointerType==='pen'&&e.pressure>0?e.pressure:.5);active.objects.push(obj);gesture={kind:'ink',id:obj.id,start:p};render();return;}
  if(['text','sticky'].includes(tool)){
    snapshot();const obj=newObject(tool,p);active.objects.push(obj);selected=new Set([obj.id]);scheduleSave();render();editObject(obj);setTool('select');return;
  }
  if(['rect','ellipse','diamond','line','arrow','doubleArrow'].includes(tool)){
    snapshot();const obj=newObject(tool,p,{x:1,y:1});active.objects.push(obj);gesture={kind:'shape',id:obj.id,start:p};render();return;
  }
}
function eraseAt(p){
  let changed=false;const radius=Math.max(5,width*2)/getView().zoom;
  const updated=[];
  for(const o of active.objects){
    if(o.type!=='stroke'||!M.hit(o,p.x,p.y,radius)) {updated.push(o);continue;}
    const parts=M.eraseStroke(o,p,radius+((o.thickness||3)/2));
    if(parts===null)updated.push(o);
    else {if(!gesture.changed){snapshot();gesture.changed=true;}updated.push(...parts);changed=true;}
  }
  if(changed){active.objects=updated;render();}
}
function showLaser(p){const layer=$('laserLayer');layer.replaceChildren();layer.appendChild(el('circle',{cx:p.x,cy:p.y,r:14,fill:'#f83b6a33',stroke:'#f83b6a','stroke-width':3}));}
function pointerMove(e) {
  if(e.pointerType==='touch'&&pointerContacts.has(e.pointerId)){
    pointerContacts.set(e.pointerId,screenPoint(e));
    if(pinch&&pointerContacts.size>=2){
      const points=[...pointerContacts.values()].slice(0,2),a=pinch.a;
      const dist=(p)=>Math.hypot(p[0].x-p[1].x,p[0].y-p[1].y);
      const start={x:(a[0].x+a[1].x)/2,y:(a[0].y+a[1].y)/2},now={x:(points[0].x+points[1].x)/2,y:(points[0].y+points[1].y)/2};
      const z=clamp(pinch.view.zoom*dist(points)/Math.max(1,dist(a)),.15,5);
      active.view.zoom=z;
      active.view.x=now.x-(start.x-pinch.view.x)*(z/pinch.view.zoom);
      active.view.y=now.y-(start.y-pinch.view.y)*(z/pinch.view.zoom);
      render();return;
    }
  }
  if(!gesture||!active)return;
  const p=worldPoint(e),s=screenPoint(e);
  if(gesture.kind==='pan'){active.view.x=gesture.view.x+s.x-gesture.start.x;active.view.y=gesture.view.y+s.y-gesture.start.y;}
  if(gesture.kind==='laser'){showLaser(s);return;}
  if(gesture.kind==='lasso') {
    const arr=gesture.points,last=arr[arr.length-1];if(Math.hypot(last[0]-p.x,last[1]-p.y)>2/getView().zoom)arr.push([p.x,p.y]);
  }
  if(gesture.kind==='rotate'){
    const g=gesture,obj=active.objects.find(o=>o.id===g.id);
    if(obj){const a=Math.atan2(p.y-g.cy,p.x-g.cx);let deg=g.oldAngle+(a-g.startAngle)*180/Math.PI;
      if(e.shiftKey)deg=Math.round(deg/15)*15;obj.rotation=((deg%360)+360)%360;}
  }
  if(gesture.kind==='ink'){
    const obj=active.objects.find(o=>o.id===gesture.id);if(obj){if(e.shiftKey){obj.points=[obj.points[0],[p.x,p.y,e.pointerType==='pen'&&e.pressure>0 ? e.pressure : .5]];}else obj.points.push([p.x,p.y, e.pointerType==='pen'&&e.pressure>0 ? e.pressure : .5]);const r=M.rectFor(obj);Object.assign(obj,r);}
  }
  if(gesture.kind==='shape'){
    const obj=active.objects.find(o=>o.id===gesture.id);
    if(obj){obj.x=Math.min(gesture.start.x,p.x);obj.y=Math.min(gesture.start.y,p.y);obj.w=Math.abs(p.x-gesture.start.x);obj.h=Math.abs(p.y-gesture.start.y);
      if(['line','arrow','doubleArrow'].includes(obj.type)){obj.x=gesture.start.x;obj.y=gesture.start.y;obj.w=p.x-gesture.start.x;obj.h=p.y-gesture.start.y;}}
  }
  if(gesture.kind==='move') {
    let dx=p.x-gesture.start.x,dy=p.y-gesture.start.y;
    if(snapEnabled && !e.altKey){const a=M.snapPoint({x:gesture.old[0].x+dx,y:gesture.old[0].y+dy});dx+=a.x-(gesture.old[0].x+dx);dy+=a.y-(gesture.old[0].y+dy);}
    if(Math.abs(dx)+Math.abs(dy)>1/getView().zoom&&!gesture.changed){snapshot();gesture.changed=true;}
    gesture.old.forEach(old=>{const target=active.objects.find(o=>o.id===old.id);if(target){Object.assign(target,M.translate(M.clone(old),dx,dy));}});
  }
  if(gesture.kind==='resize') {
    const r=gesture.rect, sx=clamp((r.w+p.x-gesture.start.x)/Math.max(1,r.w),.15,20),sy=clamp((r.h+p.y-gesture.start.y)/Math.max(1,r.h),.15,20);
    const obj=active.objects.find(o=>o.id===gesture.id);if(obj)Object.assign(obj,M.resize(M.clone(gesture.old),sx,sy,r.x,r.y));
  }
  if(gesture.kind==='marquee')gesture.end=p;
  if(gesture.kind==='erase'){eraseAt(p);return;}
  render();
  if(gesture.kind==='lasso'){
    const a=gesture.points; $('preview').replaceChildren(el('polyline',{points:a.map(pt=>pt.join(',')).join(' '),fill:'none',stroke:'#6664e9','stroke-width':2/getView().zoom,'stroke-dasharray':`${6/getView().zoom},${3/getView().zoom}`,'pointer-events':'none'}));
  }
  if(gesture.kind==='marquee'){
    const g=$('preview'),r={x:Math.min(gesture.start.x,gesture.end.x),y:Math.min(gesture.start.y,gesture.end.y),w:Math.abs(gesture.end.x-gesture.start.x),h:Math.abs(gesture.end.y-gesture.start.y)};
    g.replaceChildren(el('rect',{...r,fill:'#7770e317',stroke:'#6a66df','stroke-width':1/getView().zoom,'stroke-dasharray':'5 3','pointer-events':'none'}));
  }
}
function pointerUp(e) {
  if(e.pointerType==='touch'){pointerContacts.delete(e.pointerId);if(pinch){if(pointerContacts.size<2){pinch=null;gesture=null;scheduleSave();}return;}}
  if(!gesture)return;
  const g=gesture;gesture=null;$('preview').replaceChildren();$('laserLayer').replaceChildren();
  if(g.kind==='lasso'&&g.points.length>=3){
    const ids=new Set(active.objects.filter(o=>M.lassoHits(o,g.points)).map(o=>o.id));
    M.expandGrouped(active.objects,ids).forEach(id=>selected.add(id));
  }
  if(g.kind==='marquee'){
    const a=g.start,b=g.end;const r={x:Math.min(a.x,b.x),y:Math.min(a.y,b.y),w:Math.abs(a.x-b.x),h:Math.abs(a.y-b.y)};
    if(r.w>3&&r.h>3){for(const o of active.objects){const b=M.rectFor(o);if(b.x>=r.x&&b.y>=r.y&&b.x+b.w<=r.x+r.w&&b.y+b.h<=r.y+r.h)selected.add(o.id);}}
  }
  if(g.kind==='shape'){
    const obj=active.objects.find(o=>o.id===g.id);
    if(obj&&Math.abs(obj.w)+Math.abs(obj.h)<9){obj.w=150;obj.h=obj.type==='line'||obj.type==='arrow'?70:100;}
    if(obj)selected=new Set([obj.id]);
  }
  if(g.kind==='ink'){
    const obj=active.objects.find(o=>o.id===g.id);if(obj){const r=M.rectFor(obj);Object.assign(obj,r);}
  }
  render();
  if(!['laser','marquee','lasso'].includes(g.kind))scheduleSave();
  if(g.kind==='pan')scheduleSave();
}
function wheel(e){if(!active)return;e.preventDefault();const s=screenPoint(e);const v=active.view;const prev=v.zoom,next=clamp(prev*Math.exp(-e.deltaY*.0015),.15,5);v.x=s.x-(s.x-v.x)*(next/prev);v.y=s.y-(s.y-v.y)*(next/prev);v.zoom=next;render();scheduleSave();}
function zoom(factor){if(!active)return;const canvas=$('canvas'),s={x:canvas.clientWidth/2,y:canvas.clientHeight/2},v=active.view;const next=clamp(v.zoom*factor,.15,5);v.x=s.x-(s.x-v.x)*next/v.zoom;v.y=s.y-(s.y-v.y)*next/v.zoom;v.zoom=next;render();scheduleSave();}
function fitObjects(){if(!active)return;const box=M.bounds(active.objects),svg=$('canvas');const z=clamp(Math.min((svg.clientWidth-120)/Math.max(400,box.w),(svg.clientHeight-120)/Math.max(320,box.h)),.15,3);active.view={zoom:z,x:(svg.clientWidth-box.w*z)/2-box.x*z,y:(svg.clientHeight-box.h*z)/2-box.y*z};render();scheduleSave();}
function changeSelection(ids){selected=new Set(ids);render();}
function updateInspector() {
  const panel=$('inspector'),objects=active?.objects.filter(o=>selected.has(o.id))||[];
  if(!objects.length){panel.hidden=true;return;}panel.hidden=false;
  const body=$('inspectorBody');
  if(objects.length>1){
    body.innerHTML=`<p class="tip">${objects.length} objects selected</p><div class="action-row"><button class="soft-btn" id="groupSel">Group</button><button class="soft-btn" id="ungroupSel">Ungroup</button></div><label>Align objects</label><div class="align-actions">${['left','center','right','top','middle','bottom'].map(v=>`<button class="soft-btn" data-align="${v}">${v}</button>`).join('')}</div><div class="action-row"><button class="soft-btn" id="duplicateSel">Duplicate</button><button class="soft-btn" id="deleteSel">Delete</button></div>`;
    $('groupSel').onclick=groupSelected;$('ungroupSel').onclick=ungroupSelected;
    document.querySelectorAll('[data-align]').forEach(b=>b.onclick=()=>alignSelected(b.dataset.align));
    $('duplicateSel').onclick=duplicateSelected;$('deleteSel').onclick=deleteSelected;return;}
  const o=objects[0],showText=['text','sticky','emoji'].includes(o.type);
  body.replaceChildren();
  const title=document.createElement('p');title.className='tip';title.textContent=`${o.type.toUpperCase()} · Double-click to edit`;body.appendChild(title);
  if(showText){const label=document.createElement('label');label.textContent='Content';const area=document.createElement('textarea');area.value=o.text||'';area.maxLength=10000;area.addEventListener('change',()=>updateStyle('text',area.value));label.appendChild(area);body.appendChild(label);}
  function makeControl(labelText,inp) {const label=document.createElement('label');label.textContent=labelText;label.appendChild(inp);body.appendChild(label);}
  if(o.type!=='image'){
    const col=document.createElement('input');col.type='color';col.value=safeColor(o.color);col.onchange=()=>updateStyle('color',col.value);makeControl('Stroke / text color',col);
    if(['sticky','rect','ellipse','diamond'].includes(o.type)){
      const fill=document.createElement('input');fill.type='color';fill.value=safeColor(o.fill,'#ffffff');fill.onchange=()=>updateStyle('fill',fill.value);makeControl('Fill color',fill);
    }
  }
  if(showText&&o.type!=='emoji'){
    const fs=document.createElement('input');fs.type='number';fs.min=10;fs.max=90;fs.value=o.fontSize||20;fs.onchange=()=>updateStyle('fontSize',clamp(Number(fs.value),10,90));makeControl('Font size',fs);
  }
  if(o.type!=='stroke'){
    const rot=document.createElement('input');rot.type='number';rot.min=0;rot.max=359;rot.step=1;rot.value=Math.round(o.rotation||0);
    rot.onchange=()=>updateStyle('rotation',((Number(rot.value)%360)+360)%360);makeControl('Rotation (degrees)',rot);
  }
  const row=document.createElement('div');row.className='action-row';
  [['Duplicate',duplicateSelected],['Delete',deleteSelected]].forEach(([name,fn])=>{const btn=document.createElement('button');btn.className='soft-btn';btn.textContent=name;btn.onclick=fn;row.appendChild(btn);});body.appendChild(row);
}
function updateStyle(key,value){if(!selected.size||!active)return;snapshot();active.objects.filter(o=>selected.has(o.id)).forEach(o=>o[key]=value);render();scheduleSave();}
function groupSelected(){if(selected.size<2)return;snapshot();M.groupObjects(active.objects,selected);selected=M.expandGrouped(active.objects,selected);render();scheduleSave();}
function ungroupSelected(){if(!selected.size)return;const groups=active.objects.some(o=>selected.has(o.id)&&o.groupId);if(!groups)return;snapshot();M.ungroupObjects(active.objects,selected);render();scheduleSave();}
function alignSelected(mode){if(selected.size<2)return;snapshot();M.alignObjects(active.objects,selected,mode);render();scheduleSave();}
function deleteSelected(){if(!selected.size)return;snapshot();active.objects=active.objects.filter(o=>!selected.has(o.id));selected.clear();render();scheduleSave();}
function duplicateSelected(){if(!selected.size)return;snapshot();const newGroups=new Map();const arr=active.objects.filter(o=>selected.has(o.id)).map(o=>{const n=M.clone(o);n.id=M.uid();if(n.groupId){if(!newGroups.has(n.groupId))newGroups.set(n.groupId,M.uid());n.groupId=newGroups.get(n.groupId);}return M.translate(n,24,24);});active.objects.push(...arr);changeSelection(arr.map(o=>o.id));scheduleSave();}
function layer(action){if(!selected.size)return;snapshot();const ids=selected;
  if(action==='front'||action==='back'){
    const arr=active.objects.filter(o=>ids.has(o.id));active.objects=active.objects.filter(o=>!ids.has(o.id));
    if(action==='front')active.objects.push(...arr);else active.objects.unshift(...arr);
  } else {
    const list=active.objects;if(action==='forward'){for(let i=list.length-2;i>=0;i--)if(ids.has(list[i].id)&&!ids.has(list[i+1].id))[list[i],list[i+1]]=[list[i+1],list[i]];}
    if(action==='backward'){for(let i=1;i<list.length;i++)if(ids.has(list[i].id)&&!ids.has(list[i-1].id))[list[i],list[i-1]]=[list[i-1],list[i]];}
  }render();scheduleSave();}
function editObject(o){if(!o||!['text','sticky','emoji'].includes(o.type))return;openDialog('Edit '+(o.type==='sticky'?'sticky note':o.type),`<label class="field">Text<textarea id="editText" maxlength="10000"></textarea></label>`,[{text:'Cancel'},{text:'Save changes',primary:true,click:()=>{snapshot();o.text=$('editText').value;render();scheduleSave();return true;}}]);$('editText').value=o.text||'';$('editText').focus();$('editText').select();}
function openDialog(title,html,actions=[]){$('dialogTitle').textContent=title;$('dialogBody').innerHTML=html;$('dialogActions').replaceChildren();actions.forEach(a=>{const b=document.createElement('button');b.textContent=a.text;b.className=a.primary?'primary-btn':'soft-btn';b.onclick=async()=>{if(a.click){try{const ok=await a.click();if(ok===false)return;}catch(err){toast(err.message||'Something went wrong');return;}}closeDialog();};$('dialogActions').appendChild(b);});$('dialogBackdrop').hidden=false;}
function closeDialog(){$('dialogBackdrop').hidden=true;dialogDismiss?.();dialogDismiss=null;}
function textPrompt(title,initial,callback){openDialog(title,'<label class="field">Name<input id="nameInput" maxlength="100"/></label>',[{text:'Cancel'},{text:'Save',primary:true,click:()=>{const name=$('nameInput').value.trim();if(!name)return false;callback(name);return true;}}]);$('nameInput').value=initial;setTimeout(()=>$('nameInput').focus(),50);}
async function createBoard(){await flushSave();active=M.emptyBoard('Untitled board');documents.set(active.id,active);selected.clear();undoStack=[];redoStack=[];await boardStore.put(active).catch(()=>toast('Storage unavailable: export a backup'));$('gallery').hidden=true;setTool('select');fitObjects();updateHistoryButtons();}
async function openBoard(id){await flushSave();active=documents.get(id);if(!active)return;selected.clear();undoStack=[];redoStack=[];$('gallery').hidden=true;setTool('select');render();updateHistoryButtons();}
async function gallery(){await flushSave();active=null;selected.clear();$('gallery').hidden=false;renderGallery();}
function renderGallery(){const list=[...documents.values()].sort((a,b)=>b.updatedAt-a.updatedAt),query=$('boardSearch').value.toLowerCase().trim();$('boardCount').textContent=list.length;const parent=$('boardCards');parent.replaceChildren();
  const filtered=list.filter(b=>b.name.toLowerCase().includes(query));
  if(!filtered.length){const p=document.createElement('p');p.style.color='#919aaf';p.textContent=list.length?'No boards match your search.':'No boards yet. Create your first whiteboard!';parent.appendChild(p);return;}
  filtered.forEach(board=>{const card=document.createElement('article');card.className='board-card';card.tabIndex=0;card.setAttribute('role','button');
    const thumb=document.createElement('div');thumb.className='board-thumb';thumb.dataset.theme=board.theme||'light';thumb.textContent=board.objects.length?'✎':'✳';
    const meta=document.createElement('div');meta.className='board-meta';const info=document.createElement('div');const name=document.createElement('strong');name.textContent=board.name;const date=document.createElement('small');date.textContent=`${fmtDate(board.updatedAt)} · ${board.objects.length} items`;info.append(name,date);
    const menu=document.createElement('button');menu.className='card-menu';menu.textContent='⋯';menu.title='Board options';menu.onclick=e=>{e.stopPropagation();boardOptions(board);};meta.append(info,menu);card.append(thumb,meta);card.onclick=()=>openBoard(board.id);card.onkeydown=e=>{if(e.key==='Enter')openBoard(board.id);};parent.appendChild(card);});
}
function boardOptions(board){openDialog(esc(board.name),'<div class="export-opts"><button id="renameBoard">Rename <span>Change the board name</span></button><button id="duplicateBoard">Duplicate <span>Make an independent copy</span></button><button id="deleteBoard">Delete <span>Remove from this device</span></button></div>');
  $('renameBoard').onclick=()=>{closeDialog();textPrompt('Rename whiteboard',board.name,name=>{board.name=name;board.updatedAt=Date.now();boardStore.put(board).catch(()=>toast('Save failed'));renderGallery();});};
  $('duplicateBoard').onclick=async()=>{const b=M.clone(board);b.id=M.uid();b.name=board.name+' (copy)';b.createdAt=b.updatedAt=Date.now();b.objects.forEach(o=>o.id=M.uid());documents.set(b.id,b);try{await boardStore.put(b);}catch(e){toast('Could not store duplicate');}closeDialog();renderGallery();};
  $('deleteBoard').onclick=()=>{closeDialog();openDialog('Delete whiteboard?',`<p>Delete <strong>${esc(board.name)}</strong> from this device? Export a backup first if you need it again.</p>`,[{text:'Cancel'},{text:'Delete board',primary:true,click:async()=>{try{await boardStore.remove(board.id);documents.delete(board.id);renderGallery();}catch(e){toast('Delete failed');}return true;}}]);};
}
function addTemplate(key){if(!active)return;const [centerX,centerY]=centerWorld();const x=centerX-330,y=centerY-220;const created=[];
  function box(xx,yy,w,h,txt,fill='#fff',fs=23){created.push({...newObject('rect',{x:xx,y:yy},{x:w,y:h}),fill,color:'#bec6d6',thickness:1});if(txt)created.push({...newObject('text',{x:xx+16,y:yy+15}),w:w-28,h:h-25,text:txt,fontSize:fs,color:'#2a3245',bold:true});}
  function note(xx,yy,txt,color){created.push({...newObject('sticky',{x:xx,y:yy}),w:180,h:125,text:txt,fill:color,fontSize:18});}
  if(key==='kanban'||key==='retro'){const heads=key==='kanban'?['To do','In progress','Done']:['Went well','To improve','Action items'];heads.forEach((head,i)=>{box(x+i*235,y,218,405,head,'#fafbff',23);note(x+16+i*235,y+75,'Add an idea…',noteColors[i]);});}
  if(key==='swot'){['Strengths','Weaknesses','Opportunities','Threats'].forEach((h,i)=>{const xx=x+(i%2)*340,yy=y+Math.floor(i/2)*220;box(xx,yy,322,200,h,['#f1fdfa','#fff8f0','#f3f4ff','#fff2f6'][i],24);note(xx+19,yy+60,'Add notes',noteColors[i]);});}
  if(key==='brainstorm'){box(x+175,y+140,330,120,'Your big idea','#efedff',30);['Idea 1','Idea 2','Idea 3','Idea 4'].forEach((txt,i)=>{const a=i*Math.PI/2;note(x+252+Math.cos(a)*250,y+151+Math.sin(a)*168,txt,noteColors[i]);});}
  if(key==='mindmap'){box(x+260,y+150,180,95,'Main topic','#e9e6ff');[['Branch A',10,10],['Branch B',480,10],['Branch C',10,315],['Branch D',480,315]].forEach(([t,dx,dy],i)=>{note(x+dx,y+dy,t,noteColors[i]);created.push({...newObject('line',{x:x+350,y:y+200},{x:x+dx+90-(x+350),y:y+dy+60-(y+200)}),color:'#8f92bb'});});}
  if(key==='lesson'){box(x,y,680,80,'Lesson plan','#efedff',31);[['Objectives',0,110],['Explain',350,110],['Practice',0,300],['Review',350,300]].forEach(([h,dx,dy],i)=>{box(x+dx,y+dy,330,165,h,['#f2f7ff','#f4fff6','#fffaf1','#fff5fa'][i],24);});}
  if(!created.length)return;snapshot();active.objects.push(...created);selected.clear();fitObjects();scheduleSave();toast('Template added. Everything is editable!');
}
function templatesDialog(){openDialog('Choose a template',`<div class="template-grid">${templates.map(t=>`<button class="template-card" data-template="${t[0]}"><span class="template-emoji">${t[1]}</span><strong>${t[2]}</strong><small>${t[3]}</small></button>`).join('')}</div>`);document.querySelectorAll('[data-template]').forEach(b=>b.onclick=()=>{const key=b.dataset.template;closeDialog();addTemplate(key);});}
function settingsDialog(){if(!active)return;openDialog('Canvas appearance',`<label class="field">Background style<select id="bgChoice"><option value="dots">Dotted</option><option value="grid">Square grid</option><option value="lines">Ruled lines</option><option value="plain">Plain</option></select></label><label class="field">Color theme<select id="themeChoice"><option value="light">Light canvas</option><option value="dark">Dark canvas</option></select></label><p>Customize the surface independently for each board.</p>`,[{text:'Cancel'},{text:'Apply',primary:true,click:()=>{active.background=$('bgChoice').value;active.theme=$('themeChoice').value;render();scheduleSave();}}]);$('bgChoice').value=active.background;$('themeChoice').value=active.theme;}
function centerWorld(){const canvas=$('canvas'),v=getView();return [(canvas.clientWidth/2-v.x)/v.zoom,(canvas.clientHeight/2-v.y)/v.zoom];}
function downloadBlob(blob,name){const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=name;document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),15000);}
const safeFilename=name=>String(name).replace(/[^\w\-. ]/g,'_').slice(0,75)||'whiteboard';
function exportJson(){if(!active)return;downloadBlob(new Blob([JSON.stringify({...active,format:'bosha-whiteboard',formatVersion:1},null,2)],{type:'application/json'}),safeFilename(active.name)+'.bosha.json');toast('Board backup exported');}
function exportSvg(){const {svg}=makeExport();downloadBlob(new Blob([new XMLSerializer().serializeToString(svg)],{type:'image/svg+xml'}),safeFilename(active.name)+'.svg');}
function makeExport(){const b=M.bounds(active.objects),pad=40,r={x:b.x-pad,y:b.y-pad,w:Math.max(640,b.w+pad*2),h:Math.max(440,b.h+pad*2)};
  const svg=el('svg',{xmlns:svgNS,width:Math.ceil(r.w),height:Math.ceil(r.h),viewBox:`${r.x} ${r.y} ${r.w} ${r.h}`});
  svg.appendChild(el('rect',{x:r.x,y:r.y,width:r.w,height:r.h,fill:active.theme==='dark'?'#1e2431':'#ffffff'}));
  if(active.background!=='plain'){
    const defs=el('defs'),pattern=el('pattern',{id:'export-grid',width:24,height:24,patternUnits:'userSpaceOnUse'});
    const color=active.theme==='dark'?'#45495a':'#e2e5ee';
    if(active.background==='dots')pattern.appendChild(el('circle',{cx:1,cy:1,r:1.2,fill:color}));
    if(active.background==='grid')pattern.appendChild(el('path',{d:'M 24 0 L 0 0 0 24',fill:'none',stroke:color,'stroke-width':.8}));
    if(active.background==='lines')pattern.appendChild(el('path',{d:'M 0 24 L 24 24',stroke:color,'stroke-width':.8}));
    defs.appendChild(pattern);svg.appendChild(defs);svg.appendChild(el('rect',{x:r.x,y:r.y,width:r.w,height:r.h,fill:'url(#export-grid)'}));
  }
  active.objects.forEach(o=>drawObject(o,svg));return {svg,width:Math.ceil(r.w),height:Math.ceil(r.h)};
}
async function exportPng(){const {svg,width:ww,height:hh}=makeExport();const svgText=new XMLSerializer().serializeToString(svg),data=new Blob([svgText],{type:'image/svg+xml'}),url=URL.createObjectURL(data);
  const img=new Image();try{
    await new Promise((resolve,reject)=>{img.onload=resolve;img.onerror=()=>reject(new Error('Image rendering failed — export SVG instead'));img.src=url;});
    const ratio=Math.min(2,12000/Math.max(ww,hh));const canvas=document.createElement('canvas');canvas.width=Math.round(ww*ratio);canvas.height=Math.round(hh*ratio);const ctx=canvas.getContext('2d');ctx.drawImage(img,0,0,canvas.width,canvas.height);
    await new Promise((resolve,reject)=>canvas.toBlob(blob=>{if(blob){downloadBlob(blob,safeFilename(active.name)+'.png');resolve();}else reject(new Error('PNG export failed'));},'image/png'));
    toast('PNG image exported');
  }catch(err){toast(err.message);console.error(err);}finally{URL.revokeObjectURL(url);}
}
async function exportPdf(){
  if(!active)return;
  const {svg,width:ww,height:hh}=makeExport();
  const ratio=Math.min(2,2200/Math.max(ww,hh));
  const url=URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(svg)],{type:'image/svg+xml'}));
  try{
    const img=new Image();await new Promise((resolve,reject)=>{img.onload=resolve;img.onerror=()=>reject(new Error('Unable to rasterize board'));img.src=url;});
    const canvas=document.createElement('canvas');canvas.width=Math.max(1,Math.round(ww*ratio));canvas.height=Math.max(1,Math.round(hh*ratio));
    canvas.getContext('2d').drawImage(img,0,0,canvas.width,canvas.height);
    const b64=canvas.toDataURL('image/jpeg',.9).split(',')[1];
    const jpeg=Uint8Array.from(atob(b64),c=>c.charCodeAt(0));
    const pdf=window.WhiteboardPdf.imagePdf(jpeg,canvas.width,canvas.height);
    downloadBlob(new Blob([pdf],{type:'application/pdf'}),safeFilename(active.name)+'.pdf');
    toast('PDF exported (one printable page)');
  }catch(e){toast('PDF export failed: '+e.message);console.error(e);}finally{URL.revokeObjectURL(url);}
}
function exportDialog(){if(!active)return;openDialog('Export your whiteboard','<div class="export-opts"><button id="expPdf">PDF document <span>Single printable A4 page</span></button><button id="expPng">PNG image <span>High-resolution image</span></button><button id="expSvg">SVG vector <span>Scalable graphics</span></button><button id="expJson">Board backup <span>Editable .bosha.json format</span></button></div><p>Your editable backup can be imported on another device.</p>');$('expPdf').onclick=()=>{closeDialog();exportPdf();};$('expPng').onclick=()=>{closeDialog();exportPng();};$('expSvg').onclick=()=>{closeDialog();exportSvg();};$('expJson').onclick=()=>{closeDialog();exportJson();};}
async function importBoard(file){if(!file)return;if(file.size>14*1024*1024){toast('File too large (14 MB limit)');return;}
  try{const b=M.normalizeBoard(JSON.parse(await file.text()));documents.set(b.id,b);await boardStore.put(b);closeDialog();await openBoard(b.id);toast('Board imported successfully');}catch(err){toast('Could not import board: '+err.message);}}
async function imageFile(file){if(!file||!active||!/^image\/(png|jpeg|webp|gif)$/.test(file.type)){toast('Choose a PNG, JPG, WebP or GIF');return;}if(file.size>4*1024*1024){toast('Image exceeds 4 MB limit');return;}
  const data=await new Promise((resolve,reject)=>{const reader=new FileReader();reader.onload=()=>resolve(reader.result);reader.onerror=reject;reader.readAsDataURL(file);});
  const img=new Image();img.onload=()=>{const [cx,cy]=centerWorld();const scale=Math.min(1,500/img.naturalWidth,400/img.naturalHeight);snapshot();const obj={...newObject('image',{x:cx-img.naturalWidth*scale/2,y:cy-img.naturalHeight*scale/2}),w:img.naturalWidth*scale,h:img.naturalHeight*scale,src:data};active.objects.push(obj);selected=new Set([obj.id]);setTool('select');render();scheduleSave();};img.onerror=()=>toast('Invalid image file');img.src=data;
}
function reactions(){openDialog('Add a reaction',`<div style="display:flex;flex-wrap:wrap;gap:10px">${['👍','❤️','✅','⭐','🎉','🔥','💡','👀','😊','❓','❗','👎'].map(e=>`<button class="soft-btn reaction" style="font-size:28px">${e}</button>`).join('')}</div>`);document.querySelectorAll('.reaction').forEach(b=>b.onclick=()=>{const [cx,cy]=centerWorld();snapshot();const obj=newObject('emoji',{x:cx-36,y:cy-36});obj.text=b.textContent;active.objects.push(obj);selected=new Set([obj.id]);render();scheduleSave();closeDialog();});}
function copySelection(){if(!active||!selected.size)return;clipboard=active.objects.filter(o=>selected.has(o.id)).map(M.clone);toast(`${clipboard.length} object(s) copied`);}
function pasteSelection(){if(!active||!clipboard.length)return;snapshot();const items=clipboard.map(obj=>{const o=M.clone(obj);o.id=M.uid();return M.translate(o,30,30);});active.objects.push(...items);selected=new Set(items.map(o=>o.id));clipboard=items.map(M.clone);render();scheduleSave();}
function toggleRuler(){const ruler=$('ruler');ruler.hidden=!ruler.hidden;
  $('rulerBtn').classList.toggle('active',!ruler.hidden);
  if(!ruler.hidden && !rulerPos.x && !rulerPos.y){rulerPos.x=110;rulerPos.y=135;}
  updateRuler();
}
function updateRuler(){const r=$('ruler');r.style.left=rulerPos.x+'px';r.style.top=rulerPos.y+'px';r.style.transform=`rotate(${rulerPos.deg}deg)`;}
function setupRuler(){const r=$('ruler');r.addEventListener('pointerdown',e=>{
  if(e.button!==0)return;r.setPointerCapture(e.pointerId);rulerDrag={x:e.clientX,y:e.clientY,startX:rulerPos.x,startY:rulerPos.y};e.preventDefault();e.stopPropagation();
});r.addEventListener('pointermove',e=>{if(!rulerDrag)return;rulerPos.x=rulerDrag.startX+e.clientX-rulerDrag.x;rulerPos.y=rulerDrag.startY+e.clientY-rulerDrag.y;updateRuler();});
  r.addEventListener('pointerup',()=>rulerDrag=null);r.addEventListener('pointercancel',()=>rulerDrag=null);
  r.addEventListener('wheel',e=>{e.preventDefault();e.stopPropagation();rulerPos.deg=(rulerPos.deg+(e.deltaY>0?5:-5)+360)%360;updateRuler();},{passive:false});
}
function allSelected(){if(!active)return;selected=new Set(active.objects.map(o=>o.id));render();}
function setupEvents(){
  $('canvas').addEventListener('pointerdown',pointerDown);$('canvas').addEventListener('pointermove',pointerMove);$('canvas').addEventListener('pointerup',pointerUp);$('canvas').addEventListener('pointercancel',pointerUp);$('canvas').addEventListener('lostpointercapture',()=>{if(gesture){gesture=null;$('preview').replaceChildren();}});
  $('canvas').addEventListener('wheel',wheel,{passive:false});
  $('canvas').addEventListener('dblclick',e=>{if(tool!=='select')return;const obj=findHit(worldPoint(e));if(obj)editObject(obj);});
  $('canvas').addEventListener('contextmenu',e=>{e.preventDefault();if(!active)return;const hit=findHit(worldPoint(e));if(hit){selected=new Set([hit.id]);render();const pos=screenPoint(e),menu=$('contextMenu');menu.hidden=false;menu.style.left=Math.min(pos.x,$('canvas').clientWidth-190)+'px';menu.style.top=Math.min(pos.y,$('canvas').clientHeight-205)+'px';}else{selected.clear();render();}});
  document.querySelectorAll('[data-tool]').forEach(b=>b.onclick=()=>setTool(b.dataset.tool));
  document.querySelectorAll('#contextMenu button').forEach(b=>b.onclick=()=>{const action=b.dataset.action;const obj=active.objects.find(o=>selected.has(o.id));$('contextMenu').hidden=true;if(action==='edit')editObject(obj);if(action==='delete')deleteSelected();if(action==='duplicate')duplicateSelected();if(action==='group')groupSelected();if(action==='ungroup')ungroupSelected();if(action==='front'||action==='back')layer(action);});
  document.addEventListener('pointerdown',e=>{if(!e.target.closest('#contextMenu')&&!e.target.closest('#canvas'))$('contextMenu').hidden=true;});
  $('strokeWidth').oninput=e=>{width=Number(e.target.value);$('strokeWidthValue').textContent=width;};$('moreColors').onclick=()=>$('colorInput').click();$('colorInput').oninput=e=>{ink=e.target.value;renderColors();if(selected.size)updateStyle('color',ink);};
  $('imageTool').onclick=()=>$('fileInput').click();$('fileInput').onchange=e=>{imageFile(e.target.files[0]);e.target.value='';};$('reactionTool').onclick=reactions;$('snapToggle').onchange=e=>{snapEnabled=e.target.checked;toast(snapEnabled?'Grid snapping enabled':'Grid snapping disabled');};setupRuler();
  $('homeBtn').onclick=gallery;$('newBoardBtn').onclick=createBoard;$('boardTitle').onclick=()=>{if(active)textPrompt('Rename whiteboard',active.name,name=>{active.name=name;render();scheduleSave();});};
  $('boardSearch').oninput=renderGallery;$('templatesBtn').onclick=templatesDialog;$('hintTemplate').onclick=templatesDialog;$('boardSettingsBtn').onclick=settingsDialog;$('importBtn').onclick=()=>$('boardInput').click();$('boardInput').onchange=e=>{importBoard(e.target.files[0]);e.target.value='';};$('exportBtn').onclick=exportDialog;
  $('undoBtn').onclick=undo;$('redoBtn').onclick=redo;$('zoomInBtn').onclick=()=>zoom(1.2);$('zoomOutBtn').onclick=()=>zoom(1/1.2);$('zoomDisplay').onclick=()=>{active.view.zoom=1;render();scheduleSave();};$('fitBtn').onclick=fitObjects;$('rulerBtn').onclick=toggleRuler;$('closeInspector').onclick=()=>{selected.clear();render();};
  $('dialogClose').onclick=closeDialog;$('dialogBackdrop').onclick=e=>{if(e.target===$('dialogBackdrop'))closeDialog();};
  document.addEventListener('keydown',e=>{
    if(e.key==='Escape'){closeDialog();$('contextMenu').hidden=true;selected.clear();render();return;}
    const typing=['INPUT','TEXTAREA','SELECT'].includes(document.activeElement?.tagName),modal=!$('dialogBackdrop').hidden;
    if(typing||modal||!active)return;
    if(e.code==='Space'){e.preventDefault();spaceHeld=true;render();return;}
    const mod=e.ctrlKey||e.metaKey, key=e.key.toLowerCase();
    if(mod){
      if(key==='z'){e.preventDefault();e.shiftKey?redo():undo();}
      if(key==='y'){e.preventDefault();redo();}
      if(key==='a'){e.preventDefault();allSelected();}
      if(key==='c'){e.preventDefault();copySelection();}
      if(key==='v'&&clipboard.length){e.preventDefault();pasteSelection();}
      if(key==='d'){e.preventDefault();duplicateSelected();}
      if(key==='g'){e.preventDefault();e.shiftKey?ungroupSelected():groupSelected();}
      if(key==='s'){e.preventDefault();exportJson();}
      return;
    }
    if(e.key==='Delete'||e.key==='Backspace'){e.preventDefault();deleteSelected();return;}
    if(key==='u'){e.preventDefault();toggleRuler();return;}
    const shortcuts={q:'lasso',b:'doubleArrow',v:'select',h:'hand',p:'pen',m:'highlighter',e:'eraser',l:'laser',n:'sticky',t:'text',r:'rect',o:'ellipse',d:'diamond',i:'line',a:'arrow'};
    if(shortcuts[key]){e.preventDefault();setTool(shortcuts[key]);}
  });
  document.addEventListener('keyup',e=>{if(e.code==='Space'){spaceHeld=false;if(active)render();}});
  document.addEventListener('paste',e=>{
    if(!active||!$('dialogBackdrop').hidden||['INPUT','TEXTAREA'].includes(document.activeElement.tagName))return;
    const img=[...e.clipboardData?.items||[]].find(i=>i.type.startsWith('image/'));
    if(img){e.preventDefault();imageFile(img.getAsFile());}
  });
  window.addEventListener('beforeunload',()=>{if(active&&db){try{const t=db.transaction('boards','readwrite');t.objectStore('boards').put(M.clone(active));}catch(e){}}});
  window.addEventListener('resize',()=>{if(active)render();});
}
async function initialize(){if(initialized)return;initialized=true;setupEvents();renderColors();updateHistoryButtons();await boardStore.init();const boards=await boardStore.all();boards.forEach(b=>documents.set(b.id,b));renderGallery();$('gallery').hidden=false;}
initialize().catch(e=>{console.error(e);toast('Could not start whiteboard: '+e.message);});
})();
