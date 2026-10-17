# Changelog

## 0.2.0 — 2026-10-17

- Freeform lasso and persistent group/ungroup.
- Alignment commands and optional snap-to-grid.
- Rotate handle and inspector angle, including rotation-aware hit testing.
- Pressure-responsive strokes, non-destructive-in-place partial eraser, double-headed arrows.
- Move/rotate ruler overlay.
- One-page flattened PDF document export, no runtime PDF library.
- Input validation updates, 9 new test cases and advanced headless browser smoke test.

## 0.1.0 — 2026-10-17

- Initial SVG infinite-canvas board and Electron Windows project.
- Local gallery, templates, notes, drawing, image insertion and JSON/PNG/SVG export.

## v0.3.0 — Inklume beta source (2026-10-17)

- Rebrand to **Inklume**, new expressive app icon (ICO, PNG, favicon) and Windows metadata. Old app ID and profile preserved for upgrade compatibility.
- Add native atomic on-disk board snapshots, previous-revision `.bak` recovery, dual IndexedDB compatibility and defensive data bounds.
- Integrate local PDF.js rendering into board-image import (up to 12 pages / 20MB) and drag/drop images/PDF; installed dependency and Windows PDF tests pending.
- Add conservative **Convert ink to shape** action for strokes, rectangles and ellipses.
- Add opt-in updater UI, `electron-updater` version-check/download/restart and progress, GitHub feed and NSIS `true` blockmap configuration for future differential updates.
- Increase unit tests from 15 to 21, including recovery, update flow and ink recognizer. Windows build and two-release delta integration test still pending.
