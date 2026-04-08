# Shooter project memory

## What this repo is
- Modern C#/.NET 10 cross-platform-minded 3D engine/editor PoC, currently focused on the **map editor**.
- Repo path: `C:\github\shooter`
- GitHub: `adriebaselmans/shooter`
- Current branch baseline: `main`
- Stack: WPF editor app + Silk.NET OpenGL renderer + command-based scene/core layers.

## Product intent
- Build a professional-feeling 3D map editor inspired by UnrealEd / SketchUp workflows.
- First milestone is an editor that lets the user draw rooms and place objects using primitive brushes.
- The level/map format must stay extensible for geometry, lights, spawn points, and future gameplay data.

## Important coordinate contract
- **Left-handed, Y-up**
- `+X = right`
- `+Y = up`
- `+Z = forward`
- View mappings:
  - **Top** = X right, Z up
  - **Front** = X right, Y up
  - **Side** = Z right, Y up
- 3D guide must match the same contract with **red X / green Y / blue Z**.

## Architecture decisions kept stable
- `MapEditor.App` owns editor shell, tools, viewmodels, input routing, logging, and viewport orchestration.
- `MapEditor.Core` owns scene entities and command-based mutations.
- `MapEditor.Rendering` is rendering/math/infrastructure and should stay read-only with respect to domain mutations.
- `MapEditor.Formats` owns map serialization and file I/O boundaries.
- Keep one mutation path through scene commands so undo/redo stays coherent.

## Major implemented history
- Built the initial editor shell with 3 orthographic viewports plus one 3D viewport.
- Completed WI-10 interaction loop: tool contracts, selection state, viewport input routing, select/create/move behavior, UI synchronization, and hardening.
- Fixed a major runtime issue where the GL viewport host used a `STATIC` child window and mouse input never reached the tool path.
- Added visible orthographic grids and a visible 3D floor/orientation guide.
- Made realtime brush creation/movement visible in both orthographic and perspective rendering.
- Removed the dedicated resize-mode workflow; orthographic resize now happens from selection handles directly.
- Fixed the 3D mouse wheel crash caused by unsafe 64-bit wheel message decoding.
- Added per-session logging plus global managed exception logging to session log files.
- Restored normal desktop shell behavior: movable window, default maximize/close behavior, working menu/toolbar/file flows.
- Fixed open/save/shortcut behavior; a key gotcha was that MVVM Toolkit generates `OpenFileCommand` / `SaveFileCommand`, not `OpenFileAsyncCommand` / `SaveFileAsyncCommand`.
- Added keyboard routing from the native GL child window back into WPF command handling so shortcuts still work while a viewport has focus.
- Implemented editor-local copy/paste, undo/redo, viewport maximize/restore, and working properties editing.
- Replaced the primitive dropdown with explicit primitive buttons: **Box, Cylinder, Cone, Wedge**.
- Primitive create buttons are **mutually exclusive single-use toggles**:
  - activating one enters create mode for that primitive
  - clicking the active one again returns to select mode
  - completing one brush creation auto-returns to select mode
- Unified brush colors through a shared `BrushColorPalette`.
- Fixed perspective brushes appearing like transparent black in low light by preserving a minimum editor tint in the 3D solid shader.
- Realigned all viewport interaction/rendering/camera/grid/resize math to the explicit axis contract above.
- Fixed the final Top(XZ) vs 3D mismatch by correcting the perspective floor-guide X/Z colors so Top and 3D now agree.

## Current user-facing behavior that should stay true
- Window behaves like a normal desktop app.
- Orthographic views show logical projections that match the 3D world axes.
- 3D view shows readable translucent brush colors matching the orthographic color language.
- Selected brushes can be moved by direct manipulation in orthographic editing.
- Brush creation is visible in realtime in orthographic and 3D views.
- Open/save/menu/toolbar/shortcut flows work.
- Ctrl-based shortcuts work even when a GL viewport has focus.
- Primitive creation uses toolbar buttons rather than a dropdown.

## Key files to read first next time
- `memory.md`
- `doc_templates\requirements\current.yaml`
- `doc_templates\design\current.yaml`
- `src\MapEditor.App\MainWindow.xaml`
- `src\MapEditor.App\ViewModels\MainViewModel.cs`
- `src\MapEditor.App\Views\ViewportPanel.xaml.cs`
- `src\MapEditor.App\Views\GlViewportHost.cs`
- `src\MapEditor.App\Tools\CreateBrushTool.cs`
- `src\MapEditor.Rendering\Renderers\OrthographicViewportRenderer.cs`
- `src\MapEditor.Rendering\Renderers\PerspectiveViewportRenderer.cs`
- `src\MapEditor.Rendering\Infrastructure\GridGeometryBuilder.cs`
- `src\MapEditor.Rendering\Infrastructure\ResizeHandleMath.cs`
- `src\MapEditor.Rendering\Cameras\Cameras.cs`

## Validation and workflow notes
- Fast PoC loop is preferred right now.
- User explicitly asked to **skip acceptance-test automation and DoD phases** until they say otherwise.
- Successful validation baseline at handoff was **57/57 tests passing**.
- Common validation command: `dotnet test shooter.slnx -nologo -v:minimal`
- Do not run `dotnet build` and `dotnet test` in parallel on this repo; it can trigger transient file-lock issues.
- Expect pre-existing XML doc warnings; they were not the active focus during this loop.

## Known gotchas worth remembering
- WPF + native `HwndHost` means keyboard shortcuts do not automatically route when the GL child HWND owns focus; the explicit shortcut router is required.
- Side/Top viewport bugs were easy to misdiagnose as math issues when the real problem was sometimes **axis color mismatch**.
- The perspective floor guide must use the same Top(XZ) color meaning:
  - **X = red**
  - **Z = blue**
- The perspective shader should not rely purely on scene lighting for editor brush visibility.

## Best next-step areas if work resumes
- Runtime verification/polish of viewport interaction feel in the executable.
- More editor UX polish: axis labels, clearer manipulators, better toolbar spacing/icons.
- Continued map-format and engine-side iteration once editor basics feel stable.
