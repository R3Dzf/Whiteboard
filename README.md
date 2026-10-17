# Inklume Native Rewrite

**Avalonia 12 / C# / .NET 10 — Phase 1 implementation**

This branch develops the next-generation desktop engine in `native/` while the existing Electron v0.3.0 remains untouched on `main`.

## Why rewrite?

The earlier editor replaced the entire SVG element subtree on every pen move. The native preview uses a single Avalonia-rendered control (Skia renderer) and queries a spatial hash for visible objects. This eliminates per-object DOM reconstruction. It does **not** by itself prove a particular FPS or input latency; physical-device profiling remains necessary.

## Implemented in this native prototype

- C# board schema with ink samples including pressure and tilt.
- Skia-backed Avalonia custom-drawn canvas: strokes, rects, ellipses, text, notes, lines.
- Pen, marker, shape, selection/move, hand/pan and eraser tools.
- Pointer-centric zoom anchored at cursor, pan, basic selection and hit testing.
- Spatial hashing to reduce candidate objects for rendering and selection.
- Atomic JSON snapshots with last-good backups, autosave debounce, history undo/redo.
- Migration of supported shapes and ink from old `.bosha.json` / Electron board exports. Legacy embedded image data is **retained** in documents but not yet rendered.
- Automated core smoke tests and Windows CI compile/publish workflow.

## Run from source

Install the .NET 10 SDK. Avalonia and its NuGet dependencies are restored automatically.

```powershell
cd native
dotnet run --project Inklume.App/Inklume.App.csproj
```

Run the tests:

```powershell
dotnet run --project native/Inklume.SmokeTests/Inklume.SmokeTests.csproj
```

The app stores native board files under `%APPDATA%\Inklume\Boards`. **It does not delete, overwrite, or auto-convert the Electron app's old data.** Use Import and select a backup file; imported boards get new IDs. Back up any important boards before trying prerelease software.

## Known gaps

This is an architectural preview, not a replacement for the older app: native PDF import, images on canvas, multiple-board gallery, rich formatting, lasso, multi-touch pinch, advanced partial eraser, true crash recovery journal, SQLite, delta update installer, collaboration, and performance profiling are not yet done. A Win-x64 build artifact is **not** a signed installer.

See [`MIGRATION.md`](MIGRATION.md) for the development checklist.
