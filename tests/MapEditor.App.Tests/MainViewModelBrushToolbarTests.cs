using FluentAssertions;
using MapEditor.App.Services;
using MapEditor.App.Tools;
using MapEditor.App.ViewModels;
using MapEditor.Core;
using MapEditor.Formats;
using System.IO;

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

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mapeditor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
