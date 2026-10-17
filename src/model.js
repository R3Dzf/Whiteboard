/* Deterministic document operations, independently testable in Node and in the UI. */
(function (root) {
  'use strict';
  const clone = (value) => JSON.parse(JSON.stringify(value));
  const uid = () => (typeof crypto !== 'undefined' && crypto.randomUUID)
    ? crypto.randomUUID() : `wb-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  const emptyBoard = (name = 'Untitled board') => ({
    id: uid(), name, createdAt: Date.now(), updatedAt: Date.now(),
    background: 'dots', theme: 'light', view: { x: 0, y: 0, zoom: 1 }, objects: []
  });
  const isNumber = (n) => typeof n === 'number' && Number.isFinite(n);
  const validObjectTypes = ['stroke', 'rect', 'ellipse', 'diamond', 'line', 'arrow', 'doubleArrow', 'text', 'sticky', 'image', 'emoji'];
  function normalizeBoard(input) {
    if (!input || typeof input !== 'object' || !Array.isArray(input.objects)) throw new Error('Invalid board file');
    if (input.objects.length > 15000) throw new Error('Board exceeds 15,000 objects');
    const b = emptyBoard(String(input.name || 'Imported board').slice(0, 100));
    b.objects = input.objects.map(item => {
      if (!item || !validObjectTypes.includes(item.type)) throw new Error('Unknown object type');
      if (![item.x, item.y, item.w, item.h].every(isNumber)) throw new Error('Invalid object coordinates');
      if ([item.x, item.y, item.w, item.h].some(n => Math.abs(n) > 1e7)) throw new Error('Object coordinates out of range');
      if (item.type === 'image' && (!/^data:image\/(png|jpeg|webp|gif);base64,/.test(item.src || '') || (item.src || '').length > 10000000)) throw new Error('Unsafe image');
      if (item.type === 'stroke' && (!Array.isArray(item.points) || item.points.length > 100000 || !item.points.every(p=>Array.isArray(p) && (p.length===2||p.length===3) && p.every(isNumber) && Math.abs(p[0])<=1e7 && Math.abs(p[1])<=1e7 && (p.length<3||(p[2]>=0&&p[2]<=1))))) throw new Error('Invalid stroke');
      if (item.rotation !== undefined && (!isNumber(item.rotation) || Math.abs(item.rotation)>36000)) throw new Error('Invalid object rotation');
      if (item.groupId !== undefined && (typeof item.groupId !== 'string' || item.groupId.length>100)) throw new Error('Invalid group identifier');
      const out = clone(item); out.id = uid();
      out.text = String(item.text || '').slice(0, 10000);
      return out;
    });
    b.background = ['plain','dots','grid','lines'].includes(input.background) ? input.background : 'dots';
    b.theme = input.theme === 'dark' ? 'dark' : 'light';
    return b;
  }
  const rawRect = o => ({ x: Math.min(o.x, o.x + o.w), y: Math.min(o.y, o.y + o.h), w: Math.abs(o.w), h: Math.abs(o.h) });
  const rectFor = (o) => {
    if (o.type === 'stroke' && o.points && o.points.length) {
      const xs = o.points.map(p => p[0]), ys = o.points.map(p => p[1]);
      const minX = Math.min(...xs), minY = Math.min(...ys);
      return { x: minX, y: minY, w: Math.max(1, Math.max(...xs) - minX), h: Math.max(1, Math.max(...ys) - minY) };
    }
    const r=rawRect(o), angle=((o.rotation||0)*Math.PI)/180;
    if(!angle) return r;
    const cx=r.x+r.w/2,cy=r.y+r.h/2,c=Math.cos(angle),s=Math.sin(angle);
    const rotated=[[r.x,r.y],[r.x+r.w,r.y],[r.x+r.w,r.y+r.h],[r.x,r.y+r.h]].map(([x,y])=>[cx+(x-cx)*c-(y-cy)*s,cy+(x-cx)*s+(y-cy)*c]);
    const xs=rotated.map(v=>v[0]),ys=rotated.map(v=>v[1]);
    return {x:Math.min(...xs),y:Math.min(...ys),w:Math.max(...xs)-Math.min(...xs),h:Math.max(...ys)-Math.min(...ys)};
  };
  function bounds(objects) {
    if (!objects.length) return { x: -400, y: -300, w: 800, h: 600 };
    const r = objects.map(rectFor);
    const left = Math.min(...r.map(o=>o.x)), top = Math.min(...r.map(o=>o.y));
    return { x: left, y: top, w: Math.max(...r.map(o=>o.x+o.w))-left, h: Math.max(...r.map(o=>o.y+o.h))-top };
  }
  const hit = (o, x, y, gap = 8) => {
    if(o.rotation && o.type!=='stroke') {
      const r=rawRect(o),cx=r.x+r.w/2,cy=r.y+r.h/2,t=-o.rotation*Math.PI/180;
      const dx=x-cx,dy=y-cy;x=cx+dx*Math.cos(t)-dy*Math.sin(t);y=cy+dx*Math.sin(t)+dy*Math.cos(t);
    }
    const r = rawRect(o.type==='stroke'?{...o,...rectFor(o)}:o);
    return x >= r.x-gap && x <= r.x+r.w+gap && y >= r.y-gap && y <= r.y+r.h+gap;
  };
  const translate = (o, dx, dy) => {
    o.x += dx; o.y += dy;
    if (o.type === 'stroke') o.points = o.points.map(p=>[p[0]+dx,p[1]+dy,...p.slice(2)]);
    return o;
  };
  const resize = (o, sx, sy, anchorX, anchorY) => {
    o.x = anchorX + (o.x-anchorX)*sx; o.y = anchorY + (o.y-anchorY)*sy;
    o.w *= sx; o.h *= sy;
    if (o.type === 'stroke') o.points = o.points.map(([x,y])=>[anchorX+(x-anchorX)*sx, anchorY+(y-anchorY)*sy]);
    return o;
  };

  const distanceSegment = (p,a,b) => {
    const dx=b[0]-a[0],dy=b[1]-a[1],den=dx*dx+dy*dy;
    const t=den===0?0:Math.max(0,Math.min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/den));
    return Math.hypot(p[0]-a[0]-t*dx,p[1]-a[1]-t*dy);
  };
  const isPointInPolygon=(point,poly)=>{
    let inside=false;
    for(let i=0,j=poly.length-1;i<poly.length;j=i++){
      const a=poly[i],b=poly[j];
      if((a[1]>point[1])!==(b[1]>point[1]) && point[0]<(b[0]-a[0])*(point[1]-a[1])/(b[1]-a[1])+a[0])inside=!inside;
    }
    return inside;
  };
  const cross=(a,b,c)=>(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]);
  function segmentsIntersect(a,b,c,d){
    const ab=cross(a,b,c),abd=cross(a,b,d),cd=cross(c,d,a),cdb=cross(c,d,b);
    return ((ab<=0&&abd>=0)||(ab>=0&&abd<=0))&&((cd<=0&&cdb>=0)||(cd>=0&&cdb<=0));
  }
  function lassoHits(o,poly){
    if(!Array.isArray(poly)||poly.length<3)return false;
    const r=rectFor(o),corners=[[r.x,r.y],[r.x+r.w,r.y],[r.x+r.w,r.y+r.h],[r.x,r.y+r.h]];
    if(corners.some(p=>isPointInPolygon(p,poly)))return true;
    if(poly.some(p=>p[0]>=r.x&&p[0]<=r.x+r.w&&p[1]>=r.y&&p[1]<=r.y+r.h))return true;
    for(let i=0;i<poly.length;i++)for(let j=0;j<4;j++)if(segmentsIntersect(poly[i],poly[(i+1)%poly.length],corners[j],corners[(j+1)%4]))return true;
    return false;
  }
  function eraseStroke(o,at,radius=9){
    if(o.type!=='stroke'||!o.points?.length)return null;
    const pts=o.points;
    const adjacent=pts.map((q,i)=>Math.hypot(q[0]-at.x,q[1]-at.y)<=radius ||
      (i>0&&distanceSegment([at.x,at.y],pts[i-1],q)<=radius) ||
      (i<pts.length-1&&distanceSegment([at.x,at.y],q,pts[i+1])<=radius));
    if(!adjacent.some(Boolean))return null;
    const chunks=[];let current=[];
    pts.forEach((q,i)=>{if(adjacent[i]){if(current.length){chunks.push(current);current=[];}}else current.push(q)});
    if(current.length)chunks.push(current);
    return chunks.filter(c=>c.length>=2).map(chunk=>{
      const out=clone(o);out.id=uid();out.points=chunk;
      const xs=chunk.map(q=>q[0]),ys=chunk.map(q=>q[1]);out.x=Math.min(...xs);out.y=Math.min(...ys);out.w=Math.max(1,Math.max(...xs)-out.x);out.h=Math.max(1,Math.max(...ys)-out.y);
      return out;
    });
  }
  function expandGrouped(objects, ids){
    const included=new Set(ids), groups=new Set(objects.filter(o=>included.has(o.id)&&o.groupId).map(o=>o.groupId));
    return new Set(objects.filter(o=>included.has(o.id)||(o.groupId&&groups.has(o.groupId))).map(o=>o.id));
  }
  function groupObjects(objects, ids){
    const all=expandGrouped(objects,ids);if(all.size<2)return null;
    const id=uid();objects.forEach(o=>{if(all.has(o.id))o.groupId=id});return id;
  }
  function ungroupObjects(objects,ids){const groups=new Set(objects.filter(o=>ids.has(o.id)&&o.groupId).map(o=>o.groupId));
    if(!groups.size)return false;objects.forEach(o=>{if(groups.has(o.groupId))delete o.groupId});return true;
  }
  function alignObjects(objects,ids,mode){
    const chosen=objects.filter(o=>ids.has(o.id));if(chosen.length<2)return false;
    const rects=chosen.map(rectFor),left=Math.min(...rects.map(r=>r.x)),right=Math.max(...rects.map(r=>r.x+r.w)),top=Math.min(...rects.map(r=>r.y)),bottom=Math.max(...rects.map(r=>r.y+r.h));
    chosen.forEach((o,i)=>{const r=rects[i],target=mode==='left'?left:mode==='right'?right-r.w:mode==='center'?(left+right-r.w)/2:null;
      const targetY=mode==='top'?top:mode==='bottom'?bottom-r.h:mode==='middle'?(top+bottom-r.h)/2:null;
      translate(o,(target??r.x)-r.x,(targetY??r.y)-r.y);});return true;
  }
  function snapPoint(p,step=24){return {x:Math.round(p.x/step)*step,y:Math.round(p.y/step)*step};}
  const escapeXml = (str) => String(str).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));
  const textLines = (text, chars = 26) => {
    const lines = [];
    for (const para of String(text || '').split('\n')) {
      if (!para) { lines.push(''); continue; }
      let curr = '';
      for (const word of para.split(/(\s+)/).filter(Boolean)) {
        if (curr.length + word.length > chars && curr.trim()) { lines.push(curr.trimEnd()); curr = ''; }
        let rest = word;
        while (rest.length > chars && !curr) { lines.push(rest.slice(0,chars)); rest = rest.slice(chars); }
        curr += rest;
      }
      lines.push(curr.trimEnd());
    }
    return lines.slice(0, 100);
  };
  const api = { clone, uid, emptyBoard, normalizeBoard, rectFor, bounds, hit, translate, resize, escapeXml, textLines, distanceSegment, isPointInPolygon, lassoHits, eraseStroke, expandGrouped, groupObjects, ungroupObjects, alignObjects, snapPoint };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  root.WhiteboardModel = api;
})(typeof window === 'undefined' ? globalThis : window);
