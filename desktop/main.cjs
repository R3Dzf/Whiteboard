const { app, BrowserWindow, shell } = require('electron');
const path = require('node:path');
const createWindow = () => {
  const win = new BrowserWindow({
    width: 1440, height: 900, minWidth: 940, minHeight: 620,
    backgroundColor: '#f7f8fb', title: 'Bosha Whiteboard',
    autoHideMenuBar: true,
    webPreferences: {
      nodeIntegration: false, contextIsolation: true, sandbox: true,
      webSecurity: true, spellcheck: true
    }
  });
  win.loadFile(path.join(__dirname, '..', 'index.html'));
  win.webContents.setWindowOpenHandler(({ url }) => {
    if (/^https:\/\//.test(url)) shell.openExternal(url);
    return { action: 'deny' };
  });
};
app.whenReady().then(() => {
  createWindow();
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
});
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
