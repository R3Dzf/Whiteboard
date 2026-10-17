# Microsoft Whiteboard vs Bosha Whiteboard — feature tracker

Updated 2026-10-17 · Bosha v0.2.0 beta source preview

Legend: ✅ Implemented in source; 🟡 partial / needs validation; ⬜ not built; ➖ intentionally out of scope. Microsoft status follows published feature docs; availability depends on edition.

Microsoft retired its standalone Windows, iOS and Android apps on October 16, 2026. Whiteboard on the web and in Teams remains available for eligible work/school users.

Area | Feature | Microsoft Whiteboard | Bosha v0.2 | Status / note
---|---|:---:|:---:|---
Platform | Windows standalone | Retired | 🟡 | Electron source; installer unvalidated
Platform | Browser interface | ✅ | ✅ | Offline drawing core
Platform | Android native app | Retired | ⬜ | Not built
Platform | iOS native app | Retired | ⬜ | Not built
Account | No-account use | Varies | ✅ | No sign-in needed
Canvas | Infinite canvas / zoom / pan | ✅ | ✅ | Includes fit-to-content
Canvas | Grid, ruled, dotted, dark surface | ✅ | ✅ | Per board
Canvas | Touch / pinch | ✅ | 🟡 | Implemented; hardware validation pending
Canvas | Ruler | ✅ | 🟡 | Movable rotatable overlay; no ink constraint
Canvas | Snap-to-grid | Varies | ✅ | Toggle
Ink | Pen, thickness, custom colors | ✅ | ✅ | Mouse/pen
Ink | Pressure-sensitive pen | ✅ | 🟡 | PointerEvent pressure; hardware validation pending
Ink | Highlighter | ✅ | ✅ | Semi-transparent
Ink | Eraser | ✅ | ✅ | Sampled stroke segments
Ink | Straight line ink with Shift | ✅ | ✅ | Pen mode
Ink | Ink-to-shape auto recognition | ✅ | ⬜ | Not built
Ink | Laser pointer | ✅ | ✅ | Not saved
Ink | Double arrow mode | ✅ | ✅ | Shape equivalent
Elements | Shapes, line, arrow, text | ✅ | ✅ | Editable
Elements | Sticky notes | ✅ | ✅ | Multiple colors
Elements | Note grids | ✅ | 🟡 | Template arrangement only
Elements | Images / photos | ✅ | ✅ | PNG, JPEG, WebP, GIF
Elements | Reactions / emojis | ✅ | ✅ | Twelve styles
Elements | PDF import | ✅ | ⬜ | Local PDF renderer needed
Elements | PowerPoint import | ✅ | ⬜ | Local slide renderer needed
Selection | Box multi-select | ✅ | ✅ | Marquee
Selection | Lasso select | ✅ | ✅ | Freeform polygon selection
Editing | Move / resize | ✅ | ✅ | Object-specific controls
Editing | Rotate | ✅ | 🟡 | Handle + inspector, some resize limits
Editing | Group / ungroup | ✅ | ✅ | Persists in JSON backup
Editing | Align multiple objects | ✅ | ✅ | Six align modes
Editing | Copy/paste/duplicate | ✅ | ✅ | In-app object clipboard
Editing | Bring front/send back | ✅ | ✅ | Supported
Editing | Undo/redo | ✅ | ✅ | 80 object snapshots
Templates | Brainstorm/Kanban/SWOT/mindmap | ✅ | ✅ | Plus retro and lesson
Templates | Large template library | ✅ | 🟡 | Six starters
Storage | Multiple boards/gallery/search | ✅ | ✅ | Local IndexedDB
Storage | Autosave | ✅ | 🟡 | Built; Windows persistence smoke needed
Storage | Editable import/export backup | Other format | ✅ | .bosha.json
Storage | Import Microsoft .whiteboard | Native | ⬜ | Not compatible
Export | PNG | ✅ | ✅ | Local render
Export | SVG vector | Varies | ✅ | Local render
Export | PDF | Varies | ✅ | Single flattened A4 page
Collaboration | Live multi-editor | ✅ | ⬜ | No sync backend
Collaboration | Live cursors | ✅ | ⬜ | Not built
Collaboration | Invite links/roles | ✅ | ⬜ | Not built
Collaboration | Teams integration | ✅ | ➖ | Independent product
Privacy | No telemetry | Varies | ✅ | No analytics/remote requests
Release | Signed stable Windows binary | Retired | ⬜ | Windows packaging still pending
Release | Portable EXE/NSIS build scripts | N/A | 🟡 | CI definition present
Quality | Automated model tests | Internal | ✅ | 15 checks passed; advanced isolated browser smoke tested

## Work completed in v0.2

Added lasso, grouping, alignment, snap, rotation, visual ruler, pressure-aware ink strokes, partial eraser, two-headed arrows, single-page PDF export and 9 additional model/PDF tests.

## Not finished

Native PDF and PowerPoint import; collaborative live editing/cursors/roles; accurate hardware-pen and Windows persistence verification; Android app; a tested and signed Windows release.

## Sources

- https://support.microsoft.com/en-us/whiteboard/retirement-standalone-microsoft-whiteboard-apps
- https://support.microsoft.com/en-us/whiteboard/draw-and-ink-in-whiteboard
- https://support.microsoft.com/en-us/whiteboard/whiteboard-create-panel
- https://support.microsoft.com/en-us/whiteboard/use-ruler-and-enhance-shapes-in-whiteboard
- https://support.microsoft.com/en-us/whiteboard/use-mouse-keyboard-and-pen-in-whiteboard
