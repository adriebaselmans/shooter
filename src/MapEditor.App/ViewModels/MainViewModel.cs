using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapEditor.App.Services;
using MapEditor.App.Tools;
using MapEditor.Core.Commands;
using MapEditor.Core.Entities;
using MapEditor.Core;
using MapEditor.Formats;
using Microsoft.Win32;
using System.Numerics;
using System.Windows;

namespace MapEditor.App.ViewModels;

/// <summary>
/// Root ViewModel — owns file operations, tool selection, undo/redo, and window title.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IEditorShortcutTarget
{
    private readonly SceneService _sceneService;
    private readonly MapFileService _mapFileService;
    private readonly ActiveToolService _activeToolService;
    private readonly SelectionService _selectionService;
    private readonly BrushClipboardService _brushClipboardService;
    private readonly SessionLogService _sessionLogService;
    private readonly SceneOutlinerViewModel _outlinerVm;
    private readonly PropertiesViewModel _propertiesVm;
    private readonly StatusBarViewModel _statusBarVm;
    private const float PasteOffset = 32f;

    private string? _filePath;
    private bool _isDirty;

    [ObservableProperty] private string _windowTitle = "MapEditor — Untitled";
    [ObservableProperty] private string _activeToolName = "Select";
    [ObservableProperty] private BrushPrimitive _newBrushPrimitive = BrushPrimitive.Box;

    public SceneService SceneService => _sceneService;
    public ActiveToolService ActiveToolService => _activeToolService;
    public SelectionService SelectionService => _selectionService;
    public SceneOutlinerViewModel Outliner => _outlinerVm;
    public PropertiesViewModel Properties => _propertiesVm;
    public StatusBarViewModel StatusBar => _statusBarVm;
    public bool IsSelectToolActive => _activeToolService.CurrentToolKind == EditorToolKind.Select;
    public bool IsCreateBrushToolActive => _activeToolService.CurrentToolKind == EditorToolKind.CreateBrush;
    public bool IsBoxBrushToolActive => IsBrushPrimitiveToolActive(BrushPrimitive.Box);
    public bool IsCylinderBrushToolActive => IsBrushPrimitiveToolActive(BrushPrimitive.Cylinder);
    public bool IsConeBrushToolActive => IsBrushPrimitiveToolActive(BrushPrimitive.Cone);
    public bool IsWedgeBrushToolActive => IsBrushPrimitiveToolActive(BrushPrimitive.Wedge);

    public MainViewModel(
        SceneService sceneService,
        MapFileService mapFileService,
        ActiveToolService activeToolService,
        SelectionService selectionService,
        BrushClipboardService brushClipboardService,
        SessionLogService sessionLogService,
        SceneOutlinerViewModel outlinerVm,
        PropertiesViewModel propertiesVm,
        StatusBarViewModel statusBarVm)
    {
        _sceneService = sceneService;
        _mapFileService = mapFileService;
        _activeToolService = activeToolService;
        _selectionService = selectionService;
        _brushClipboardService = brushClipboardService;
        _sessionLogService = sessionLogService;
        _outlinerVm = outlinerVm;
        _propertiesVm = propertiesVm;
        _statusBarVm = statusBarVm;

        _sceneService.SceneChanged += OnSceneChanged;
        _selectionService.SelectionChanged += OnSelectionChanged;
        _activeToolService.ToolChanged += OnToolChanged;

        _outlinerVm.Refresh(_sceneService.Scene);
        _statusBarVm.BrushCount = _sceneService.Scene.Brushes.Count;
        RefreshSelectionDetails();
        OnToolChanged(this, _activeToolService.CurrentToolKind);
    }

    private void OnSceneChanged(object? sender, EventArgs e)
    {
        _isDirty = true;
        UpdateTitle();
        _selectionService.RemoveMissing(_sceneService.Scene);
        _outlinerVm.Refresh(_sceneService.Scene);
        _statusBarVm.BrushCount = _sceneService.Scene.Brushes.Count;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        RefreshSelectionDetails();
        CopySelectedBrushCommand.NotifyCanExecuteChanged();
    }

    private void OnToolChanged(object? sender, EditorToolKind toolKind)
    {
        var toolName = _activeToolService.GetDisplayName();
        ActiveToolName = toolName;
        _statusBarVm.ActiveTool = toolName;

        OnPropertyChanged(nameof(IsSelectToolActive));
        OnPropertyChanged(nameof(IsCreateBrushToolActive));
        NotifyBrushPrimitiveToolStateChanged();
    }

    private void UpdateTitle()
    {
        var name  = _filePath != null ? System.IO.Path.GetFileName(_filePath) : "Untitled";
        var dirty = _isDirty ? "*" : string.Empty;
        WindowTitle = $"MapEditor — {dirty}{name}";
    }

    // ── File operations ────────────────────────────────────────────────────────

    [RelayCommand]
    private void NewFile()
    {
        if (!ConfirmDiscard()) return;
        _sceneService.ReplaceScene(new Scene());
        _selectionService.Clear();
        _activeToolService.SetTool(EditorToolKind.Select);
        _filePath = null;
        _isDirty  = false;
        UpdateTitle();
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = "MapEditor Map|*.shmap|All Files|*.*" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var scene = await _mapFileService.LoadAsync(dlg.FileName);
            _sceneService.ReplaceScene(scene);
            _selectionService.Clear();
            _activeToolService.SetTool(EditorToolKind.Select);
            _filePath = dlg.FileName;
            _isDirty  = false;
            UpdateTitle();
            _statusBarVm.Message = $"Opened {System.IO.Path.GetFileName(_filePath)}";
        }
        catch (Exception ex)
        {
            _sessionLogService.WriteException("OpenFileAsync", ex, dlg.FileName);
            MessageBox.Show($"Failed to open file:\n{ex.Message}", "Open Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task SaveFileAsync()
    {
        if (_filePath == null) { await SaveFileAsAsync(); return; }
        await DoSaveAsync(_filePath);
    }

    [RelayCommand]
    private async Task SaveFileAsAsync()
    {
        var dlg = new SaveFileDialog { Filter = "MapEditor Map|*.shmap|All Files|*.*", DefaultExt = ".shmap" };
        if (dlg.ShowDialog() != true) return;
        await DoSaveAsync(dlg.FileName);
        _filePath = dlg.FileName;
        UpdateTitle();
    }

    private async Task DoSaveAsync(string path)
    {
        try
        {
            await _mapFileService.SaveAsync(_sceneService.Scene, path);
            _isDirty = false;
            UpdateTitle();
            _statusBarVm.Message = $"Saved {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            _sessionLogService.WriteException("DoSaveAsync", ex, path);
            MessageBox.Show($"Failed to save file:\n{ex.Message}", "Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Edit operations ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _sceneService.Undo();
    private bool CanUndo() => _sceneService.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _sceneService.Redo();
    private bool CanRedo() => _sceneService.CanRedo;

    [RelayCommand(CanExecute = nameof(CanCopySelectedBrush))]
    private void CopySelectedBrush()
    {
        var brush = GetSelectedBrush();
        if (brush is null)
        {
            return;
        }

        _brushClipboardService.Copy(brush);
        PasteBrushCommand.NotifyCanExecuteChanged();
        _statusBarVm.Message = "Brush copied.";
    }

    private bool CanCopySelectedBrush() => GetSelectedBrush() is not null;

    [RelayCommand(CanExecute = nameof(CanPasteBrush))]
    private void PasteBrush()
    {
        var brush = _brushClipboardService.CreatePaste(new Vector3(PasteOffset, 0f, PasteOffset));
        if (brush is null)
        {
            return;
        }

        _sceneService.Execute(new CreateBrushCommand(_sceneService.Scene, brush));
        _selectionService.SetSingle(brush.Id);
        RefreshSelectionDetails();
        _statusBarVm.Message = "Brush pasted.";
    }

    private bool CanPasteBrush() => _brushClipboardService.HasBrush;

    [RelayCommand]
    private void DeleteSelected()
    {
        var selected = _selectionService.PrimarySelectionId ?? Guid.Empty;
        if (selected == Guid.Empty) return;

        var brush = _sceneService.Scene.Brushes.FirstOrDefault(b => b.Id == selected);
        if (brush != null)
        {
            _sceneService.Execute(new DeleteBrushCommand(_sceneService.Scene, brush));
            _selectionService.Clear();
        }
    }

    // ── Tool selection ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void SelectTool(string toolName)
    {
        if (Enum.TryParse<EditorToolKind>(toolName, true, out var toolKind))
        {
            _activeToolService.SetTool(toolKind);
            return;
        }

        _activeToolService.SetTool(toolName);
    }

    // ── Brush creation ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void CreateBrush()
    {
        _activeToolService.SetBrushPrimitive(NewBrushPrimitive);
        _activeToolService.SetTool(EditorToolKind.CreateBrush);
        _statusBarVm.Message = $"Click-drag in Top, Front, or Side viewport to create a {NewBrushPrimitive} brush.";
    }

    [RelayCommand]
    private void ToggleCreateBrushPrimitive(BrushPrimitive primitive)
    {
        if (IsBrushPrimitiveToolActive(primitive))
        {
            _activeToolService.SetTool(EditorToolKind.Select);
            _statusBarVm.Message = "Brush creation canceled.";
            return;
        }

        NewBrushPrimitive = primitive;
        _activeToolService.SetBrushPrimitive(primitive);
        _activeToolService.SetTool(EditorToolKind.CreateBrush);
        _statusBarVm.Message = $"Click-drag in Top, Front, or Side viewport to create a {primitive} brush.";
    }

    [RelayCommand]
    private void CommitPropertyEdits()
    {
        var brush = GetSelectedBrush();
        if (brush is null || !_propertiesVm.CanEditTransform)
        {
            return;
        }

        if (!PropertiesViewModel.TryParseVector3(_propertiesVm.PositionText, out var position) ||
            !PropertiesViewModel.TryParseVector3(_propertiesVm.RotationText, out var rotation) ||
            !PropertiesViewModel.TryParseVector3(_propertiesVm.ScaleText, out var scale))
        {
            _statusBarVm.Message = "Invalid transform format. Use x, y, z.";
            RefreshSelectionDetails();
            return;
        }

        var newTransform = brush.Transform with
        {
            Position = position,
            EulerDegrees = rotation,
            Scale = scale
        };

        if (newTransform == brush.Transform)
        {
            return;
        }

        _sceneService.Execute(new TransformBrushCommand(_sceneService.Scene, brush, brush.Transform, newTransform));
        RefreshSelectionDetails();
        _statusBarVm.Message = "Brush properties updated.";
    }

    partial void OnNewBrushPrimitiveChanged(BrushPrimitive value) =>
        UpdateBrushPrimitive(value);

    bool IEditorShortcutTarget.TryExecuteShortcut(EditorShortcutAction action, object? parameter)
    {
        return action switch
        {
            EditorShortcutAction.NewFile => ExecuteCommand(NewFileCommand),
            EditorShortcutAction.OpenFile => ExecuteCommand(OpenFileCommand),
            EditorShortcutAction.SaveFile => ExecuteCommand(SaveFileCommand),
            EditorShortcutAction.Undo => ExecuteCommand(UndoCommand),
            EditorShortcutAction.Redo => ExecuteCommand(RedoCommand),
            EditorShortcutAction.Copy => ExecuteCommand(CopySelectedBrushCommand),
            EditorShortcutAction.Paste => ExecuteCommand(PasteBrushCommand),
            EditorShortcutAction.Delete => ExecuteCommand(DeleteSelectedCommand),
            EditorShortcutAction.CreateBrush => ExecuteCommand(CreateBrushCommand),
            EditorShortcutAction.SelectTool => ExecuteCommand(SelectToolCommand, parameter),
            _ => false
        };
    }

    public void RefreshSelectionDetails()
    {
        var selectedId = _selectionService.PrimarySelectionId;
        if (selectedId is null)
        {
            _propertiesVm.Clear();
            return;
        }

        var scene = _sceneService.Scene;
        var brush = scene.Brushes.FirstOrDefault(b => b.Id == selectedId.Value);
        if (brush is not null)
        {
            _propertiesVm.PopulateFromBrush(brush);
            return;
        }

        var light = scene.Lights.FirstOrDefault(l => l.Id == selectedId.Value);
        if (light is not null)
        {
            _propertiesVm.PopulateFromLight(light);
            return;
        }

        var spawnPoint = scene.SpawnPoints.FirstOrDefault(s => s.Id == selectedId.Value);
        if (spawnPoint is not null)
        {
            _propertiesVm.PopulateFromSpawnPoint(spawnPoint);
            return;
        }

        _propertiesVm.Clear();
    }

    private Brush? GetSelectedBrush()
    {
        var selectedId = _selectionService.PrimarySelectionId;
        if (selectedId is null)
        {
            return null;
        }

        return _sceneService.Scene.Brushes.FirstOrDefault(b => b.Id == selectedId.Value);
    }

    // ── Window close guard ─────────────────────────────────────────────────────

    public bool RequestClose()
    {
        if (!_isDirty) return true;
        var r = MessageBox.Show("You have unsaved changes. Save before closing?",
            "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        if (r == MessageBoxResult.Cancel) return false;
        if (r == MessageBoxResult.Yes)
        {
            // Fire-and-forget synchronous save dialog
            SaveFileAsync().GetAwaiter().GetResult();
        }
        return true;
    }

    private bool ConfirmDiscard()
    {
        if (!_isDirty) return true;
        var r = MessageBox.Show("Discard unsaved changes?",
            "Unsaved Changes", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        return r == MessageBoxResult.OK;
    }

    private static bool ExecuteCommand(System.Windows.Input.ICommand command, object? parameter = null)
    {
        if (!command.CanExecute(parameter))
        {
            return false;
        }

        command.Execute(parameter);
        return true;
    }

    private bool IsBrushPrimitiveToolActive(BrushPrimitive primitive) =>
        _activeToolService.CurrentToolKind == EditorToolKind.CreateBrush &&
        NewBrushPrimitive == primitive;

    private void UpdateBrushPrimitive(BrushPrimitive primitive)
    {
        _activeToolService.SetBrushPrimitive(primitive);
        NotifyBrushPrimitiveToolStateChanged();
    }

    private void NotifyBrushPrimitiveToolStateChanged()
    {
        OnPropertyChanged(nameof(IsBoxBrushToolActive));
        OnPropertyChanged(nameof(IsCylinderBrushToolActive));
        OnPropertyChanged(nameof(IsConeBrushToolActive));
        OnPropertyChanged(nameof(IsWedgeBrushToolActive));
    }
}
