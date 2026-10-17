# Bosha Whiteboard

**Free, private, offline-first whiteboard for Windows. Independent open-source alternative to the retired standalone Microsoft Whiteboard app.**

**Status: v0.2.0 beta source preview.** The Windows installer and portable EXE have not yet been validated. This project is not affiliated with Microsoft.

## What works

- Infinite SVG canvas, mouse-wheel zoom, pan, fit to objects, light/dark grid backgrounds.
- Pen and highlighter (pressure-aware pointer input), erase sampled portions of strokes, straight strokes with Shift, temporary laser pointer.
- Rectangle, ellipse, diamond, line, one-way and double-headed arrows, sticky notes, text, images and reaction emojis.
- Lasso and marquee selection, group/ungroup, align left/center/right/top/middle/bottom, move, resize, rotate, duplicate, layering and snapping.
- Movable, rotatable visual ruler (not a constraint/precision measurement tool yet).
- Multi-board gallery, browser IndexedDB autosave, six editable templates.
- PNG, SVG, single-page rasterized PDF and editable .bosha.json export, .bosha.json import.
- No login, tracking, analytics, API dependency or cloud upload.

## Run it

Open index.html in a modern browser to try the UI. Local storage behavior varies by file-origin security rules; the Electron app uses its local application origin.

To build and run the Electron desktop app, use Node.js 22+ on your computer:

    npm install
    npm start

To produce unsigned Windows installer and portable EXE files on Windows:

    npm install
    npm run dist:win

Run automated tests:

    npm run check

GitHub Actions also includes a Windows packaging workflow. Do not treat its presence as proof that a verified release has been produced.

## Shortcuts

V select, Q freehand lasso, H pan, P pen, M highlighter, E eraser, U ruler, B two-way arrow. Ctrl+G groups; Ctrl+Shift+G ungroups. Ctrl+Z/Y undo/redo; Ctrl+C/V/D copy/paste/duplicate; Ctrl+S exports editable backup. Click a rotation handle and hold Shift to snap the angle to 15-degree increments.

## Data and limitations

Boards live locally in IndexedDB. Export editable .bosha.json backups before clearing app data or changing devices. The one-page PDF export is a flattened image placed on an A4-sized page and may downscale large boards. Native PDF/PowerPoint import, Microsoft .whiteboard import, realtime collaboration, true ink-to-shape, Android and a verified signed release have not been implemented.

See [FEATURES.md](FEATURES.md) for the detailed Microsoft vs Bosha feature tracker. Licensed under MIT.
