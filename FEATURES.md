# Inklume vs. Microsoft Whiteboard — Feature inventory

Updated: **2026-10-17** — **Inklume v0.3.0 beta source** (formerly Bosha Whiteboard)

Legend: ✅ implemented in source / applicable automated tests; ◐ partial, unverified on Windows hardware, or depends on an untested packaged component; ⬜ missing; — intentionally outside independent-app scope. “Microsoft” indicates a feature in some supported Whiteboard experience, not necessarily every Microsoft account. The standalone Microsoft Whiteboard apps retired **Oct 16 2026** (web & Teams continue for supported enterprise accounts).

| Category | Capability | Microsoft | Inklume v0.3.0 | Detail / outstanding work |
|---|---|:---:|:---:|---|
| Platform | Windows desktop | Retired | ◐ | Electron packaging exists; v0.3 installer not yet certified |
| Platform | Browser | ✅ | ✅ | Local static web UI; PDF import requires bundled pdfjs-dist assets |
| Platform | Android | Retired | ⬜ | Not implemented |
| Platform | iOS | Retired | ⬜ | Not implemented |
| Accounts | Use without sign-in | Varies | ✅ | Fully local-first |
| Canvas | Infinite pan and zoom | ✅ | ✅ | Zoom, fit, pan and shortcuts |
| Canvas | Light and dark backgrounds | ✅ | ✅ | Board-specific |
| Canvas | Grid, dots and ruled | ✅ | ✅ | Board-specific |
| Canvas | Finger gestures and pinch | ✅ | ◐ | Hardware behavior untested |
| Canvas | Movable / rotatable ruler | ✅ | ◐ | Visual ruler; line guidance not implemented |
| Canvas | Snap to grid | Varies | ✅ | 24-unit snap |
| Ink | Pen & highlighter | ✅ | ✅ | Colors, widths and stroke smoothing |
| Ink | Pressure-sensitive stylus | ✅ | ◐ | PointerEvent pressure, hardware untested |
| Ink | Partial eraser | ✅ | ✅ | Erases portions of sampled strokes |
| Ink | Ink-to-shape recognition | ✅ | ◐ | v0.3 manual conversion: clear line/rectangle/ellipse only |
| Ink | Lasso | ✅ | ✅ | Freehand path selection |
| Ink | Straight line / Shift | ✅ | ✅ | Drawing tools |
| Ink | One-way & two-way arrows | ✅ | ✅ | Geometry tools |
| Ink | Laser pointer | ✅ | ✅ | Nonpersistent |
| Elements | Text / sticky notes | ✅ | ✅ | Editable |
| Elements | Note grids | ✅ | ◐ | Templates approximate note grids |
| Elements | Shapes / diagrams | ✅ | ✅ | Rectangle, oval, diamond, arrows etc |
| Elements | Images | ✅ | ✅ | Upload, drag-drop, clipboard paste |
| Elements | Reactions | ✅ | ✅ | Emoji elements |
| Elements | PDF pages import | ✅ | ◐ | v0.3 PDF.js source code (12 pages max); Windows packaged test needed |
| Elements | PowerPoint import | ✅ | ⬜ | Not available |
| Elements | Live links/media embedding | Varies | ⬜ | No interactive embedding |
| Editing | Marquee multi-select | ✅ | ✅ | Select by rectangle |
| Editing | Lasso selection | ✅ | ✅ | Free-form path |
| Editing | Move / resize | ✅ | ✅ | Resize rotated objects remains limited |
| Editing | Rotation | ✅ | ◐ | Numeric / handle controls |
| Editing | Group / ungroup | ✅ | ✅ | Persistent group IDs |
| Editing | Align objects | ✅ | ✅ | Six alignments |
| Editing | Duplicate / copy / paste | ✅ | ✅ | Inside app; system image paste supported |
| Editing | Layer ordering | ✅ | ✅ | Front/back + model functions |
| Editing | Undo / redo | ✅ | ✅ | 80 object snapshots |
| Templates | Starter board templates | ✅ | ✅ | Six presets |
| Templates | Large reusable template library | ✅ | ◐ | Limited to six layouts |
| Templates | Custom template saving | ✅ | ⬜ | JSON export as workaround |
| Storage | Multi-board gallery/search | ✅ | ✅ | Works locally |
| Storage | Local autosave | MS cloud | ◐ | IndexedDB + native atomic snapshots; native integration unverified on Windows |
| Storage | Crash recovery backup | Different | ✅ | Native `.bak` fallback tested |
| Storage | v0.2 board migration | Different | ◐ | Schema & path retained; in-place upgrade test needed |
| Storage | Export/import editable JSON | Different | ✅ | `.inklume.json`, compatible with `.bosha.json` |
| Storage | Microsoft `.whiteboard` import | Native | ⬜ | Format migration not implemented |
| Export | PNG / SVG | ✅/varies | ✅ | Export tools |
| Export | PDF | Varies | ✅ | One-page flattened PDF, not editable |
| Collaboration | Multiple live editors | ✅ | ⬜ | Requires backend/session protocol |
| Collaboration | Live cursors | ✅ | ⬜ | Not implemented |
| Collaboration | Invite links / permissions | ✅ | ⬜ | Not implemented |
| Integration | Microsoft Teams | ✅ | — | Independent product |
| Privacy | No cloud account / telemetry | Varies | ✅ | Local by default |
| Branding | Independent name and artwork | — | ✅ | **Inklume** icon PNG/ICO/favicon |
| Updates | Manual check / download / install | Former apps | ◐ | Electron-updater state machine; real feed & Windows QA required |
| Updates | Differential/Delta downloads | Varies | ◐ | v0.3 NSIS blockmap config; **not yet end-to-end tested** |
| Updates | Download progress display | Varies | ✅ | User-facing percentage/bytes UI |
| Updates | Automatic fallback to full | Varies | ◐ | Provided by electron-updater; validate with real pair of releases |
| Releases | Windows installer / portable | Retired | ◐ | Build config, not a verified v0.3 release |
| Releases | Signed stable release | Retired | ⬜ | Certificate and QA needed |
| QA | Automated tests | Internal | ✅ | 21 source/unit tests; PDF integration smoke outstanding |

## Remaining prioritized milestones

1. **v0.3 acceptance:** Install on Windows, validate native save/recovery and old-board migration, PDF import (scanned & text), touch + stylus, and verify release artifacts.
2. **v0.3→v0.4 update test:** Publish two signed NSIS releases with `latest.yml` and blockmaps, measure actual transferred bytes and fallback behavior; test no silent install, no data loss.
3. **v0.4 usability:** Improve PDF page layout, multipage PDF export, robust native templates, ruler-guided ink, better stylus pressure and large-canvas performance.
4. **v0.5+:** Secure opt-in local-network collaboration / edit & viewer roles, eventual Android client, Microsoft file migration if format access is viable.

## Sources

- https://support.microsoft.com/en-us/whiteboard/retirement-standalone-microsoft-whiteboard-apps
- https://support.microsoft.com/en-us/whiteboard/draw-and-ink-in-whiteboard
- https://support.microsoft.com/en-us/whiteboard/whiteboard-create-panel
- https://www.electron.build/v26/docs/nsis/
- https://www.electron.build/v26/docs/features/auto-update/
