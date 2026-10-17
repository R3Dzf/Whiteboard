# Inklume Native — interaction and shell rebuild

## What changed

- Replaced the minimal preview layout with a desktop workspace: searchable-in-future board list, title, tools, canvas, property editor, status, zoom and fit controls.
- Added multi-selection, rectangle selection, duplicate, delete, copy-safe edit transactions and geometry-aware hit testing.
- Resampled stylus input so fast pointer events cannot leave long visual gaps. Retained pressure and tilt when moving or erasing ink.
- Added partial stroke erasing with independently editable fragments.
- Added real raster image display with capped local decode cache; users can import PNG, JPG and WEBP directly.
- Added native arrows and lines, board background toggles, tool shortcuts and board creation/opening.
- Added editable JSON board export and immediate persistence of newly created boards.
- Added the branded Windows EXE icon.
- Grid rendering has a bounded workload regardless of zoom or display size. Spatial culling remains enabled.

## Quality status

These are structural improvements, **not a claim of finished product quality**. CI can compile on Windows and run deterministic core tests, but does not measure pointer latency, rendering FPS or the subjective pen feel. Test on a real Windows device before tagging a stable release.

## Next critical milestones

1. Profiling and retained stroke geometry caching; verify 60+ FPS on large canvases and measure end-to-end pen latency.
2. Proper Lasso, ink smoothing and shape recognition, object handles, touch gestures, palm rejection, accessible keyboard commands.
3. PDF and image imports with robust high-resolution scaling and safe file management.
4. Multi-board management (rename, delete with confirmation, export), packaging, update signing and delta updates.
5. Collaboration after file integrity and rendering quality are proven.

## Migration safety

Legacy web and Electron branches are not modified. Native app data lives in `%APPDATA%/Inklume/Boards`. Import `.bosha.json` exports explicitly via Import; never overwrite legacy data or pretend that all unsupported legacy objects render correctly.