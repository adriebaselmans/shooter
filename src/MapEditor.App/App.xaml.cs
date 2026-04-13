using MapEditor.App.Services;
using MapEditor.App.Tools;
using MapEditor.App.ViewModels;
using MapEditor.Core;
using MapEditor.Formats;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;

namespace MapEditor.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private SessionLogService? _sessionLogService;

    protected override void OnStartup(StartupEventArgs e)
    {
        _sessionLogService = new SessionLogService();
        RegisterGlobalExceptionLogging(_sessionLogService);
        _sessionLogService.WriteInfo("Application startup.");

        try
        {
            base.OnStartup(e);

            var sc = new ServiceCollection();
            sc.AddSingleton(_sessionLogService);
            sc.AddSingleton<SceneService>();
            sc.AddSingleton<MapFileService>();
            sc.AddSingleton<BrushClipboardService>();
            sc.AddSingleton<SelectionService>();
            sc.AddSingleton<SurfaceSelectionService>();
            sc.AddSingleton<TextureLibraryService>();
            sc.AddSingleton<ResizeTool>();
            sc.AddSingleton<SelectTool>();
            sc.AddSingleton<CreateBrushTool>();
            sc.AddSingleton<MoveTool>();
            sc.AddSingleton<ActiveToolService>();
            sc.AddSingleton<MainViewModel>();
            sc.AddSingleton<SceneOutlinerViewModel>();
            sc.AddSingleton<PropertiesViewModel>();
            sc.AddSingleton<StatusBarViewModel>();
            sc.AddSingleton<MainWindow>();

            _services = sc.BuildServiceProvider();
            _sessionLogService.WriteInfo("Service provider created.");

            var statusBar = _services.GetRequiredService<StatusBarViewModel>();
            statusBar.Message = $"Ready — logging to {Path.GetFileName(_sessionLogService.LogFilePath)}";
            _sessionLogService.WriteInfo("Status bar initialized.");

            var mainWindow = _services.GetRequiredService<MainWindow>();
            _sessionLogService.WriteInfo("Main window resolved from DI.");
            mainWindow.Show();
            _sessionLogService.WriteInfo("Main window shown.");
        }
        catch (Exception exception)
        {
            _sessionLogService.WriteException("Application startup failed", exception);
            ShowStartupFailure(exception, _sessionLogService.LogFilePath);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _sessionLogService?.WriteInfo("Application exit.");
        _services?.Dispose();
        base.OnExit(e);
    }

    private void RegisterGlobalExceptionLogging(SessionLogService sessionLogService)
    {
        DispatcherUnhandledException += (_, args) =>
            sessionLogService.WriteException("DispatcherUnhandledException", args.Exception);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            sessionLogService.WriteException(
                "AppDomain.UnhandledException",
                args.ExceptionObject as Exception,
                $"IsTerminating={args.IsTerminating}");

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            sessionLogService.WriteException("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };
    }

    private static void ShowStartupFailure(Exception exception, string logFilePath)
    {
        MessageBox.Show(
            $"MapEditor failed during startup.{Environment.NewLine}{Environment.NewLine}" +
            $"{exception.Message}{Environment.NewLine}{Environment.NewLine}" +
            $"Log: {logFilePath}",
            "MapEditor Startup Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
