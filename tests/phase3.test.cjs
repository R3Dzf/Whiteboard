const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs/promises');
const os=require('node:os');
const path=require('node:path');
const EventEmitter=require('node:events');
const {createStorage,assertBoard}=require('../desktop/storage.cjs');
const {setupUpdater}=require('../desktop/updater.cjs');
const sample=(id='board-001')=>({id,name:'Study board ✨',updatedAt:Date.now(),objects:[{id:'s1',type:'sticky',x:0,y:0,w:50,h:60,text:'Hello'}]});
test('native board snapshots persist and reopen with original ID',async t=>{
  const root=await fs.mkdtemp(path.join(os.tmpdir(),'inklume-'));
  t.after(()=>fs.rm(root,{recursive:true,force:true}));
  const storage=createStorage(root);await storage.save(sample());
  const restored=await createStorage(root).load();assert.equal(restored[0].id,'board-001');assert.equal(restored[0].name,'Study board ✨');
});
test('native snapshot uses backup when current file is damaged',async t=>{
  const root=await fs.mkdtemp(path.join(os.tmpdir(),'inklume-'));t.after(()=>fs.rm(root,{recursive:true,force:true}));
  const s=createStorage(root);await s.save(sample());await s.save({...sample(),name:'Edited'});
  await fs.writeFile(path.join(root,'board-snapshots','board-001.json'),'{broken');
  const all=await s.load();assert.equal(all.length,1);assert.equal(all[0].name,'Study board ✨');
});
test('native snapshot deletion removes main and backup',async t=>{
  const root=await fs.mkdtemp(path.join(os.tmpdir(),'inklume-'));t.after(()=>fs.rm(root,{recursive:true,force:true}));
  const s=createStorage(root);await s.save(sample());await s.save(sample());await s.remove('board-001');assert.deepEqual(await s.load(),[]);
});
test('invalid path traversal and malformed boards rejected',async()=>{
  assert.throws(()=>assertBoard(sample('../../bad')));assert.throws(()=>assertBoard({...sample(),objects:[{type:'script'}]}));
});
test('updater requires consent before downloading and installing',async()=>{
  const updater=new EventEmitter();let downloads=0,installs=0;
  updater.checkForUpdates=async()=>updater.emit('update-available',{version:'0.4.0'});
  updater.downloadUpdate=async()=>{downloads++;updater.emit('download-progress',{percent:40,transferred:4,total:10});updater.emit('update-downloaded',{version:'0.4.0'});};
  updater.quitAndInstall=()=>{installs++;};
  const sent=[];const api=setupUpdater({app:{isPackaged:true},win:{isDestroyed:()=>false,webContents:{send:(_,v)=>sent.push(v)}},updater});
  assert.equal(downloads,0);assert.equal(installs,0);
  await api.check();assert.equal(api.get().state,'available');assert.equal(downloads,0);
  await api.download();assert.equal(api.get().state,'downloaded');assert.equal(downloads,1);
  api.install();assert.equal(installs,1);assert.ok(sent.some(s=>s.percent===40));
});

test('ink recognition supports clear circles, rectangles and lines',()=>{
  const M=require('../src/model.js');
  const line={type:'stroke',points:[[0,0],[30,1],[60,-1],[100,0]]};assert.equal(M.recognizeInkStroke(line)?.type,'line');
  const circle={type:'stroke',points:Array.from({length:50},(_,i)=>[100+50*Math.cos(i*2*Math.PI/49),100+50*Math.sin(i*2*Math.PI/49)])};assert.equal(M.recognizeInkStroke(circle)?.type,'ellipse');
  const rectangle={type:'stroke',points:[[0,0],[25,0],[50,0],[100,0],[100,25],[100,50],[100,80],[60,80],[20,80],[0,80],[0,45],[0,0]]};assert.equal(M.recognizeInkStroke(rectangle)?.type,'rect');
});
