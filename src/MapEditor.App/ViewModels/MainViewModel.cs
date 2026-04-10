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
    private readonly SurfaceSelectionService _surfaceSelectionService;
    private readonly BrushClipboardService _brushClipboardService;
    private readonly TextureLibraryService _textureLibraryService;
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
    [ObservableProperty] private string? _selectedTextureKey;
    [ObservableProperty] private string _textureSearchText = string.Empty;
    [ObservableProperty] private bool _isTextureBrowserVisible = true;

    public SceneService SceneService => _sceneService;
    public ActiveToolService ActiveToolService => _activeToolService;
    public SelectionService SelectionService => _selectionService;
    public SceneOutlinerViewModel Outliner => _outlinerVm;
    public PropertiesViewModel Properties => _propertiesVm;
    public StatusBarViewModel StatusBar => _statusBarVm;
    public SurfaceSelectionService SurfaceSelectionService => _surfaceSelectionService;
    public TextureLibraryService TextureLibrary => _textureLibraryService;
    public IReadOnlyList<TextureLibraryEntry> AvailableTextures => _textureLibraryService.Entries;
    public IReadOnlyList<TextureLibraryEntry> FilteredTextures =>
        string.IsNullOrWhiteSpace(TextureSearchText)
            ? AvailableTextures
            : AvailableTextures
                .Where(entry =>
                    entry.DisplayName.Contains(TextureSearchText, StringComparison.OrdinalIgnoreCase) ||
                    entry.Key.Contains(TextureSearchText, StringComparison.OrdinalIgnoreCase))
                .ToArray();
    public TextureLibraryEntry? SelectedTextureEntry =>
        AvailableTextures.FirstOrDefault(entry => string.Equals(entry.Key, SelectedTextureKey, StringComparison.OrdinalIgnoreCase));
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
        SurfaceSelectionService surfaceSelectionService,
        BrushClipboardService brushClipboardService,
        TextureLibraryService textureLibraryService,
        SessionLogService sessionLogService,
        SceneOutlinerViewModel outlinerVm,
        PropertiesViewModel propertiesVm,
        StatusBarViewModel statusBarVm)
    {
        _sceneService = sceneService;
        _mapFileService = mapFileService;
        _activeToolService = activeToolService;
        _selectionService = selectionService;
        _surfaceSelectionService = surfaceSelectionService;
        _brushClipboardService = brushClipboardService;
        _textureLibraryService = textureLibraryService;
        _sessionLogService = sessionLogService;
        _outlinerVm = outlinerVm;
        _propertiesVm = propertiesVm;
        _statusBarVm = statusBarVm;

        _sceneService.SceneChanged += OnSceneChanged;
        _selectionService.SelectionChanged += OnSelectionChanged;
        _surfaceSelectionService.SelectionChanged += OnSurfaceSelectionChanged;
        _activeToolService.ToolChanged += OnToolChanged;

        _outlinerVm.Refresh(_sceneService.Scene);
        _statusBarVm.BrushCount = _sceneService.Scene.Brushes.Count;
        SelectedTextureKey = AvailableTextures.FirstOrDefault()?.Key;
        RefreshSelectionDetails();
        OnToolChanged(this, _activeToolService.CurrentToolKind);
    }

    private void OnSceneChanged(object? sender, EventArgs e)
    {
        _isDirty = true;
        UpdateTitle();
        _selectionService.RemoveMissing(_sceneService.Scene);
        RemoveMissingSurfaceSelection();
        _outlinerVm.Refresh(_sceneService.Scene);
        _statusBarVm.BrushCount = _sceneService.Scene.Brushes.Count;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        SyncSurfaceSelectionToEntitySelection();
        RefreshSelectionDetails();
        CopySelectedBrushCommand.NotifyCanExecuteChanged();
        ApplySelectedTextureToBrushCommand.NotifyCanExecuteChanged();
        ApplySelectedTextureToSelectedSurfacesCommand.NotifyCanExecuteChanged();
        CommitSurfaceMappingEditsCommand.NotifyCanExecuteChanged();
        FitSelectedSurfaceMappingsCommand.NotifyCanExecuteChanged();
        ResetSelectedSurfaceMappingsCommand.NotifyCanExecuteChanged();
    }

    private void OnSurfaceSelectionChanged(object? sender, EventArgs e)
    {
        RefreshSelectionDetails();
        ApplySelectedTextureToSelectedSurfacesCommand.NotifyCanExecuteChanged();
        CommitSurfaceMappingEditsCommand.NotifyCanExecuteChanged();
        FitSelectedSurfaceMappingsCommand.NotifyCanExecuteChanged();
        ResetSelectedSurfaceMappingsCommand.NotifyCanExecuteChanged();
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
        _surfaceSelectionService.Clear();
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
            _surfaceSelectionService.Clear();
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
            _statusBarVm.Message = $"Saving {System.IO.Path.GetFileName(path)}...";
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
            _surfaceSelectionService.Clear();
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

    [RelayCommand(CanExecute = nameof(CanApplySelectedTextureToBrush))]
    private void ApplySelectedTextureToBrush()
    {
        var brush = GetSelectedBrush();
        if (brush is null || string.IsNullOrWhiteSpace(SelectedTextureKey))
        {
            return;
        }

        _sceneService.Execute(new ApplyBrushTextureCommand(_sceneService.Scene, brush, SelectedTextureKey));
        RefreshSelectionDetails();
        _statusBarVm.Message = $"Applied texture '{SelectedTextureKey}' to brush.";
    }

    private bool CanApplySelectedTextureToBrush() =>
        GetSelectedBrush() is not null && !string.IsNullOrWhiteSpace(SelectedTextureKey);

    [RelayCommand(CanExecute = nameof(CanApplySelectedTextureToSelectedSurfaces))]
    private void ApplySelectedTextureToSelectedSurfaces()
    {
        var brush = GetSelectedBrush();
        if (brush is null || string.IsNullOrWhiteSpace(SelectedTextureKey) || !_surfaceSelectionService.HasSelection)
        {
            return;
        }

        var updatedMappings = _surfaceSelectionService.SelectedSurfaceIds.ToDictionary(
            surfaceId => surfaceId,
            surfaceId =>
            {
                var current = brush.GetEffectiveSurfaceMapping(surfaceId);
                return current with { TextureKey = SelectedTextureKey! };
            },
            StringComparer.Ordinal);

        _sceneService.Execute(new UpdateSurfaceMappingCommand(_sceneService.Scene, brush, updatedMappings));
        RefreshSelectionDetails();
        _statusBarVm.Message = $"Applied texture '{SelectedTextureKey}' to selected surfaces.";
    }

    private bool CanApplySelectedTextureToSelectedSurfaces() =>
        GetSelectedBrush() is not null &&
        !string.IsNullOrWhiteSpace(SelectedTextureKey) &&
        _surfaceSelectionService.HasSelection;

    [RelayCommand(CanExecute = nameof(CanEditSelectedSurfaceMappings))]
    private void CommitSurfaceMappingEdits()
    {
        var brush = GetSelectedBrush();
        if (brush is null || !_surfaceSelectionService.HasSelection)
        {
            return;
        }

        if (!TryParseSurfaceMappingInputs(out var offset, out var scale, out var rotation))
        {
            _statusBarVm.Message = "Invalid surface mapping format.";
            RefreshSelectionDetails();
            return;
        }

        var updatedMappings = _surfaceSelectionService.SelectedSurfaceIds.ToDictionary(
            surfaceId => surfaceId,
            surfaceId =>
            {
                var current = brush.GetEffectiveSurfaceMapping(surfaceId);
                return current with
                {
                    Offset = offset,
                    Scale = scale,
                    RotationDegrees = rotation,
                    TextureLocked = _propertiesVm.TextureLockEnabled
                };
            },
            StringComparer.Ordinal);

        _sceneService.Execute(new UpdateSurfaceMappingCommand(_sceneService.Scene, brush, updatedMappings));
        RefreshSelectionDetails();
        _statusBarVm.Message = "Surface mapping updated.";
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedSurfaceMappings))]
    private void FitSelectedSurfaceMappings()
    {
        var brush = GetSelectedBrush();
        if (brush is null || !_surfaceSelectionService.HasSelection)
        {
            return;
        }

        var updatedMappings = _surfaceSelectionService.SelectedSurfaceIds.ToDictionary(
            surfaceId => surfaceId,
            surfaceId =>
            {
                var current = brush.GetEffectiveSurfaceMapping(surfaceId);
                var fitScale = GetFitScale(brush, surfaceId);
                return current with
                {
                    Offset = Vector2.Zero,
                    Scale = fitScale,
                    RotationDegrees = 0f
                };
            },
            StringComparer.Ordinal);

        _sceneService.Execute(new UpdateSurfaceMappingCommand(_sceneService.Scene, brush, updatedMappings));
        RefreshSelectionDetails();
        _statusBarVm.Message = "Surface mapping fit to selected faces.";
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedSurfaceMappings))]
    private void ResetSelectedSurfaceMappings()
    {
        var brush = GetSelectedBrush();
        if (brush is null || !_surfaceSelectionService.HasSelection)
        {
            return;
        }

        var updatedMappings = _surfaceSelectionService.SelectedSurfaceIds.ToDictionary(
            surfaceId => surfaceId,
            surfaceId => SurfaceMapping.Default(brush.GetEffectiveSurfaceMapping(surfaceId).TextureKey),
            StringComparer.Ordinal);

        _sceneService.Execute(new UpdateSurfaceMappingCommand(_sceneService.Scene, brush, updatedMappings));
        RefreshSelectionDetails();
        _statusBarVm.Message = "Surface mapping reset.";
    }

    private bool CanEditSelectedSurfaceMappings() =>
        GetSelectedBrush() is not null && _surfaceSelectionService.HasSelection;

    partial void OnNewBrushPrimitiveChanged(BrushPrimitive value) =>
        UpdateBrushPrimitive(value);

    partial void OnSelectedTextureKeyChanged(string? value)
    {
        ApplySelectedTextureToBrushCommand.NotifyCanExecuteChanged();
        ApplySelectedTextureToSelectedSurfacesCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedTextureEntry));
        OnPropertyChanged(nameof(FilteredTextures));
    }

    partial void OnTextureSearchTextChanged(string value) =>
        OnPropertyChanged(nameof(FilteredTextures));

    [RelayCommand]
    private void ToggleTextureBrowser()
    {
        IsTextureBrowserVisible = !IsTextureBrowserVisible;
        _statusBarVm.Message = IsTextureBrowserVisible ? "Texture browser shown." : "Texture browser hidden.";
    }

    [RelayCommand]
    private void ToggleSurfaceSelection(string surfaceId)
    {
        var brush = GetSelectedBrush();
        if (brush is null || !BrushSurfaceIds.IsValid(brush.Primitive, surfaceId))
        {
            return;
        }

        var selected = _surfaceSelectionService.BrushId == brush.Id
            ? _surfaceSelectionService.SelectedSurfaceIds.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        if (!selected.Add(surfaceId))
        {
            selected.Remove(surfaceId);
        }

        _surfaceSelectionService.Replace(brush.Id, selected);
        _statusBarVm.Message = _surfaceSelectionService.HasSelection
            ? $"Face selection: {_propertiesVm.SelectedSurfaceSummary}."
            : "Face selection cleared.";
    }

    bool IEditorShortcutTarget.TryExecuteShortcut(EditorShortcutAction action, object? parameter)
    {
        if (action == EditorShortcutAction.SelectTool &&
            parameter is string toolName &&
            string.Equals(toolName, nameof(EditorToolKind.Select), StringComparison.OrdinalIgnoreCase))
        {
            _surfaceSelectionService.Clear();
        }

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
            EditorShortcutAction.ToggleTextureBrowser => ExecuteCommand(ToggleTextureBrowserCommand),
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
            _propertiesVm.PopulateSurfaceMapping(brush, _surfaceSelectionService.BrushId == brush.Id
                ? _surfaceSelectionService.SelectedSurfaceIds
                : Array.Empty<string>());
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

    public void UpdateSelectedSurfaces(IEnumerable<string> surfaceIds)
    {
        var brush = GetSelectedBrush();
        if (brush is null)
        {
            _surfaceSelectionService.Clear();
            return;
        }

        _surfaceSelectionService.Replace(
            brush.Id,
            surfaceIds.Where(surfaceId => BrushSurfaceIds.IsValid(brush.Primitive, surfaceId)));
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

    private void SyncSurfaceSelectionToEntitySelection()
    {
        var brush = GetSelectedBrush();
        if (brush is null)
        {
            _surfaceSelectionService.Clear();
            return;
        }

        if (_surfaceSelectionService.BrushId == brush.Id)
        {
            return;
        }

        _surfaceSelectionService.Clear();
    }

    private void RemoveMissingSurfaceSelection()
    {
        if (_surfaceSelectionService.BrushId is null)
        {
            return;
        }

        var brush = _sceneService.Scene.Brushes.FirstOrDefault(candidate => candidate.Id == _surfaceSelectionService.BrushId.Value);
        if (brush is null)
        {
            _surfaceSelectionService.Clear();
            return;
        }

        var validSurfaceIds = _surfaceSelectionService.SelectedSurfaceIds
            .Where(surfaceId => BrushSurfaceIds.IsValid(brush.Primitive, surfaceId))
            .ToArray();
        _surfaceSelectionService.Replace(brush.Id, validSurfaceIds);
    }

    private static Vector2 GetFitScale(Brush brush, string surfaceId)
    {
        static float FitFor(float dimension) => dimension <= 0.001f ? 1f : 64f / dimension;

        var scale = brush.Transform.Scale;
        return surfaceId switch
        {
            BrushSurfaceIds.Top or BrushSurfaceIds.Bottom => new Vector2(FitFor(scale.X), FitFor(scale.Z)),
            BrushSurfaceIds.Front or BrushSurfaceIds.Back => new Vector2(FitFor(scale.X), FitFor(scale.Y)),
            BrushSurfaceIds.Left or BrushSurfaceIds.Right => new Vector2(FitFor(scale.Z), FitFor(scale.Y)),
            BrushSurfaceIds.Side => new Vector2(FitFor(MathF.PI * MathF.Max(scale.X, scale.Z)), FitFor(scale.Y)),
            BrushSurfaceIds.Base => new Vector2(FitFor(scale.X), FitFor(scale.Z)),
            _ => new Vector2(FitFor(scale.X), FitFor(scale.Y))
        };
    }

    private bool TryParseSurfaceMappingInputs(out Vector2 offset, out Vector2 scale, out float rotation)
    {
        rotation = 0f;
        if (!PropertiesViewModel.TryParseFloat(_propertiesVm.SurfaceOffsetUText, out var offsetU) ||
            !PropertiesViewModel.TryParseFloat(_propertiesVm.SurfaceOffsetVText, out var offsetV) ||
            !PropertiesViewModel.TryParseFloat(_propertiesVm.SurfaceScaleUText, out var scaleU) ||
            !PropertiesViewModel.TryParseFloat(_propertiesVm.SurfaceScaleVText, out var scaleV) ||
            !float.TryParse(_propertiesVm.SurfaceRotationText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out rotation))
        {
            offset = default;
            scale = default;
            return false;
        }

        offset = new Vector2(offsetU, offsetV);
        scale = new Vector2(scaleU, scaleV);
        return true;
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
