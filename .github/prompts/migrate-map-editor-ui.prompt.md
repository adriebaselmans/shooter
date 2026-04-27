---
description: "Implement the map editor UI migration from WPF to Avalonia while preserving Silk.NET OpenGL viewports and editor input behavior"
name: "Migrate Map Editor UI To Avalonia"
argument-hint: "Migrate the current WPF editor shell to Avalonia with viewport/input parity"
agent: "agent"
---

# Task

Implement a migration of the shooter map editor UI from WPF to a modern Avalonia-based desktop UI while preserving the existing editor behavior, Silk.NET OpenGL rendering, multi-viewport layout, mouse/keyboard forwarding, and command-based scene workflow.

Read and follow [AGENTS.md](../../AGENTS.md) before making changes. Use the repository's staged software delivery flow and validate progressively.

# Current Architecture To Preserve

The existing editor is a WPF application in `src/MapEditor.App`:

- `src/MapEditor.App/MainWindow.xaml` defines the desktop editor shell: menu, toolbar, left outliner, central four-viewport grid, right properties/texture panels, and status bar.
- `src/MapEditor.App/MainWindow.xaml.cs` wires the view model, initializes `GlContextManager`, attaches Top/Front/Side/Perspective viewport renderers, handles global shortcuts, and toggles viewport maximize/restore.
- `src/MapEditor.App/Views/ViewportPanel.xaml` and `src/MapEditor.App/Views/ViewportPanel.xaml.cs` wrap each viewport, connect renderers, build `ToolContext`, route pointer/key input to tools, implement camera navigation, hit testing, face selection, cursor status, and maximize UI.
- `src/MapEditor.App/Views/GlViewportHost.cs` is the current WPF-specific viewport host. It derives from `HwndHost`, creates a Win32 child HWND, creates a shared WGL context through `GlContextManager`, renders from `CompositionTarget.Rendering`, swaps buffers, and forwards native mouse/keyboard messages to managed events.
- `src/MapEditor.Rendering/Infrastructure/GlContextManager.cs` owns the shared WGL resource context and creates shared viewport contexts. Keep this WGL/Silk.NET strategy for the first migration pass.
- `src/MapEditor.App/Tools/ToolContracts.cs` defines the viewport input/tool contract. It currently depends on WPF `System.Windows.Point` and `System.Windows.Input.ModifierKeys`; that must be made UI-framework-neutral.
- `src/MapEditor.App/EditorShortcutRouter.cs` explicitly routes shortcuts because the focused native OpenGL child window bypasses normal WPF command routing. Preserve this explicit routing model in Avalonia.
- `src/MapEditor.App/ViewModels/MainViewModel.cs` uses CommunityToolkit.Mvvm and owns file commands, tool selection, undo/redo, dirty state, scene updates, texture selection, and command notifications. Preserve the MVVM Toolkit model.

Do not rewrite `MapEditor.Core`, `MapEditor.Formats`, or `MapEditor.Rendering` except for narrow adapter seams needed by the UI migration. Preserve the command-based scene mutation model and all existing map file compatibility.

# Framework Decision

Use Avalonia 11.x as the target UI framework.

Reasons:

- Closest modern XAML/MVVM migration path from WPF while avoiding another WPF-based skin.
- Works well with CommunityToolkit.Mvvm and dependency injection.
- Provides native control hosting and pointer/key APIs suitable for a custom viewport host.
- Gives a path to reuse the existing Win32 child HWND plus WGL/Silk.NET integration first, then optionally evolve to Avalonia's OpenGL control APIs later.
- Keeps future cross-platform optionality without forcing a rendering rewrite in this migration.

Do not choose WinUI 3 for this migration unless Avalonia's native hosting proves impossible in this repository. WinUI 3 is Windows-native, but it is less direct for the current WPF-style XAML and still needs bespoke native viewport interop.

# Migration Strategy

Use an incremental adapter migration. Keep the WPF app buildable until Avalonia reaches parity, then make the Avalonia app the primary startup application.

## Phase 0: Baseline

1. Run the cheapest current validation first, such as `dotnet build` and relevant tests.
2. Record failures that pre-exist the migration and do not fix unrelated issues.
3. Inspect current UI behavior manually where possible: four viewports, maximize/restore, camera navigation, brush creation, selection, undo/redo, copy/paste, texture panel, open/save.

## Phase 1: Extract UI-Neutral Editor Contracts

Refactor WPF-specific interaction types out of the shared editor/tool layer:

1. Introduce UI-neutral structs/enums in a shared location usable by both WPF and Avalonia, for example:
   - `ViewportPoint` with `double X`, `double Y`
   - `ViewportVector` if needed for deltas
   - `EditorModifierKeys` flags for None, Control, Shift, Alt
   - `EditorKey` values for the keys used by editor shortcuts: N, O, S, Z, Y, C, V, Delete, B, T, Escape
2. Update `ViewportPointerEvent`, `ViewportKeyEvent`, `ToolContext`, and `EditorShortcutRouter` to use those neutral types.
3. Add small WPF mapping helpers so the existing WPF app still compiles and behaves the same.
4. Move dialog-dependent behavior behind abstractions where needed:
   - `IEditorFileDialogService` for open/save/save-as paths
   - `IEditorMessageService` for error/info/confirm dialogs
5. Keep CommunityToolkit.Mvvm source generators working. Watch existing MVVM Toolkit naming conventions and generated command/property names.

Acceptance for this phase: existing WPF app still builds, existing tool and shortcut tests pass or are updated to neutral contracts without behavior changes.

## Phase 2: Create Avalonia App Project

Create a new Avalonia desktop project rather than replacing WPF in-place immediately.

Suggested structure:

- `src/MapEditor.App.Avalonia/MapEditor.App.Avalonia.csproj`
- `src/MapEditor.App.Avalonia/App.axaml`
- `src/MapEditor.App.Avalonia/App.axaml.cs`
- `src/MapEditor.App.Avalonia/MainWindow.axaml`
- `src/MapEditor.App.Avalonia/MainWindow.axaml.cs`
- `src/MapEditor.App.Avalonia/Views/ViewportPanel.axaml`
- `src/MapEditor.App.Avalonia/Views/ViewportPanel.axaml.cs`
- `src/MapEditor.App.Avalonia/Views/AvaloniaGlViewportHost.cs`
- `src/MapEditor.App.Avalonia/Interop/AvaloniaInputMapper.cs`
- `src/MapEditor.App.Avalonia/Services/AvaloniaEditorDialogService.cs`

Packages:

- `Avalonia`
- `Avalonia.Desktop`
- `Avalonia.Themes.Fluent`
- `Avalonia.Fonts.Inter`
- `CommunityToolkit.Mvvm`
- Existing Microsoft.Extensions.DependencyInjection/Hosting packages as needed

Use central package management in `Directory.Packages.props`.

Configure the new project for the same .NET SDK family as the repository. Keep it Windows-first if native WGL hosting requires Windows target properties. Do not attempt macOS/Linux support in the first implementation unless it falls out naturally.

Acceptance for this phase: Avalonia app starts, creates the main window, resolves existing services/viewmodels through DI, and shows static layout placeholders without OpenGL yet.

## Phase 3: Port The Editor Shell UI

Port the WPF shell to Avalonia XAML with UX parity, not a visual redesign.

Required UI surfaces:

- Top menu: File, Edit, View, Boolean, Help
- Toolbar actions with icon-like buttons for New, Open, Save, Undo, Redo
- Brush primitive toggles: Box, Cylinder, Cone, Wedge
- Brush operation toggles: Additive/Subtractive
- Tool selection behavior for Select/Create/Move as currently exposed
- Left scene outliner
- Center four-viewport grid: Top, Perspective, Front, Side
- Viewport header bars with maximize/restore behavior
- Right properties and texture workflow panels
- Bottom status bar with active tool, brush count, cursor position, and message

Avalonia control mapping hints:

- WPF `Window` -> Avalonia `Window`
- WPF `Menu/MenuItem` -> Avalonia `Menu/MenuItem`
- WPF `ToolBar` -> a styled horizontal `StackPanel` or `ToolBar` if available and stable
- WPF `ToggleButton` -> Avalonia `ToggleButton`
- WPF `Grid/GridSplitter` -> Avalonia `Grid` plus `GridSplitter`
- WPF `Expander`, `ListBox`, `TextBox`, `ComboBox` -> Avalonia equivalents
- WPF `Visibility.Collapsed` -> Avalonia `IsVisible = false`
- WPF dependency properties -> Avalonia `StyledProperty` or direct bindings depending on need

Keep the visual style work-focused and dense: dark editor theme, compact controls, stable viewport sizes, and no landing-page or marketing-style layout.

Acceptance for this phase: layout matches the current app closely enough to operate the editor, and all non-viewport commands bind to the existing view model.

## Phase 4: Implement Avalonia OpenGL Viewport Host

Implement `AvaloniaGlViewportHost` as a Windows-first native host that reuses the existing WGL/Silk.NET path.

Recommended first approach:

1. Derive from Avalonia native-hosting support, such as `NativeControlHost`, and create a Win32 child HWND under the Avalonia parent platform handle.
2. Reuse the current `GlViewportHost` responsibilities:
   - Register a viewport window class with `CS_OWNDC`
   - Create a child HWND with `WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN`
   - Acquire HDC
   - Create a shared viewport context through `GlContextManager.Instance.CreateSharedViewportContext(hdc)`
   - Load Silk.NET `GL.GetApi(GlContextManager.GetOpenGlProcAddress)`
   - Render on an Avalonia render tick or dispatcher timer equivalent
   - Resize/move the child HWND using Avalonia bounds and render scaling
   - `wglMakeCurrent`, raise `RenderFrame`, `SwapBuffers`, release context
3. Preserve one shared resource context and one shared viewport context per viewport.
4. Keep `PixelWidth` and `PixelHeight` in physical pixels, using Avalonia render scaling. Do not mix device-independent coordinates with GL viewport pixels.
5. Dispose HWND, HDC, WGL context, render subscriptions, and timers deterministically.

Alternative only after investigation: use Avalonia's OpenGL control APIs if they can provide a stable shared OpenGL context and compatible Silk.NET function loading without rewriting the renderers. Do not switch to Skia, D3D, or a different rendering engine in this migration.

Acceptance for this phase: one Avalonia viewport renders the existing orthographic grid/scene using the existing renderer, then all four viewports render Top/Front/Side/Perspective with shared resources.

## Phase 5: Port ViewportPanel Behavior

Port `ViewportPanel` behavior from WPF to Avalonia while preserving the same editor semantics.

Required behavior:

- Attach perspective and orthographic renderers exactly like the WPF app.
- Build `ToolContext` with correct viewport kind, pixel dimensions, camera references, grid size, selection services, status callbacks, hit testing, and world projection helpers.
- Keep Top/Front/Side/Perspective camera and axis behavior identical.
- Keep left-handed Y-up world coordinates.
- Preserve axis colors: X red, Y green, Z blue.
- Preserve camera navigation:
  - Orthographic wheel zoom
  - Orthographic middle-button pan unless routed to select tool resize behavior
  - Perspective wheel zoom
  - Perspective right drag orbit
  - Perspective middle drag pan
  - Alt+left orbit
  - Alt+right zoom
- Preserve surface selection behavior in perspective view: Shift+left select face, Ctrl modifies selection, Alt excludes face select.
- Preserve maximize/restore behavior and double-click header toggle.

Acceptance for this phase: tool workflows and camera navigation feel the same as WPF in all four viewports.

## Phase 6: Mouse And Keyboard Forwarding

This is a release-critical phase. Do not rely on Avalonia's normal routed commands alone.

Requirements:

1. The native OpenGL child must forward pointer events to managed editor events:
   - left/middle/right down
   - left/middle/right up
   - move
   - wheel with correct signed delta
   - leave if available
2. The viewport host must set focus and capture mouse appropriately on pointer down, release capture on pointer up, and preserve drag behavior outside the viewport bounds when the OS allows it.
3. Keyboard shortcuts must work when the OpenGL viewport has focus:
   - Ctrl+N, Ctrl+O, Ctrl+S
   - Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z
   - Ctrl+C, Ctrl+V
   - Delete
   - B
   - T
   - Escape
4. Text entry controls must not trigger editor shortcuts while the user is typing. Preserve the existing `TextBoxBase` exclusion behavior with an Avalonia equivalent.
5. Implement an `AvaloniaInputMapper` that converts Avalonia/native key and modifier data to the neutral `EditorKey` and `EditorModifierKeys` contracts.
6. Add or update tests for shortcut routing independent of WPF/Avalonia UI classes.

Acceptance for this phase: every shortcut works both when focus is in the normal UI shell and when focus is inside a viewport native child; shortcuts do not fire while typing in text boxes.

## Phase 7: File Dialogs And Platform Services

Replace WPF-specific file/message dialogs with Avalonia adapters.

Requirements:

- Open existing `.shmap` files.
- Save and Save As existing `.shmap` files.
- Preserve dirty-state confirmation before destructive open/new/close.
- Preserve status bar messages and error handling.
- Keep `MapFileService` unchanged unless a very small adapter is necessary.

Acceptance for this phase: existing map files load/save with byte-compatible schema behavior, and no WPF dialog APIs remain in shared view models.

## Phase 8: Tests, Validation, And Cutover

Validation requirements:

1. Run `dotnet build`.
2. Run relevant tests under `tests/`.
3. Add tests where migration risk is highest:
   - neutral input mapping
   - shortcut routing
   - viewport pointer event construction
   - world point projection unaffected by UI framework changes
   - file dialog service behavior through mocks if view models were refactored
4. Manually smoke-test the Avalonia app:
   - launch app
   - render all four viewports
   - create a box brush in orthographic viewport
   - select brush
   - move/resize where supported
   - orbit/pan/zoom perspective camera
   - maximize and restore each viewport
   - Ctrl+Z/Ctrl+Y
   - Ctrl+C/Ctrl+V
   - Delete
   - open/save `.shmap`
5. Only after parity, decide whether to keep WPF temporarily or switch the solution's primary app to Avalonia. If removing WPF, do it as a final cleanup step and keep the git diff reviewable.

Acceptance for this phase: Avalonia app is the primary editor, WPF-only dependencies are isolated or removed, all tests pass, and manual smoke tests pass.

# Non-Negotiable Invariants

- Preserve the current `.shmap` format and loading/saving behavior.
- Preserve command-based scene mutations through `ISceneCommand` and `SceneService` so undo/redo remains correct.
- Preserve left-handed Y-up world coordinates.
- Preserve Top/Front/Side/Perspective viewport semantics.
- Preserve shared OpenGL resource behavior across viewports.
- Preserve texture loading and GPU cache behavior.
- Preserve explicit shortcut routing from the focused native viewport.
- Do not rewrite the renderer, shader pipeline, scene model, or format layer as part of the UI migration.
- Do not introduce a web UI, Electron, MAUI, or a game engine.

# Expected Final Deliverable

When done, provide a concise implementation summary with:

- Framework choice and why Avalonia was used.
- New/changed project structure.
- How OpenGL hosting works in Avalonia.
- How mouse/keyboard forwarding works.
- Tests run and results.
- Known limitations or follow-up work.

The implementation is not complete until the Avalonia editor can actually run and edit maps with viewport rendering and input parity against the current WPF app.
