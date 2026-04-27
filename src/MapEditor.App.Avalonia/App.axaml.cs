using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MapEditor.App.Avalonia.Services;
using MapEditor.App.Services;
using MapEditor.App.Tools;
using MapEditor.App.ViewModels;
using MapEditor.Core;
using MapEditor.Formats;
using Microsoft.Extensions.DependencyInjection;

namespace MapEditor.App.Avalonia;

public partial class App : Application
{
    private ServiceProvider? _services;
    private SessionLogService? _sessionLogService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _sessionLogService = new SessionLogService();
        var services = new ServiceCollection();
        RegisterServices(services, _sessionLogService);
        _services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var statusBar = _services.GetRequiredService<StatusBarViewModel>();
            statusBar.Message = $"Ready - logging to {Path.GetFileName(_sessionLogService.LogFilePath)}";
            var mainWindow = _services.GetRequiredService<MainWindow>();
            _services.GetRequiredService<AvaloniaEditorDialogService>().Attach(mainWindow);
            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) => _services?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterServices(IServiceCollection services, SessionLogService sessionLogService)
    {
        services.AddSingleton(sessionLogService);
        services.AddSingleton<AvaloniaEditorDialogService>();
        services.AddSingleton<IEditorFileDialogService>(provider => provider.GetRequiredService<AvaloniaEditorDialogService>());
        services.AddSingleton<IEditorMessageService>(provider => provider.GetRequiredService<AvaloniaEditorDialogService>());
        services.AddSingleton<SceneService>();
        services.AddSingleton<MapFileService>();
        services.AddSingleton<BrushClipboardService>();
        services.AddSingleton<SelectionService>();
        services.AddSingleton<SurfaceSelectionService>();
        services.AddSingleton<TextureLibraryService>();
        services.AddSingleton<ResizeTool>();
        services.AddSingleton<SelectTool>();
        services.AddSingleton<CreateBrushTool>();
        services.AddSingleton<MoveTool>();
        services.AddSingleton<ActiveToolService>();
        services.AddSingleton<SceneOutlinerViewModel>();
        services.AddSingleton<PropertiesViewModel>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}