# Inklume Native

A native Windows whiteboard prototype built with Avalonia UI, C# and the Skia-backed Avalonia renderer. Inklume replaces the old SVG/DOM-per-movement approach with a single drawing visual and a spatial index.

## Current capabilities

Infinite canvas, pen and marker pressure samples, partial eraser, rectangles, ellipses, lines, arrows, sticky notes and text, selectable and movable objects, multi-selection, duplicating and deleting, zoom/pan/fit, light and dark canvas backgrounds, a multi-board sidebar, editable titles, raster image import/display, undo/redo, resilient local board storage and explicit import of legacy JSON exports.

This is an **engineering preview**, not a stable release. Running tests does not demonstrate real-world drawing latency or flawless interaction on every screen/digitizer.

## Windows build

```powershell
dotnet restore native/Inklume.App/Inklume.App.csproj
dotnet run --project native/Inklume.App/Inklume.App.csproj
dotnet run --project native/Inklume.SmokeTests/Inklume.SmokeTests.csproj -c Release
dotnet publish native/Inklume.App/Inklume.App.csproj -c Release -r win-x64 --self-contained true
```

Source: [`native/Inklume.App`](native/Inklume.App). Core model and geometry: [`native/Inklume.Core`](native/Inklume.Core). All dependencies are open source. This project does not require any paid APIs.

## Limitations

PDF import, realtime collaboration, full precision lasso, genuine signed installers and delta upgrades are not implemented in the native app. Do not discard your older board files. See [PHASE2.md](PHASE2.md) for work done and the release gate.