---
id: implementation-history
cat: context
rev: 2
created: 2026-04-26T12:00:00Z
updated: 2026-04-26T18:45:00Z
by: coordinator
tags: [history, implemented, editor, viewports, brushes, gl, wpf, avalonia]
summary: "Completed implementation milestones for the shooter map editor and shell migration."
status: active
---

## Completed milestones
- Initial editor shell: 3 orthographic viewports + 1 3D viewport.
- WI-10 interaction loop: tool contracts, selection state, viewport input routing, select/create/move behavior, UI sync, hardening.
- Fixed GL viewport host: was using `STATIC` child window — mouse input never reached tool path.
- Visible orthographic grids + 3D floor/orientation guide.
- Realtime brush creation/movement in orthographic and perspective rendering.
- Removed dedicated resize-mode workflow; orthographic resize from selection handles directly.
- Fixed 3D mouse wheel crash (unsafe 64-bit wheel message decoding).
- Per-session logging + global managed exception logging.
- Restored normal desktop shell behavior (movable window, maximize/close, menu/toolbar/file flows).
- Fixed open/save/shortcut behavior (MVVM Toolkit command naming gotcha).
- Keyboard routing from native GL child window back into WPF command handling.
- Editor-local copy/paste, undo/redo, viewport maximize/restore, properties editing.
- Replaced primitive dropdown with explicit buttons: Box, Cylinder, Cone, Wedge.
- Primitive buttons as mutually exclusive single-use toggles.
- Unified brush colors via shared `BrushColorPalette`.
- Fixed perspective brushes appearing black/transparent in low light (minimum editor tint in 3D solid shader).
- Realigned all viewport interaction/rendering/camera/grid/resize math to axis contract.
- Fixed Top(XZ) vs 3D mismatch by correcting perspective floor-guide X/Z colors.
- Added UI-neutral input contracts: `ViewportPoint`, `ViewportVector`, `EditorKey`, `EditorModifierKeys`.
- Moved file/message dialogs behind shared service interfaces for non-WPF shells.
- Added `MapEditor.App.Avalonia` as the active desktop shell.
- Ported main shell layout, four-viewport composition, and shortcut routing to Avalonia.
- Replaced Avalonia Windows-native host approach with cross-platform `OpenGlControlBase` host.
- Active solution now builds around Avalonia app + core + formats + rendering on macOS.

## open
- None.
