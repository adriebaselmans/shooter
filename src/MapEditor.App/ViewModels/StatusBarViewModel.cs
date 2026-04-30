using CommunityToolkit.Mvvm.ComponentModel;
using MapEditor.App.Services;

namespace MapEditor.App.ViewModels;

/// <summary>Drives the status bar at the bottom of the main window.</summary>
public sealed partial class StatusBarViewModel : ObservableObject
{
    public StatusBarViewModel(SessionLogService sessionLogService)
    {
        LogFilePath = sessionLogService.LogFilePath;
        LogFileName = sessionLogService.LogFileName;
    }

    [ObservableProperty] private string _message = "Ready";
    [ObservableProperty] private int _brushCount;
    [ObservableProperty] private string _activeTool = "Select";
    [ObservableProperty] private string _cursorPos = "0, 0, 0";
    [ObservableProperty] private string _logFilePath = string.Empty;
    [ObservableProperty] private string _logFileName = string.Empty;
}
