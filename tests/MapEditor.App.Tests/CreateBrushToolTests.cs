using FluentAssertions;
using MapEditor.App.Services;
using MapEditor.App.Tools;
using MapEditor.App.ViewModels;
using MapEditor.Core;
using MapEditor.Core.Entities;
using MapEditor.Rendering.Cameras;
using System.IO;
using System.Numerics;
using System.Windows;

namespace MapEditor.App.Tests;

public sealed class CreateBrushToolTests
{
    [Fact]
    public void OnPointerUp_SideViewportCreatesBrushInExpectedYZPlaneAndReturnsToSelect()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            var sceneService = new SceneService();
            var selectionService = new SelectionService();
            var statusBar = new StatusBarViewModel(new SessionLogService(
                tempDirectory,
                new DateTimeOffset(2026, 4, 8, 17, 58, 21, TimeSpan.Zero)));
            var createBrushTool = new CreateBrushTool(sceneService);
            EditorToolKind activeTool = EditorToolKind.CreateBrush;

            var context = new ToolContext
            {
                SceneService = sceneService,
                SelectionService = selectionService,
                StatusBarViewModel = statusBar,
                ViewportKind = EditorViewportKind.Side,
                PixelWidth = 100,
                PixelHeight = 100,
                GridSize = 32f,
                SelectedBrushPrimitive = BrushPrimitive.Box,
                OrthographicCamera = new OrthographicCamera
                {
                    Axis = ViewAxis.Side,
                    Zoom = 500f
                },
                PerspectiveCamera = null,
                ViewAxis = ViewAxis.Side,
                TryGetWorldPoint = ResolveSideWorldPoint,
                TryGetSnappedWorldPoint = ResolveSideWorldPoint,
                HitTestEntity = _ => null,
                RefreshSelectionDetails = static () => { },
                SetStatusMessage = message => statusBar.Message = message,
                SetActiveTool = toolKind => activeTool = toolKind
            };

            createBrushTool.OnPointerDown(context, new ViewportPointerEvent(
                ViewportPointerAction.Down,
                ViewportPointerButton.Left,
                new Point(0, 0),
                0,
                default));
            createBrushTool.OnPointerMove(context, new ViewportPointerEvent(
                ViewportPointerAction.Move,
                ViewportPointerButton.None,
                new Point(1, 0),
                0,
                default));
            createBrushTool.OnPointerUp(context, new ViewportPointerEvent(
                ViewportPointerAction.Up,
                ViewportPointerButton.Left,
                new Point(1, 0),
                0,
                default));

            var brush = sceneService.Scene.Brushes.Should().ContainSingle().Which;
            brush.Transform.Position.Should().Be(new Vector3(16f, 64f, 112f));
            brush.Transform.Scale.Should().Be(new Vector3(32f, 64f, 96f));
            activeTool.Should().Be(EditorToolKind.Select);
            statusBar.Message.Should().Be("Brush created.");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static Vector3? ResolveSideWorldPoint(Point point) => point.X switch
    {
        0d => new Vector3(0f, 32f, 64f),
        1d => new Vector3(0f, 96f, 160f),
        _ => null
    };

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mapeditor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
