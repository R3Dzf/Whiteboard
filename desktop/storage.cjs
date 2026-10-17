'use strict';
const fs = require('node:fs/promises');
const path = require('node:path');
const MAX_BYTES = 32 * 1024 * 1024;
const validId = id => typeof id === 'string' && /^[a-zA-Z0-9_-]{1,110}$/.test(id);
function assertBoard(board) {
  if (!board || !validId(board.id) || !Array.isArray(board.objects) || board.objects.length > 15000 ||
      typeof board.name !== 'string' || board.name.length > 120) throw new Error('Invalid board data');
  if (board.objects.some(item => !item || typeof item !== 'object' || typeof item.type !== 'string' || item.type.length > 32 ||
    !['stroke','rect','ellipse','diamond','line','arrow','doubleArrow','text','sticky','image','emoji'].includes(item.type))) {
    throw new Error('Invalid board object');
  }
  return board;
}
function createStorage(userData){
  const root=path.join(userData,'board-snapshots');
  let queue=Promise.resolve();
  const ready=()=>fs.mkdir(root,{recursive:true});
  const file=id=>{if(!validId(id))throw new Error('Invalid board ID');return path.join(root,id+'.json');};
  const serialize=board=>{
    assertBoard(board);
    const content=JSON.stringify(board);
    if(Buffer.byteLength(content)>MAX_BYTES)throw new Error('Board exceeds 32 MB; export large images separately');
    return content;
  };
  function transact(task){
    const operation=queue.then(task);queue=operation.catch(()=>{});return operation;
  }
  async function save(board){
    // Serialize immediately so the renderer cannot change data mid-write.
    const content=serialize(board),target=file(board.id);
    return transact(async()=>{
      await ready();
      const tmp=path.join(root,board.id+'.'+process.pid+'.tmp');
      try{
        await fs.writeFile(tmp,content,{encoding:'utf8',mode:0o600});
        await fs.copyFile(target,target+'.bak').catch(err=>{if(err.code!=='ENOENT')throw err;});
        await fs.rename(tmp,target);
      }finally{await fs.unlink(tmp).catch(()=>{});}
      return {id:board.id,saved:true};
    });
  }
  async function load(){
    return transact(async()=>{
      await ready();const list=await fs.readdir(root);const ids=new Set(list.filter(x=>/^[\w-]+\.json(?:\.bak)?$/.test(x)).map(x=>x.replace(/\.json(?:\.bak)?$/,'')));
      const boards=[];
      for(const id of ids){
        let board;
        for(const suffix of ['','.bak']){
          try{const data=await fs.readFile(file(id)+suffix,'utf8');if(Buffer.byteLength(data)>MAX_BYTES)continue;
            const b=assertBoard(JSON.parse(data));if(b.id===id){board=b;break;}
          }catch(err){if(err.code!=='ENOENT')console.warn('Inklume recovered/ignored snapshot',id,err.message);}
        }
        if(board)boards.push(board);
      }
      return boards;
    });
  }
  async function remove(id){return transact(async()=>{
    const target=file(id);
    for(const suffix of ['','.bak'])await fs.unlink(target+suffix).catch(err=>{if(err.code!=='ENOENT')throw err;});
    return true;
  });}
  return {save,load,remove,assertBoard};
}
module.exports={createStorage,assertBoard,validId};
