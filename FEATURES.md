# Feature parity tracker — Bosha Whiteboard vs. Microsoft Whiteboard

> Updated: 17 October 2026. **Implemented** means present in the source and exercised where possible; it does **not** mean all edge cases or operating systems are certified. **Partial** means a usable but not fully equivalent implementation. **Planned** means no working implementation yet. The Microsoft column reflects Microsoft's published Whiteboard documentation rather than a guarantee for every account/license.
>
> Microsoft retired its standalone Windows, Android and iOS apps on **16 October 2026**. Its web and Teams experiences continue for eligible work/school accounts. https://support.microsoft.com/en-us/whiteboard/retirement-standalone-microsoft-whiteboard-apps

| Area | Capability | Microsoft Whiteboard | Bosha Whiteboard | Notes / outstanding work |
|---|---|:---:|:---:|---|
| Platform | Standalone Windows desktop app | Previously | **Implemented** (Electron source; CI packaging pending) | Windows build produced by GitHub Actions once install succeeds; unsigned. |
| Platform | Web/browser interface | Yes | **Implemented** | Open index.html in a browser; no cloud required. |
| Platform | Android native app | Previously | **Planned** | Touch-enabled browser UI exists, not an Android APK. |
| Platform | iOS native app | Previously | **Planned** | Touch-enabled browser UI exists, not an iOS app. |
| Accounts | Work/school account sign-in | Yes | **Not planned** | An intentional offline-first alternative. |
| Accounts | No sign-in needed | No for sharing | **Implemented** | Fully local. |
| Canvas | Unlimited panning | Yes | **Implemented** | Coordinates extend freely beyond the screen. |
| Canvas | Mouse wheel zoom | Yes | **Implemented** | Zoom anchored to pointer. |
| Canvas | Fit all / reset zoom | Yes | **Implemented** | Fit button, 100% button. |
| Canvas | Touch drawing, touch pinch | Yes | **Partial** | Pointer Events support; needs hardware validation on Windows touchscreens. |
| Canvas | Mouse / pen input | Yes | **Implemented** | Uses pointer events, no special driver. |
| Canvas | Dots, grid, ruled, plain backgrounds | Yes | **Implemented** | Per-board setting. |
| Canvas | Dark canvas | Yes | **Implemented** | Per-board setting. |
| Canvas | Ruler / angle adjustment | Yes | **Planned** | Precise measurement tool. |
| Ink | Freehand pen and thickness | Yes | **Implemented** | Color and adjustable width. |
| Ink | Highlighter | Yes | **Implemented** | Semi-transparent strokes. |
| Ink | Ink eraser | Yes | **Partial** | Deletes whole strokes; segment eraser planned. |
| Ink | Single arrow pen | Yes | **Partial** | Arrow shapes are implemented; no dedicated ink-arrow mode. |
| Ink | Double arrow pen | Yes | **Planned** | Dual endpoints. |
| Ink | Hold Shift for straight ink | Yes | **Implemented** | While drawing a stroke. |
| Ink | Temporary laser pointer | Yes | **Implemented** | Temporary cursor indicator, not saved. |
| Ink | Smart shape recognition | Yes | **Planned** | Shape tools already produce precise geometry. |
| Ink | 15 pens / 15 highlighters | Yes | **Partial** | 10 preset colors + native custom picker. |
| Objects | Sticky notes with colored backgrounds | Yes | **Implemented** | Create, move, resize, edit and recolor. |
| Objects | Note grids | Yes | **Partial** | Gridded note groups available through templates, not linked note containers. |
| Objects | Text boxes | Yes | **Implemented** | Editable text and font sizes. |
| Objects | Shapes | Yes | **Implemented** | Rectangles, ellipses, diamonds, lines and arrows. |
| Objects | Reactions | Yes | **Implemented** | 12 Unicode reaction styles. |
| Objects | Images / photographs | Yes | **Implemented** | PNG, JPEG, WebP, GIF: file input and clipboard. 4 MB per image. |
| Objects | Import PDF pages | Yes | **Planned** | Requires local PDF renderer. |
| Objects | Import PowerPoint pages | Yes | **Planned** | Requires file converter. |
| Objects | YouTube / links / rich embeds | Variant | **Planned** | Offline-oriented options under evaluation. |
| Editing | Object select, drag, resize | Yes | **Implemented** | Resize using lower-right handle. |
| Editing | Multiple selection / marquee | Yes | **Implemented** | Drag empty canvas / Shift+select. |
| Editing | Freeform lasso tool | Yes | **Planned** | Marquee currently substitutes. |
| Editing | Duplicate, delete, copy/paste | Yes | **Implemented** | In-app object clipboard and shortcuts. |
| Editing | Bring forward / send backward | Yes | **Partial** | Bring to front and send to back, not step-by-step. |
| Editing | Object snapping / alignment guides | Yes | **Planned** | Intended for later phase. |
| Editing | Rotation of arbitrary objects | Yes | **Planned** | Move/resize work; rotation pending. |
| Editing | Group / ungroup objects | Yes | **Planned** | Multi-select currently supported. |
| Editing | Undo / redo | Yes | **Implemented** | Up to 80 document snapshots per open session. |
| Templates | Brainstorming | Yes | **Implemented** | One editable layout. |
| Templates | Kanban / project planning | Yes | **Implemented** | One editable layout. |
| Templates | SWOT analysis | Yes | **Implemented** | Editable layout. |
| Templates | Mind mapping | Yes | **Implemented** | Editable starter nodes and connections. |
| Templates | Retrospective | Yes | **Implemented** | Editable layout. |
| Templates | Lesson plan | Yes | **Implemented** | Editable layout. |
| Templates | Large official category library | Yes | **Partial** | Six built-in templates; expanding catalog pending. |
| Templates | Save and share custom templates | Yes | **Planned** | Exporting whole boards as backups works. |
| Library | Multi-board gallery | Yes | **Implemented** | Search, new, rename, duplicate, delete. |
| Library | Autosave | Yes | **Implemented** | IndexedDB on device with visible status. |
| Library | Offline access | Varies | **Implemented** | No network calls in application UI. |
| Library | Import/export editable format | Limited | **Implemented** | Bosha-specific `.bosha.json`; not a Microsoft `.whiteboard` reader. |
| Library | Import Microsoft `.whiteboard` documents | No native general interchange | **Planned** | Reverse engineering / migration separate effort. |
| Export | PNG export | Yes | **Implemented** | Renders the board into a PNG locally. |
| Export | SVG vector export | Not documented as standard | **Implemented** | Scalable vector format. |
| Export | PDF export | Not standard direct feature | **Planned** | Dedicated PDF export pending. |
| Collaboration | Real-time multi-user editing | Yes | **Planned** | Requires opt-in sync/backend or LAN session protocol. |
| Collaboration | Live collaborator cursors | Yes | **Planned** | Part of collaboration phase. |
| Collaboration | Invite links / roles / view-only | Yes | **Planned** | Permission system pending. |
| Collaboration | Microsoft Teams integration | Yes | **Not planned** | Independent product, not Microsoft-connected. |
| AI | Copilot workflows | Eligible subscriptions | **Not implemented** | Optional offline intelligence could be added later. |
| Accessibility | Keyboard shortcuts | Yes | **Implemented** | V/H/P/M/E/L/N/T/R/O/D/I/A, Ctrl+Z/Y/C/V/D/S. |
| Accessibility | Screen reader support | Yes | **Partial** | Buttons labeled; full canvas object navigation pending. |
| Privacy | No tracking, no telemetry | Varies | **Implemented** | No analytics SDK or remote dependencies. |
| Backup | Multi-device synchronization | Microsoft 365 cloud | **Planned** | Local transfer / user-selected transport phase. |
| Release | Installer / portable EXE | Previously | **Partial** | Packaging workflow provided; CI build not yet verified on GitHub. |
| Testing | Automated tests | Internal | **Partial** | Node model tests and local headless UI smoke test; more coverage needed. |

## Implementation milestones

- **Phase 1 (in this commit)**: Windows desktop scaffold, local storage, drawing engine, object tools, boards library, templates, export, keyboard, test harness.
- **Phase 2**: Improve drawing fidelity, pressure sensitivity, reliable pen erasing, snapping, rotation, lasso, grouping, ruler, undo/view history.
- **Phase 3**: PDF import/export, improved documents, custom templates, larger objects, print support.
- **Phase 4**: Secure LAN collaboration, permissions, live pointers and conflict resolution, opt-in sync.
- **Phase 5**: Android packaging, accessibility audit, performance/stress tests and signed releases.

## Microsoft documentation (feature references)

- [Standalone app retirement](https://support.microsoft.com/en-us/whiteboard/retirement-standalone-microsoft-whiteboard-apps)
- [Draw and ink](https://support.microsoft.com/en-us/whiteboard/draw-and-ink-in-whiteboard)
- [Whiteboard Create panel](https://support.microsoft.com/en-us/whiteboard/whiteboard-create-panel)
- [Select shapes and reactions](https://support.microsoft.com/en-us/whiteboard/select-shapes-and-reactions-in-whiteboard)
- [Notes and note grids](https://support.microsoft.com/en-us/whiteboard/add-notes-and-note-grids-in-whiteboard)
- [Templates](https://support.microsoft.com/en-us/whiteboard/insert-templates-in-whiteboard)
- [Collaboration](https://support.microsoft.com/en-us/whiteboard/get-started-with-whiteboard-collaboration)
- [Settings](https://support.microsoft.com/en-us/whiteboard/whiteboard-settings-menu)
- [Export](https://support.microsoft.com/en-us/whiteboard/export-a-whiteboard)
- [Shortcuts](https://support.microsoft.com/en-us/whiteboard/whiteboard-keyboard-shortcuts)