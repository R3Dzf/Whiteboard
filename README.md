# ✳ Bosha Whiteboard

**A clean, free, offline-first infinite whiteboard for Windows.** Sketch ideas, teach a class, plan a project, organize sticky notes, and keep your work locally without an account.

![Bosha Whiteboard](https://img.shields.io/badge/platform-Windows-5158dd) ![License](https://img.shields.io/badge/license-MIT-green) ![Status](https://img.shields.io/badge/status-v0.1.0%20prototype-orange)

> ⚠️ **Project status:** Functional first prototype, not a certified stable replacement. The portable executable and Windows installer are created by GitHub Actions after dependency installation succeeds. Builds have not yet been code-signed. Back up critical boards using Export → Board backup.

## What works now

- **Infinite-feeling canvas** — pan, zoom, fit, dark/light backgrounds, grids.
- **Drawing tools** — pen, highlighter, stroke eraser, laser, straight ink with Shift, rectangle, ellipse, diamond, line, arrow.
- **Content** — text boxes, colored sticky notes, emoji reactions, images (including clipboard paste).
- **Editing** — multi-selection, marquee selection, move, resize, recolor, duplicate, z-order, Undo and Redo.
- **Organize** — board gallery with search, rename, duplicate and delete.
- **Templates** — brainstorm, Kanban, SWOT, mind map, retrospective and lesson planning.
- **Your files** — automatic local IndexedDB storage, import/export editable `.bosha.json`, export SVG / PNG. No servers or monthly fee.

Full Microsoft Whiteboard comparison: **[FEATURES.md](FEATURES.md)** — it explicitly marks incomplete and planned functionality.

## Run on Windows

### Option 1 — Windows application (recommended once CI has passed)

1. Open **Actions → Windows build** in this repository.
2. Select a successful run on `main` and download `Bosha-Whiteboard-Windows` from Artifacts.
3. Extract and launch the **portable EXE** or install using the **NSIS setup**. Windows SmartScreen may warn about an unsigned community build; download only from this repo and verify the source.

### Option 2 — Open as a browser app

Download or clone this repository and open **`index.html`** using Chrome or Edge. Internet access is not required after files are available. Browser-specific local file privacy rules can impact storage; check that the "Saved locally" indicator appears and export a backup.

### Option 3 — Run from source

Requires Node.js 22+ and npm:

```bash
git clone https://github.com/R3Dzf/Whiteboard.git
cd Whiteboard
npm install
npm start
```

Build the Windows installer/portable EXE:

```bash
npm run check
npm run dist:win
```

The desktop build requires a Windows machine. A CI workflow is included for Windows packaging. The runtime UI itself uses vanilla JavaScript, SVG and native browser APIs with **no web/CDN dependencies**.

## Controls

| Action | Shortcut |
|---|---|
| Select / move | `V` |
| Pan | `H` or hold `Space` |
| Pen / highlighter / erase | `P` / `M` / `E` |
| Laser pointer | `L` |
| Note / text | `N` / `T` |
| Rectangle / ellipse / diamond | `R` / `O` / `D` |
| Line / arrow | `I` / `A` |
| Undo / redo | `Ctrl+Z` / `Ctrl+Y` |
| Select all / copy / paste | `Ctrl+A` / `Ctrl+C` / `Ctrl+V` |
| Duplicate / delete | `Ctrl+D` / `Delete` |
| Export editable backup | `Ctrl+S` |
| Zoom | Mouse wheel or bottom controls |
| Straight freehand ink | Hold `Shift` while drawing |
| Add multiple to selection | `Shift+click` or drag empty canvas |

## Data and security

Documents are stored in the app's local Chromium **IndexedDB**, not uploaded anywhere. Deleting the app's local profile or clearing site data can remove boards. Export editable `.bosha.json` files regularly. The importer validates object types, coordinates and supported image data URIs. **Microsoft `.whiteboard` files are not compatible** with the current version; export your Microsoft boards through its supported interfaces before they are lost.

## Development

```bash
npm run check  # JS syntax + model unit tests
```

`tests/browser-smoke.py` exercises the UI with Playwright in a local headless browser. Its about:blank test origin cannot access persistent storage, so it is **not** a storage verification test. Windows packaging, touch hardware, long-running performance, and full recovery tests must still be completed.

## Roadmap & parity

See [FEATURES.md](FEATURES.md) for a detailed checklist and the next five phases. Real-time collaboration, lasso, handwriting enhancement, PDF/PowerPoint import and native Android are not in v0.1.0 yet.

## License

MIT. Independent project; not affiliated with Microsoft.