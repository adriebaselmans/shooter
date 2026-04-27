---
id: viewport-hosting
cat: architecture
rev: 2
created: 2026-04-26T18:45:00Z
updated: 2026-04-26T19:20:00Z
by: coordinator
tags: [opengl, avalonia, silk-net, viewports, input, overlay]
summary: "Avalonia viewports render with OpenGlControlBase and route input through an overlay."
refs: [decisions/avalonia-primary-editor, context/current-behavior]
status: active
---

## host
- `MapEditor.App.Avalonia/Views/AvaloniaGlViewportHost.cs`
- Base type: `Avalonia.OpenGL.Controls.OpenGlControlBase`
- GL binding: `Silk.NET.OpenGL.GL.GetApi(gl.GetProcAddress)`
- Rendering only; no editor input logic.

## input
- `ViewportPanel.axaml` overlays transparent `InputSurface` above GL control.
- Overlay handles pointer down/up/move/wheel/leave.
- Overlay focuses + captures pointer on press; releases on up.
- Window tunnel `KeyDown` routes shortcuts through `EditorShortcutRouter`.

## responsibilities
- Track framebuffer pixel size from Avalonia scaling.
- Raise `RenderFrame` for renderer-owned drawing.
- Convert overlay input to neutral `ViewportPointerEvent`.

## invariants
- Viewport pixel dimensions are physical pixels.
- Input contracts are neutral: `ViewportPoint`, `EditorKey`, `EditorModifierKeys`.
- Camera/tool behavior stays in `ViewportPanel`, not the host.

## open
- Confirm long-run shared-resource behavior across all four viewports under heavier scenes.