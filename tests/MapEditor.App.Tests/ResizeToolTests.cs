using FluentAssertions;
using MapEditor.App.Services;
using MapEditor.App.Tools;
using MapEditor.App.ViewModels;
using MapEditor.Core;
using MapEditor.Core.Commands;
using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;
using MapEditor.Rendering.Cameras;
using System.IO;
using System.Numerics;
using System.Windows;

namespace MapEditor.App.Tests;

public sealed class ResizeToolTests
{
    [Fact]
    public void OnPointerMove_StopsDraggingWhenBrushWasDeleted()
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

            resizeTool.TryStartHandleDrag(context, new ViewportPointerEvent(
                ViewportPointerAction.Down,
                ViewportPointerButton.Left,
                new Point(0, 0),
                0,
                default)).Should().BeTrue();

            sceneService.Execute(new DeleteBrushCommand(sceneService.Scene, brush));

            resizeTool.OnPointerMove(context, new ViewportPointerEvent(
                ViewportPointerAction.Move,
                ViewportPointerButton.None,
                new Point(1, 0),
                0,
                default));

            resizeTool.IsDragging.Should().BeFalse();
            sceneService.Scene.Brushes.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void TryStartHandleDrag_ReturnsFalseForExplicitGeometryBrush()
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
                    Scale = Vector3.One
                }
            };
            brush.SetGeometry(new BrushGeometry(
            [
                new BrushFace("face-a", [new(-2f, -2f, -2f), new(2f, -2f, -2f), new(2f, 2f, -2f), new(-2f, 2f, -2f)])
            ]));

            sceneService.Execute(new CreateBrushCommand(sceneService.Scene, brush));
            selectionService.SetSingle(brush.Id);

            var resizeTool = new ResizeTool();
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

            resizeTool.TryStartHandleDrag(context, new ViewportPointerEvent(
                ViewportPointerAction.Down,
                ViewportPointerButton.Left,
                new Point(0, 0),
                0,
                default)).Should().BeFalse();
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

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mapeditor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
