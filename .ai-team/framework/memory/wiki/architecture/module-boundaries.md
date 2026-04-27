---
id: module-boundaries
cat: architecture
rev: 2
created: 2026-04-26T12:00:00Z
updated: 2026-04-26T18:45:00Z
by: coordinator
tags: [modules, boundaries, avalonia, wpf, rendering, scene, formats]
summary: "Module ownership rules and active startup paths for the shooter map editor."
status: active
---

## Module ownership
- `MapEditor.App.Avalonia` — active editor shell, desktop startup, Avalonia viewport host, Avalonia dialogs.
- `MapEditor.App` — shared editor logic plus legacy WPF shell/reference implementation.
- `MapEditor.Core` — scene entities, command-based mutations. One mutation path → undo/redo coherent.
- `MapEditor.Rendering` — rendering/math/infrastructure. **Read-only** w.r.t. domain mutations.
- `MapEditor.Formats` — map serialization and file I/O boundaries.

## Key files (read first)
- `doc_templates/requirements/current.yaml`
- `doc_templates/design/current.yaml`
- `src/MapEditor.App.Avalonia/MainWindow.axaml`
- `src/MapEditor.App.Avalonia/Views/ViewportPanel.axaml.cs`
- `src/MapEditor.App.Avalonia/Views/AvaloniaGlViewportHost.cs`
- `src/MapEditor.App/ViewModels/MainViewModel.cs`
- `src/MapEditor.App/Tools/EditorInputTypes.cs`
- `src/MapEditor.App/Services/EditorDialogServices.cs`
- `src/MapEditor.App/Tools/CreateBrushTool.cs`
- `src/MapEditor.Rendering/Renderers/OrthographicViewportRenderer.cs`
- `src/MapEditor.Rendering/Renderers/PerspectiveViewportRenderer.cs`

## active path
- Startup target: `shooter.slnx` → `MapEditor.App.Avalonia`.
- Shared editor semantics remain in linked code from `MapEditor.App`.
- WPF-specific shortcut/dialog adapters stay outside the shared path.

## open
- None.
