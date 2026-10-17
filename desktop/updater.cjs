'use strict';
// Opt-in update control only. electron-updater uses NSIS blockmaps to request changed byte ranges.
function setupUpdater({app,win,updater}){
  let status={state:'idle'};
  const emit=(next)=>{status={...next};if(win&&!win.isDestroyed())win.webContents.send('inklume:update-status',status);return status;};
  if(!app.isPackaged || !updater){
    emit({state:'disabled'});
    return {get:()=>status,check:async()=>status,download:async()=>status,install:()=>false};
  }
  updater.autoDownload=false;
  updater.autoInstallOnAppQuit=false;
  updater.allowPrerelease=false;
  updater.disableDifferentialDownload=false;
  updater.on('checking-for-update',()=>emit({state:'checking'}));
  updater.on('update-available',info=>emit({state:'available',version:info.version}));
  updater.on('update-not-available',()=>emit({state:'current'}));
  updater.on('download-progress',p=>emit({state:'downloading',percent:p.percent,transferred:p.transferred,total:p.total}));
  updater.on('update-downloaded',info=>emit({state:'downloaded',version:info.version}));
  updater.on('error',err=>emit({state:'error',error:err?.message||String(err)}));
  return {
    get:()=>status,
    async check(){try{emit({state:'checking'});await updater.checkForUpdates();return status;}
      catch(err){return emit({state:'error',error:err.message});}},
    async download(){if(status.state!=='available')throw new Error('Check for an available update first');
      try{emit({state:'downloading',percent:0,transferred:0,total:0});await updater.downloadUpdate();return status;}
      catch(err){return emit({state:'error',error:err.message});}},
    install(){if(status.state!=='downloaded')throw new Error('Download update before installing');
      updater.quitAndInstall(false,true);return true;}
  };
}
module.exports={setupUpdater};
