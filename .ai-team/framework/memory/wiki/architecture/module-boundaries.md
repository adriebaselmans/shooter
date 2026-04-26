---
id: module-boundaries
cat: architecture
rev: 1
created: 2026-04-26T12:00:00Z
updated: 2026-04-26T12:00:00Z
by: coordinator
tags: [modules, boundaries, wpf, rendering, scene, formats]
summary: "Module ownership rules and key files for the shooter map editor."
status: active
---

## Module ownership
- `MapEditor.App` — editor shell, tools, viewmodels, input routing, logging, viewport orchestration.
- `MapEditor.Core` — scene entities, command-based mutations. One mutation path → undo/redo coherent.
- `MapEditor.Rendering` — rendering/math/infrastructure. **Read-only** w.r.t. domain mutations.
- `MapEditor.Formats` — map serialization and file I/O boundaries.

## Key files (read first)
- `memory.md` (migrated to wiki)
- `doc_templates/requirements/current.yaml`
- `doc_templates/design/current.yaml`
- `src/MapEditor.App/MainWindow.xaml`
- `src/MapEditor.App/ViewModels/MainViewModel.cs`
- `src/MapEditor.App/Views/ViewportPanel.xaml.cs`
- `src/MapEditor.App/Views/GlViewportHost.cs`
- `src/MapEditor.App/Tools/CreateBrushTool.cs`
- `src/MapEditor.Rendering/Renderers/OrthographicViewportRenderer.cs`
- `src/MapEditor.Rendering/Renderers/PerspectiveViewportRenderer.cs`
- `src/MapEditor.Rendering/Infrastructure/GridGeometryBuilder.cs`
- `src/MapEditor.Rendering/Infrastructure/ResizeHandleMath.cs`
- `src/MapEditor.Rendering/Cameras/Cameras.cs`

## open
- None.
