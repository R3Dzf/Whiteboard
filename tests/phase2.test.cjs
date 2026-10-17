const test=require('node:test'),assert=require('node:assert/strict');
const M=require('../src/model.js');
const {imagePdf}=require('../src/pdf.js');
const rect=(id,x,y,w,h,extra={})=>({id,type:'rect',x,y,w,h,...extra});
test('lasso includes enclosed shapes and excludes far objects',()=>{
 const polygon=[[0,0],[60,0],[60,60],[0,60]];
 assert.equal(M.lassoHits(rect('a',10,10,15,15),polygon),true);
 assert.equal(M.lassoHits(rect('b',120,120,15,15),polygon),false);
 assert.equal(M.lassoHits(rect('c',-20,20,110,10),polygon),true);
});
test('lasso crossing and polygon inside large object',()=>{
 const shape=rect('t',-100,-100,200,200);
 assert.equal(M.lassoHits(shape,[[0,0],[5,0],[5,5],[0,5]]),true);
 assert.equal(M.isPointInPolygon([0,0],[[10,10],[40,10],[40,40],[10,40]]),false);
});
test('grouping and ungrouping preserves object IDs',()=>{
 const objs=[rect('a',0,0,4,4),rect('b',10,0,4,4),rect('c',20,0,4,4)];
 const id=M.groupObjects(objs,new Set(['a','b']));
 assert.equal(objs[0].groupId,id);assert.equal(objs[1].groupId,id);
 assert.deepEqual([...M.expandGrouped(objs,new Set(['b']))].sort(),['a','b']);
 assert.equal(M.ungroupObjects(objs,new Set(['a'])),true);
 assert.equal(objs[1].groupId,undefined);
});
test('align objects moves strokes without losing pressure',()=>{
 const stroke={id:'a',type:'stroke',x:5,y:0,w:5,h:10,points:[[5,0,.25],[10,10,.9]]};
 const other=rect('b',20,0,6,10);
 M.alignObjects([stroke,other],new Set(['a','b']),'left');
 assert.deepEqual(stroke.points,[[5,0,.25],[10,10,.9]]);
 assert.equal(other.x,5);
 M.translate(stroke,10,2);assert.deepEqual(stroke.points,[[15,2,.25],[20,12,.9]]);
});
test('eraser splits into pieces, and does not mutate original',()=>{
 const stroke={id:'k',type:'stroke',x:0,y:0,w:100,h:0,points:Array.from({length:11},(_,i)=>[i*10,0,.5])};
 const parts=M.eraseStroke(stroke,{x:50,y:0},5);
 assert.equal(parts.length,2);assert.ok(parts[0].points.length>1);assert.ok(parts[1].points.length>1);
 assert.equal(stroke.points.length,11);
 assert.equal(M.eraseStroke(stroke,{x:500,y:500},5),null);
});
test('rotated selection bounding and inverse hit',()=>{
 const r=rect('x',100,100,100,20,{rotation:90});const b=M.rectFor(r);
 assert.ok(Math.abs(b.w-20)<.001);assert.ok(Math.abs(b.h-100)<.001);
 assert.equal(M.hit(r,150,70,0),true);assert.equal(M.hit(r,105,110,0),false);
});
test('snap rounds correctly for negative coordinates',()=>{
 assert.deepEqual(M.snapPoint({x:-13,y:37}),{x:-24,y:48});
});
test('import rejects malformed pressure, rotation, bad groups',()=>{
 const base={type:'stroke',x:0,y:0,w:10,h:5};
 assert.throws(()=>M.normalizeBoard({objects:[{...base,points:[[0,0,Infinity]]}]}));
 assert.throws(()=>M.normalizeBoard({objects:[{...base,points:[[0,0,3]]}]}));
 assert.throws(()=>M.normalizeBoard({objects:[rect('a',1,1,10,10,{rotation:'rotate(1)'})]}));
 assert.throws(()=>M.normalizeBoard({objects:[rect('a',1,1,10,10,{groupId:4})]}));
});
test('dependency-free PDF writer makes valid xref and image object',()=>{
 const pdf=imagePdf(new Uint8Array([255,216,255,217]),300,100);
 const text=Buffer.from(pdf).toString('latin1');
 assert.equal(text.slice(0,8),'%PDF-1.4');assert.ok(text.includes('/Filter /DCTDecode'));
 assert.ok(text.includes('/MediaBox [0 0 841.89 595.28]'));
 assert.ok(text.includes('%%EOF'));
 assert.throws(()=>imagePdf(new Uint8Array([0]),5,5));
});
