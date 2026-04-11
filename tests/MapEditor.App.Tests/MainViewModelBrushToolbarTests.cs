using FluentAssertions;
using MapEditor.App.Services;
using MapEditor.App.Tools;
using MapEditor.App.ViewModels;
using MapEditor.Core;
using MapEditor.Core.Commands;
using MapEditor.Core.Entities;
using MapEditor.Formats;
using System.IO;
using System.Numerics;

namespace MapEditor.App.Tests;

public sealed class MainViewModelBrushToolbarTests
{
    [Fact]
    public void ToggleCreateBrushPrimitiveCommand_ActsAsSingleUsePrimitiveToggle()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            var sceneService = new SceneService();
            var selectionService = new SelectionService();
            var sessionLogService = new SessionLogService(
                tempDirectory,
                new DateTimeOffset(2026, 4, 8, 17, 58, 21, TimeSpan.Zero));
            var statusBar = new StatusBarViewModel(sessionLogService);
            var activeToolService = new ActiveToolService(
                new SelectTool(new ResizeTool(), new MoveTool()),
                new CreateBrushTool(sceneService),
                new MoveTool());
            var viewModel = new MainViewModel(
                sceneService,
                new MapFileService(),
                activeToolService,
                selectionService,
                new SurfaceSelectionService(),
                new BrushClipboardService(),
                new TextureLibraryService(tempDirectory),
                sessionLogService,
                new SceneOutlinerViewModel(selectionService),
                new PropertiesViewModel(),
                statusBar);

            viewModel.ToggleCreateBrushPrimitiveCommand.Execute(MapEditor.Core.Entities.BrushPrimitive.Cylinder);

            activeToolService.CurrentToolKind.Should().Be(EditorToolKind.CreateBrush);
            viewModel.IsCylinderBrushToolActive.Should().BeTrue();
            viewModel.IsBoxBrushToolActive.Should().BeFalse();

            viewModel.ToggleCreateBrushPrimitiveCommand.Execute(MapEditor.Core.Entities.BrushPrimitive.Cylinder);

            activeToolService.CurrentToolKind.Should().Be(EditorToolKind.Select);
            viewModel.IsCylinderBrushToolActive.Should().BeFalse();
            statusBar.Message.Should().Be("Brush creation canceled.");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void ToggleTextureBrowserCommand_TogglesVisibilityState()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            var sceneService = new SceneService();
            var selectionService = new SelectionService();
            var sessionLogService = new SessionLogService(
                tempDirectory,
                new DateTimeOffset(2026, 4, 8, 17, 58, 21, TimeSpan.Zero));
            var statusBar = new StatusBarViewModel(sessionLogService);
            var activeToolService = new ActiveToolService(
                new SelectTool(new ResizeTool(), new MoveTool()),
                new CreateBrushTool(sceneService),
                new MoveTool());
            var viewModel = new MainViewModel(
                sceneService,
                new MapFileService(),
                activeToolService,
                selectionService,
                new SurfaceSelectionService(),
                new BrushClipboardService(),
                new TextureLibraryService(tempDirectory),
                sessionLogService,
                new SceneOutlinerViewModel(selectionService),
                new PropertiesViewModel(),
                statusBar);

            viewModel.IsTextureBrowserVisible.Should().BeTrue();

            viewModel.ToggleTextureBrowserCommand.Execute(null);

            viewModel.IsTextureBrowserVisible.Should().BeFalse();
            statusBar.Message.Should().Be("Texture browser hidden.");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateBrushCommand_StatusMessageOmitsBooleanCreationMode()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            var (_, _, _, _, _, _, viewModel, statusBar) = CreateHarness(tempDirectory);

            viewModel.CreateBrushCommand.Execute(null);

            statusBar.Message.Should().Be("Click-drag in Top, Front, or Side viewport to create a Box brush.");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void SubtractSelectedBoxesCommand_UsesPrimaryBrushAsCutterAndConsumesIt()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            var (sceneService, selectionService, _, _, _, _, viewModel, statusBar) = CreateHarness(tempDirectory);
            var target = CreateBoxBrush("Target", Vector3.Zero, new Vector3(4f, 4f, 4f), BrushOperation.Additive);
            var cutter = CreateBoxBrush("Cutter", new Vector3(0.5f, 0f, 0f), new Vector3(2f, 2f, 2f), BrushOperation.Additive);
            cutter.Transform = cutter.Transform with { EulerDegrees = new Vector3(0f, 25f, 0f) };

            sceneService.Execute(new CreateBrushCommand(sceneService.Scene, target));
            sceneService.Execute(new CreateBrushCommand(sceneService.Scene, cutter));
            selectionService.SetSingle(target.Id);
            selectionService.Add(cutter.Id);

            viewModel.SubtractSelectedBoxesCommand.CanExecute(null).Should().BeTrue();
            viewModel.SubtractSelectedBoxesCommand.Execute(null);

            sceneService.Scene.Brushes.Should().ContainSingle();
            sceneService.Scene.Brushes.Should().NotContain(cutter);
            sceneService.Scene.Brushes[0].HasExplicitGeometry.Should().BeTrue();
            selectionService.PrimarySelectionId.Should().Be(sceneService.Scene.Brushes[0].Id);
            statusBar.Message.Should().Be("Subtracted 'Cutter' from 1 intersecting brush(es).");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void MergeSelectedBoxesCommand_MergesCompatibleSelection()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            var (sceneService, selectionService, _, _, _, _, viewModel, statusBar) = CreateHarness(tempDirectory);
            var first = CreateBoxBrush("Left", new Vector3(-1f, 0f, 0f), new Vector3(2f, 2f, 2f), BrushOperation.Additive);
            var second = CreateBoxBrush("Right", new Vector3(1f, 0f, 0f), new Vector3(2f, 2f, 2f), BrushOperation.Additive);

            sceneService.Execute(new CreateBrushCommand(sceneService.Scene, first));
            sceneService.Execute(new CreateBrushCommand(sceneService.Scene, second));
            selectionService.SetSingle(first.Id);
            selectionService.Add(second.Id);

            viewModel.MergeSelectedBoxesCommand.CanExecute(null).Should().BeTrue();
            viewModel.MergeSelectedBoxesCommand.Execute(null);

            sceneService.Scene.Brushes.Should().ContainSingle();
            sceneService.Scene.Brushes[0].HasExplicitGeometry.Should().BeTrue();
            selectionService.PrimarySelectionId.Should().Be(sceneService.Scene.Brushes[0].Id);
            statusBar.Message.Should().Be($"Merged 2 brush(es) into '{sceneService.Scene.Brushes[0].Name}'.");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void MergeSelectedBoxesCommand_DisablesForDisconnectedSelection()
    {
        string tempDirectory = CreateTempDirectory();

        try
        {
            var (sceneService, selectionService, _, _, _, _, viewModel, _) = CreateHarness(tempDirectory);
            var first = CreateBoxBrush("Left", new Vector3(-6f, 0f, 0f), new Vector3(2f, 2f, 2f), BrushOperation.Additive);
            var second = CreateBoxBrush("Right", new Vector3(6f, 0f, 0f), new Vector3(2f, 2f, 2f), BrushOperation.Additive);

            sceneService.Execute(new CreateBrushCommand(sceneService.Scene, first));
            sceneService.Execute(new CreateBrushCommand(sceneService.Scene, second));
            selectionService.SetSingle(first.Id);
            selectionService.Add(second.Id);

            viewModel.MergeSelectedBoxesCommand.CanExecute(null).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mapeditor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static (SceneService SceneService, SelectionService SelectionService, SurfaceSelectionService SurfaceSelectionService, ActiveToolService ActiveToolService, TextureLibraryService TextureLibraryService, SessionLogService SessionLogService, MainViewModel ViewModel, StatusBarViewModel StatusBar) CreateHarness(string tempDirectory)
    {
        var sceneService = new SceneService();
        var selectionService = new SelectionService();
        var surfaceSelectionService = new SurfaceSelectionService();
        var sessionLogService = new SessionLogService(
            tempDirectory,
            new DateTimeOffset(2026, 4, 8, 17, 58, 21, TimeSpan.Zero));
        var statusBar = new StatusBarViewModel(sessionLogService);
        var activeToolService = new ActiveToolService(
            new SelectTool(new ResizeTool(), new MoveTool()),
            new CreateBrushTool(sceneService),
            new MoveTool());
        var textureLibraryService = new TextureLibraryService(tempDirectory);
        var viewModel = new MainViewModel(
            sceneService,
            new MapFileService(),
            activeToolService,
            selectionService,
            surfaceSelectionService,
            new BrushClipboardService(),
            textureLibraryService,
            sessionLogService,
            new SceneOutlinerViewModel(selectionService),
            new PropertiesViewModel(),
            statusBar);

        return (sceneService, selectionService, surfaceSelectionService, activeToolService, textureLibraryService, sessionLogService, viewModel, statusBar);
    }

    private static Brush CreateBoxBrush(string name, Vector3 position, Vector3 scale, BrushOperation operation)
    {
        return new Brush
        {
            Name = name,
            Primitive = BrushPrimitive.Box,
            Operation = operation,
            Transform = new Transform
            {
                Position = position,
                EulerDegrees = Vector3.Zero,
                Scale = scale
            }
        };
    }
}
