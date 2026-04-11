using FluentAssertions;
using MapEditor.App.Services;
using MapEditor.App.Tools;
using MapEditor.App.ViewModels;
using MapEditor.Core;
using MapEditor.Core.Commands;
using MapEditor.Core.Entities;
using MapEditor.Rendering.Cameras;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Input;

namespace MapEditor.App.Tests;

public sealed class SelectToolTests
{
    [Fact]
    public void SelectTool_DragsResizeHandleWithoutSwitchingTools()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            var sceneService = new SceneService();
            var selectionService = new SelectionService();
            var statusBar = new StatusBarViewModel(new SessionLogService(
                tempDirectory,
                new DateTimeOffset(2026, 4, 8, 14, 55, 6, TimeSpan.Zero)));

            var brush = new Brush
            {
                Transform = new Transform
                {
                    Position = Vector3.Zero,
                    EulerDegrees = Vector3.Zero,
                    Scale = new Vector3(64f, 64f, 64f)
                }
            };

            sceneService.Execute(new CreateBrushCommand(sceneService.Scene, brush));
            selectionService.SetSingle(brush.Id);

            var resizeTool = new ResizeTool();
            var moveTool = new MoveTool();
            var selectTool = new SelectTool(resizeTool, moveTool);
            var context = new ToolContext
            {
                SceneService = sceneService,
                SelectionService = selectionService,
                StatusBarViewModel = statusBar,
                ViewportKind = EditorViewportKind.Top,
                PixelWidth = 100,
                PixelHeight = 100,
                GridSize = 32f,
                SelectedBrushPrimitive = BrushPrimitive.Box,
                SelectedBrushOperation = BrushOperation.Additive,
                OrthographicCamera = new OrthographicCamera
                {
                    Axis = ViewAxis.Top,
                    Zoom = 500f
                },
                PerspectiveCamera = null,
                ViewAxis = ViewAxis.Top,
                TryGetWorldPoint = ResolveWorldPoint,
                TryGetSnappedWorldPoint = ResolveWorldPoint,
                HitTestEntity = _ => brush.Id,
                RefreshSelectionDetails = static () => { },
                SetStatusMessage = message => statusBar.Message = message,
                SetActiveTool = static _ => { }
            };

            selectTool.OnPointerDown(context, new ViewportPointerEvent(
                ViewportPointerAction.Down,
                ViewportPointerButton.Left,
                new Point(0, 0),
                0,
                default));
            selectTool.OnPointerMove(context, new ViewportPointerEvent(
                ViewportPointerAction.Move,
                ViewportPointerButton.None,
                new Point(1, 0),
                0,
                default));
            selectTool.OnPointerUp(context, new ViewportPointerEvent(
                ViewportPointerAction.Up,
                ViewportPointerButton.Left,
                new Point(1, 0),
                0,
                default));

            brush.Transform.Position.Should().Be(new Vector3(16f, 0f, 16f));
            brush.Transform.Scale.Should().Be(new Vector3(96f, 64f, 96f));
            statusBar.Message.Should().Be("Selection resized.");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void SelectTool_MiddleDragMovesSelectedBrush()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            var sceneService = new SceneService();
            var selectionService = new SelectionService();
            var statusBar = new StatusBarViewModel(new SessionLogService(
                tempDirectory,
                new DateTimeOffset(2026, 4, 8, 14, 55, 6, TimeSpan.Zero)));

            var brush = new Brush
            {
                Transform = new Transform
                {
                    Position = Vector3.Zero,
                    EulerDegrees = Vector3.Zero,
                    Scale = new Vector3(64f, 64f, 64f)
                }
            };

            sceneService.Execute(new CreateBrushCommand(sceneService.Scene, brush));
            selectionService.SetSingle(brush.Id);

            var resizeTool = new ResizeTool();
            var moveTool = new MoveTool();
            var selectTool = new SelectTool(resizeTool, moveTool);
            var context = new ToolContext
            {
                SceneService = sceneService,
                SelectionService = selectionService,
                StatusBarViewModel = statusBar,
                ViewportKind = EditorViewportKind.Top,
                PixelWidth = 100,
                PixelHeight = 100,
                GridSize = 32f,
                SelectedBrushPrimitive = BrushPrimitive.Box,
                SelectedBrushOperation = BrushOperation.Additive,
                OrthographicCamera = new OrthographicCamera
                {
                    Axis = ViewAxis.Top,
                    Zoom = 500f
                },
                PerspectiveCamera = null,
                ViewAxis = ViewAxis.Top,
                TryGetWorldPoint = ResolveMoveWorldPoint,
                TryGetSnappedWorldPoint = ResolveMoveWorldPoint,
                HitTestEntity = _ => brush.Id,
                RefreshSelectionDetails = static () => { },
                SetStatusMessage = message => statusBar.Message = message,
                SetActiveTool = static _ => { }
            };

            selectTool.OnPointerDown(context, new ViewportPointerEvent(
                ViewportPointerAction.Down,
                ViewportPointerButton.Middle,
                new Point(0, 0),
                0,
                default));
            selectTool.OnPointerMove(context, new ViewportPointerEvent(
                ViewportPointerAction.Move,
                ViewportPointerButton.None,
                new Point(1, 0),
                0,
                default));
            selectTool.OnPointerUp(context, new ViewportPointerEvent(
                ViewportPointerAction.Up,
                ViewportPointerButton.Middle,
                new Point(1, 0),
                0,
                default));

            brush.Transform.Position.Should().Be(new Vector3(32f, 0f, 32f));
            statusBar.Message.Should().Be("Selection moved.");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void SelectTool_CtrlClickTogglesSelection()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            var sceneService = new SceneService();
            var selectionService = new SelectionService();
            var statusBar = new StatusBarViewModel(new SessionLogService(
                tempDirectory,
                new DateTimeOffset(2026, 4, 8, 14, 55, 6, TimeSpan.Zero)));

            var brushA = new Brush
            {
                Transform = new Transform
                {
                    Position = Vector3.Zero,
                    EulerDegrees = Vector3.Zero,
                    Scale = new Vector3(64f, 64f, 64f)
                }
            };

            var brushB = new Brush
            {
                Transform = new Transform
                {
                    Position = new Vector3(128f, 0f, 0f),
                    EulerDegrees = Vector3.Zero,
                    Scale = new Vector3(64f, 64f, 64f)
                }
            };

            sceneService.Execute(new CreateBrushCommand(sceneService.Scene, brushA));
            sceneService.Execute(new CreateBrushCommand(sceneService.Scene, brushB));

            var resizeTool = new ResizeTool();
            var moveTool = new MoveTool();
            var selectTool = new SelectTool(resizeTool, moveTool);

            Guid? hitResult = brushA.Id;

            var context = new ToolContext
            {
                SceneService = sceneService,
                SelectionService = selectionService,
                StatusBarViewModel = statusBar,
                ViewportKind = EditorViewportKind.Top,
                PixelWidth = 100,
                PixelHeight = 100,
                GridSize = 32f,
                SelectedBrushPrimitive = BrushPrimitive.Box,
                SelectedBrushOperation = BrushOperation.Additive,
                OrthographicCamera = new OrthographicCamera
                {
                    Axis = ViewAxis.Top,
                    Zoom = 500f
                },
                PerspectiveCamera = null,
                ViewAxis = ViewAxis.Top,
                TryGetWorldPoint = ResolveWorldPoint,
                TryGetSnappedWorldPoint = ResolveWorldPoint,
                HitTestEntity = _ => hitResult,
                RefreshSelectionDetails = static () => { },
                SetStatusMessage = message => statusBar.Message = message,
                SetActiveTool = static _ => { }
            };

            // Regular click selects brushA
            selectTool.OnPointerDown(context, new ViewportPointerEvent(
                ViewportPointerAction.Down,
                ViewportPointerButton.Left,
                new Point(0.5, 0.5),
                0,
                ModifierKeys.None));

            selectionService.SelectedEntityIds.Should().ContainSingle()
                .Which.Should().Be(brushA.Id);

            // Ctrl+click on brushB adds it to selection
            hitResult = brushB.Id;
            selectTool.OnPointerDown(context, new ViewportPointerEvent(
                ViewportPointerAction.Down,
                ViewportPointerButton.Left,
                new Point(0.5, 0.5),
                0,
                ModifierKeys.Control));

            selectionService.SelectedEntityIds.Should().HaveCount(2);
            selectionService.SelectedEntityIds.Should().Contain(brushA.Id);
            selectionService.SelectedEntityIds.Should().Contain(brushB.Id);

            // Ctrl+click on brushB again removes it (toggle)
            selectTool.OnPointerDown(context, new ViewportPointerEvent(
                ViewportPointerAction.Down,
                ViewportPointerButton.Left,
                new Point(0.5, 0.5),
                0,
                ModifierKeys.Control));

            selectionService.SelectedEntityIds.Should().ContainSingle()
                .Which.Should().Be(brushA.Id);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static Vector3? ResolveWorldPoint(Point point) => point.X switch
    {
        0d => new Vector3(32f, 0f, 32f),
        1d => new Vector3(64f, 0f, 64f),
        _ => null
    };

    private static Vector3? ResolveMoveWorldPoint(Point point) => point.X switch
    {
        0d => Vector3.Zero,
        1d => new Vector3(32f, 0f, 32f),
        _ => null
    };

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mapeditor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
