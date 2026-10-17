'use strict';
const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('inklumeDesktop',Object.freeze({
  loadBoards:()=>ipcRenderer.invoke('inklume:boards:list'),
  saveBoard:board=>ipcRenderer.invoke('inklume:boards:save',board),
  removeBoard:id=>ipcRenderer.invoke('inklume:boards:remove',id),
  checkUpdates:()=>ipcRenderer.invoke('inklume:updates:check'),
  downloadUpdate:()=>ipcRenderer.invoke('inklume:updates:download'),
  installUpdate:()=>ipcRenderer.invoke('inklume:updates:install'),
  getUpdateStatus:()=>ipcRenderer.invoke('inklume:updates:status'),
  onUpdateStatus:callback=>{if(typeof callback!=='function')return;
    ipcRenderer.on('inklume:update-status',(_event,status)=>callback(status));}
}));
