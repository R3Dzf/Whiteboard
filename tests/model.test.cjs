const test = require('node:test');
const assert = require('node:assert/strict');
const M = require('../src/model.js');
test('creates independent documents', () => { const a=M.emptyBoard('A'), b=M.emptyBoard('B'); assert.notEqual(a.id,b.id); assert.deepEqual(a.objects, []); });
test('bounds for mixed shapes', () => { assert.deepEqual(M.bounds([{x:10,y:20,w:40,h:50},{x:-5,y:-10,w:10,h:10}]), {x:-5,y:-10,w:55,h:80}); });
test('move stroke translates each point',()=>{const o={type:'stroke',x:1,y:2,w:5,h:5,points:[[1,2],[3,6]]};M.translate(o,2,-1);assert.deepEqual(o.points,[[3,1],[5,5]]);});
test('resize from anchor scales path',()=>{const o={type:'stroke',x:0,y:0,w:10,h:20,points:[[0,0],[10,20]]};M.resize(o,2,.5,0,0);assert.deepEqual(o.points,[[0,0],[20,10]]);});
test('import rejects scripts and corrupted files',()=>{assert.throws(()=>M.normalizeBoard({objects:[{type:'script',x:0,y:0,w:1,h:1}]}));assert.throws(()=>M.normalizeBoard({objects:[{type:'image',x:0,y:0,w:1,h:1,src:'javascript:alert(1)'}]}));});
test('text escapes XML',()=>{assert.equal(M.escapeXml('<b>&"'), '&lt;b&gt;&amp;&quot;');});
