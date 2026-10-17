'use strict';
const {app,BrowserWindow,shell,ipcMain,session} = require('electron');
const path=require('node:path');
const {createStorage}=require('./storage.cjs');
const {setupUpdater}=require('./updater.cjs');

// Preserve the v0.1/v0.2 IndexedDB profile across the commercial-name change.
app.setPath('userData',path.join(app.getPath('appData'),'Bosha Whiteboard'));
const storage=createStorage(app.getPath('userData'));
let updates;
function createWindow(){
  const win=new BrowserWindow({
    width:1440,height:900,minWidth:940,minHeight:620,title:'Inklume',
    icon:path.join(__dirname,'..','assets','icon.png'),
    backgroundColor:'#f7f8fb',autoHideMenuBar:true,
    webPreferences:{preload:path.join(__dirname,'preload.cjs'),nodeIntegration:false,
      contextIsolation:true,sandbox:true,webSecurity:true,spellcheck:true}
  });
  win.loadFile(path.join(__dirname,'..','index.html'));
  win.webContents.setWindowOpenHandler(({url})=>{
    if(/^https:\/\//i.test(url)){shell.openExternal(url).catch(console.warn);}
    return {action:'deny'};
  });
  win.webContents.on('will-navigate',event=>event.preventDefault());
  let updater;
  if(app.isPackaged && !process.env.PORTABLE_EXECUTABLE_DIR){try{updater=require('electron-updater').autoUpdater;}catch(err){console.warn('Updater unavailable',err);}}
  updates=setupUpdater({app,win,updater});
}
function registerIpc(){
  ipcMain.handle('inklume:boards:list',()=>storage.load());
  ipcMain.handle('inklume:boards:save',(_e,board)=>storage.save(board));
  ipcMain.handle('inklume:boards:remove',(_e,id)=>storage.remove(id));
  ipcMain.handle('inklume:updates:status',()=>updates?.get()||{state:'disabled'});
  ipcMain.handle('inklume:updates:check',()=>updates?.check()||{state:'disabled'});
  ipcMain.handle('inklume:updates:download',()=>updates?.download()||{state:'disabled'});
  ipcMain.handle('inklume:updates:install',()=>updates?.install()||false);
}
if(app.requestSingleInstanceLock()){
  app.whenReady().then(()=>{
    session.defaultSession.setPermissionRequestHandler((_wc,_permission,cb)=>cb(false));
    registerIpc();createWindow();
    app.on('activate',()=>{if(BrowserWindow.getAllWindows().length===0)createWindow();});
  });
  app.on('second-instance',()=>{const win=BrowserWindow.getAllWindows()[0];if(win){if(win.isMinimized())win.restore();win.focus();}});
}else app.quit();
app.on('window-all-closed',()=>{if(process.platform!=='darwin')app.quit();});
