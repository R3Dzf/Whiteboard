# Native migration tracker (updated 2026-10-17)

Legend: ✅ Implemented in source · ◐ limited/needs validation · ⬜ not implemented.

| Priority | Function | Native status | Notes |
|---|---|---|---|
| P0 | C# + Avalonia project skeleton | ✅ | .NET 10, Avalonia 12.1.2 |
| P0 | Single custom-drawn canvas | ✅ | Skia-backed Avalonia DrawingContext; no DOM |
| P0 | Visible-item spatial index | ✅ | Bounds-cell hash, no full scene DOM rebuild |
| P0 | Pen/pressure samples | ◐ | Stored and rendered; test real pen hardware |
| P0 | Pan & pointer-centered zoom | ✅ | 10%-800% zoom |
| P0 | Accurate stroke hit detection | ✅ | Point-to-segment, not just bounding boxes |
| P0 | Rectangle / ellipse / note / text | ◐ | Basic styles and rendering only |
| P0 | Selection and move | ✅ | Single-object only |
| P0 | Undo and redo | ✅ | Per-gesture history snapshots; optimize memory later |
| P0 | Autosave and recovery backup | ◐ | Atomic JSON save and .bak, no write-ahead log |
| P0 | Old Bosha JSON import | ◐ | Ink/shapes/notes; images preserved, not displayed |
| P0 | Windows build/test CI | ◐ | CI config exists; release only after verified |
| P1 | Frame-time profiling benchmarks | ⬜ | Measure actual FPS, latency, RAM |
| P1 | Coalesced pointer samples & smoothing | ⬜ | Required for premium ink |
| P1 | Palm rejection & touch gestures | ⬜ | Real device testing required |
| P1 | Partial eraser | ⬜ | Currently whole-object eraser |
| P1 | Multi-selection / lasso / grouping | ⬜ | |
| P1 | SQLite + media asset store | ⬜ | JSON is temporary foundation |
| P1 | PDF import and annotation | ⬜ | |
| P1 | SVG / PNG / PDF export | ⬜ | |
| P1 | Image rendering and embedded-font handling | ⬜ | |
| P2 | Gallery / templates / folders | ⬜ | |
| P2 | Multi-user live collaboration | ⬜ | |
| P2 | Android app | ⬜ | |
| P2 | Signed Windows installer | ⬜ | |
| P2 | Differential updates & rollback | ⬜ | Carry over intent, different packaging stack |

**Principle:** A checked box means code exists, not proven UX quality. Only mark production ready after real Windows pen/tablet performance tests and migration recovery tests.
