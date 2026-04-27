using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapEditor.App.Services;
using MapEditor.App.Tools;
using MapEditor.Core.Commands;
using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;
using MapEditor.Core;
using MapEditor.Formats;
using System.Collections.ObjectModel;
using System.Numerics;

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
    private readonly IEditorFileDialogService _fileDialogService;
    private readonly IEditorMessageService _messageService;
    private readonly SceneOutlinerViewModel _outlinerVm;
    private readonly PropertiesViewModel _propertiesVm;
    private readonly StatusBarViewModel _statusBarVm;
    private readonly IBrushBooleanKernel _booleanKernel = new BspBrushBooleanKernel();
    private const float PasteOffset = 32f;

    private string? _filePath;
    private bool _isDirty;

    [ObservableProperty] private string _windowTitle = "MapEditor — Untitled";
    [ObservableProperty] private string _activeToolName = "Select";
    [ObservableProperty] private BrushPrimitive _newBrushPrimitive = BrushPrimitive.Box;
    [ObservableProperty] private BrushOperation _newBrushOperation = BrushOperation.Additive;
    [ObservableProperty] private string? _selectedTextureKey;
    [ObservableProperty] private string _textureSearchText = string.Empty;
    [ObservableProperty] private string _selectedTextureCategory = "All";
    [ObservableProperty] private string _selectedTextureKindFilter = "All";
    [ObservableProperty] private bool _showAnimatedTexturesOnly;
    [ObservableProperty] private bool _isTextureBrowserVisible;
    [ObservableProperty] private TextureAssetProviderDescriptor? _selectedOnlineTextureProvider;
    [ObservableProperty] private string _onlineTextureSearchText = "wood";
    [ObservableProperty] private ExternalTextureSearchResult? _selectedOnlineTextureResult;
    [ObservableProperty] private bool _isOnlineTextureBusy;
    [ObservableProperty] private string _onlineTextureStatus = "Search CC0 online libraries, then import only the materials you choose.";

    public SceneService SceneService => _sceneService;
    public ActiveToolService ActiveToolService => _activeToolService;
    public SelectionService SelectionService => _selectionService;
    public SceneOutlinerViewModel Outliner => _outlinerVm;
    public PropertiesViewModel Properties => _propertiesVm;
    public StatusBarViewModel StatusBar => _statusBarVm;
    public SurfaceSelectionService SurfaceSelectionService => _surfaceSelectionService;
    public TextureLibraryService TextureLibrary => _textureLibraryService;
    public IReadOnlyList<TextureLibraryEntry> AvailableTextures => _textureLibraryService.Entries;
    public IReadOnlyList<TextureAssetProviderDescriptor> TextureAssetProviders => _textureLibraryService.TextureAssetProviders;
    public ObservableCollection<ExternalTextureSearchResult> OnlineTextureResults { get; } = [];
    public IReadOnlyList<string> TextureCategories =>
        ["All", .. AvailableTextures.Select(entry => entry.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(category => category, StringComparer.OrdinalIgnoreCase)];
    public IReadOnlyList<string> TextureKindFilters =>
        ["All", .. AvailableTextures.Select(entry => entry.KindLabel).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(kind => kind, StringComparer.OrdinalIgnoreCase)];
    public IReadOnlyList<TextureLibraryEntry> AnimatedTextures =>
        AvailableTextures.Where(entry => entry.IsAnimated).ToArray();
    public IReadOnlyList<TextureLibraryEntry> FilteredTextures =>
        AvailableTextures
            .Where(MatchesTextureFilters)
            .ToArray();
    public TextureLibraryEntry? SelectedTextureEntry =>
        AvailableTextures.FirstOrDefault(entry => string.Equals(entry.Key, SelectedTextureKey, StringComparison.OrdinalIgnoreCase));
    public bool IsSelectToolActive => _activeToolService.CurrentToolKind == EditorToolKind.Select;
    public bool IsCreateBrushToolActive => _activeToolService.CurrentToolKind == EditorToolKind.CreateBrush;
    public bool IsBoxBrushToolActive => IsBrushPrimitiveToolActive(BrushPrimitive.Box);
    public bool IsCylinderBrushToolActive => IsBrushPrimitiveToolActive(BrushPrimitive.Cylinder);
    public bool IsConeBrushToolActive => IsBrushPrimitiveToolActive(BrushPrimitive.Cone);
    public bool IsWedgeBrushToolActive => IsBrushPrimitiveToolActive(BrushPrimitive.Wedge);
    public bool IsAdditiveBrushOperationActive => NewBrushOperation == BrushOperation.Additive;
    public bool IsSubtractiveBrushOperationActive => NewBrushOperation == BrushOperation.Subtractive;
    public bool CanEditSelectedBrushOperation => GetSelectedBrush() is not null;
    public bool IsSelectedBrushAdditive => GetSelectedBrush()?.Operation == BrushOperation.Additive;
    public bool IsSelectedBrushSubtractive => GetSelectedBrush()?.Operation == BrushOperation.Subtractive;

    public MainViewModel(
        SceneService sceneService,
        MapFileService mapFileService,
        ActiveToolService activeToolService,
        SelectionService selectionService,
        SurfaceSelectionService surfaceSelectionService,
        BrushClipboardService brushClipboardService,
        TextureLibraryService textureLibraryService,
        SessionLogService sessionLogService,
        IEditorFileDialogService fileDialogService,
        IEditorMessageService messageService,
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
        _fileDialogService = fileDialogService;
        _messageService = messageService;
        _outlinerVm = outlinerVm;
        _propertiesVm = propertiesVm;
        _statusBarVm = statusBarVm;

        _sceneService.SceneChanged += OnSceneChanged;
        _selectionService.SelectionChanged += OnSelectionChanged;
        _surfaceSelectionService.SelectionChanged += OnSurfaceSelectionChanged;
        _activeToolService.ToolChanged += OnToolChanged;
        _textureLibraryService.LibraryChanged += OnTextureLibraryChanged;

        _outlinerVm.Refresh(_sceneService.Scene);
        _statusBarVm.BrushCount = _sceneService.Scene.Brushes.Count;
        SelectedTextureKey = AvailableTextures.FirstOrDefault()?.Key;
        SelectedOnlineTextureProvider = TextureAssetProviders.FirstOrDefault();
        RefreshSelectionDetails();
        OnToolChanged(this, _activeToolService.CurrentToolKind);
    }

    private void OnTextureLibraryChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(AvailableTextures));
        OnPropertyChanged(nameof(TextureAssetProviders));
        OnPropertyChanged(nameof(TextureCategories));
        OnPropertyChanged(nameof(TextureKindFilters));
        OnPropertyChanged(nameof(FilteredTextures));
        OnPropertyChanged(nameof(AnimatedTextures));
        OnPropertyChanged(nameof(SelectedTextureEntry));
        if (SelectedTextureEntry is null)
        {
            SelectedTextureKey = AvailableTextures.FirstOrDefault()?.Key;
        }
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
        SetSelectedBrushOperationCommand.NotifyCanExecuteChanged();
        SubtractSelectedBoxesCommand.NotifyCanExecuteChanged();
        MergeSelectedBoxesCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEditSelectedBrushOperation));
        OnPropertyChanged(nameof(IsSelectedBrushAdditive));
        OnPropertyChanged(nameof(IsSelectedBrushSubtractive));
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
        SetSelectedBrushOperationCommand.NotifyCanExecuteChanged();
        SubtractSelectedBoxesCommand.NotifyCanExecuteChanged();
        MergeSelectedBoxesCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEditSelectedBrushOperation));
        OnPropertyChanged(nameof(IsSelectedBrushAdditive));
        OnPropertyChanged(nameof(IsSelectedBrushSubtractive));
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
        NotifyBrushOperationStateChanged();
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
        var filePath = await _fileDialogService.ShowOpenMapDialogAsync();
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            var scene = await _mapFileService.LoadAsync(filePath);
            _sceneService.ReplaceScene(scene);
            _selectionService.Clear();
            _surfaceSelectionService.Clear();
            _activeToolService.SetTool(EditorToolKind.Select);
            _filePath = filePath;
            _isDirty  = false;
            UpdateTitle();
            _statusBarVm.Message = $"Opened {System.IO.Path.GetFileName(_filePath)}";
        }
        catch (Exception ex)
        {
            _sessionLogService.WriteException("OpenFileAsync", ex, filePath);
            await _messageService.ShowErrorAsync("Open Error", $"Failed to open file:\n{ex.Message}");
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
        var filePath = await _fileDialogService.ShowSaveMapDialogAsync(_filePath ?? "Untitled.shmap");
        if (string.IsNullOrWhiteSpace(filePath)) return;
        await DoSaveAsync(filePath);
        _filePath = filePath;
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
            await _messageService.ShowErrorAsync("Save Error", $"Failed to save file:\n{ex.Message}");
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
    private void SetNewBrushOperation(BrushOperation operation)
    {
        if (NewBrushOperation == operation)
        {
            return;
        }

        NewBrushOperation = operation;
        _statusBarVm.Message = "Brush creation now defaults to additive; use Subtract From Intersections for boolean cuts.";
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedBrushOperation))]
    private void SetSelectedBrushOperation(BrushOperation operation)
    {
        var brush = GetSelectedBrush();
        if (brush is null || brush.Operation == operation)
        {
            return;
        }

        _sceneService.Execute(new SetBrushOperationCommand(_sceneService.Scene, brush, operation));
        RefreshSelectionDetails();
        _statusBarVm.Message = $"Brush operation set to {operation}.";
    }

    [RelayCommand(CanExecute = nameof(CanSubtractSelectedBoxes))]
    private void SubtractSelectedBoxes()
    {
        if (!TryGetSubtractSelection(out var cutter, out var errorMessage))
        {
            _statusBarVm.Message = errorMessage;
            return;
        }

        var command = new SubtractIntersectingBrushesCommand(_sceneService.Scene, cutter!, _booleanKernel);
        _sceneService.Execute(command);
        var replacementBrushes = command.ReplacementBrushes;
        if (replacementBrushes.Count == 1)
        {
            _selectionService.SetSingle(replacementBrushes[0].Id);
        }
        else if (replacementBrushes.Count > 1)
        {
            _selectionService.SetSingle(replacementBrushes[0].Id);
            foreach (var replacement in replacementBrushes.Skip(1))
            {
                _selectionService.Add(replacement.Id);
            }
        }
        else
        {
            _selectionService.Clear();
        }

        RefreshSelectionDetails();
        _statusBarVm.Message = $"Subtracted '{cutter!.Name}' from {command.AffectedBrushCount} intersecting brush(es).";
    }

    private bool CanSubtractSelectedBoxes() =>
        TryGetSubtractSelection(out _, out _);

    [RelayCommand(CanExecute = nameof(CanMergeSelectedBoxes))]
    private void MergeSelectedBoxes()
    {
        if (!TryGetMergeSelection(out var selectedBrushes, out var errorMessage))
        {
            _statusBarVm.Message = errorMessage;
            return;
        }

        var command = new MergeSelectedBrushesCommand(_sceneService.Scene, selectedBrushes!, _booleanKernel);
        _sceneService.Execute(command);
        _selectionService.SetSingle(command.MergedBrush.Id);
        RefreshSelectionDetails();
        _statusBarVm.Message = $"Merged {selectedBrushes!.Count} brush(es) into '{command.MergedBrush.Name}'.";
    }

    private bool CanMergeSelectedBoxes() =>
        TryGetMergeSelection(out _, out _);

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

    partial void OnNewBrushOperationChanged(BrushOperation value) =>
        NotifyBrushOperationStateChanged();

    partial void OnSelectedTextureKeyChanged(string? value)
    {
        ApplySelectedTextureToBrushCommand.NotifyCanExecuteChanged();
        ApplySelectedTextureToSelectedSurfacesCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedTextureEntry));
        OnPropertyChanged(nameof(FilteredTextures));
    }

    partial void OnTextureSearchTextChanged(string value) =>
        OnPropertyChanged(nameof(FilteredTextures));

    partial void OnSelectedTextureCategoryChanged(string value) =>
        RefreshTextureFilters();

    partial void OnSelectedTextureKindFilterChanged(string value) =>
        RefreshTextureFilters();

    partial void OnShowAnimatedTexturesOnlyChanged(bool value) =>
        RefreshTextureFilters();

    partial void OnSelectedOnlineTextureProviderChanged(TextureAssetProviderDescriptor? value)
    {
        SearchOnlineTexturesCommand.NotifyCanExecuteChanged();
        ImportSelectedOnlineTextureCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedOnlineTextureResultChanged(ExternalTextureSearchResult? value) =>
        ImportSelectedOnlineTextureCommand.NotifyCanExecuteChanged();

    partial void OnIsOnlineTextureBusyChanged(bool value)
    {
        SearchOnlineTexturesCommand.NotifyCanExecuteChanged();
        ImportSelectedOnlineTextureCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ToggleTextureBrowser()
    {
        IsTextureBrowserVisible = !IsTextureBrowserVisible;
        _statusBarVm.Message = IsTextureBrowserVisible ? "Texture browser shown." : "Texture browser hidden.";
    }

    [RelayCommand]
    private void ShowAnimatedTextures()
    {
        IsTextureBrowserVisible = true;
        ShowAnimatedTexturesOnly = true;
        SelectedTextureCategory = "All";
        SelectedTextureKindFilter = "All";
        _statusBarVm.Message = "Showing animated built-in materials.";
    }

    private bool MatchesTextureFilters(TextureLibraryEntry entry)
    {
        if (ShowAnimatedTexturesOnly && !entry.IsAnimated)
        {
            return false;
        }

        if (!string.Equals(SelectedTextureCategory, "All", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entry.Category, SelectedTextureCategory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(SelectedTextureKindFilter, "All", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entry.KindLabel, SelectedTextureKindFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(TextureSearchText))
        {
            return true;
        }

        return entry.DisplayName.Contains(TextureSearchText, StringComparison.OrdinalIgnoreCase) ||
               entry.Key.Contains(TextureSearchText, StringComparison.OrdinalIgnoreCase) ||
             entry.ProviderName.Contains(TextureSearchText, StringComparison.OrdinalIgnoreCase) ||
               entry.Category.Contains(TextureSearchText, StringComparison.OrdinalIgnoreCase) ||
               entry.KindLabel.Contains(TextureSearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshTextureFilters()
    {
        OnPropertyChanged(nameof(FilteredTextures));
        OnPropertyChanged(nameof(AnimatedTextures));
    }

    [RelayCommand(CanExecute = nameof(CanSearchOnlineTextures))]
    private async Task SearchOnlineTexturesAsync()
    {
        if (SelectedOnlineTextureProvider is null)
        {
            OnlineTextureStatus = "Choose an online provider first.";
            return;
        }

        try
        {
            IsOnlineTextureBusy = true;
            OnlineTextureStatus = $"Searching {SelectedOnlineTextureProvider.DisplayName}...";
            OnlineTextureResults.Clear();
            var results = await _textureLibraryService.SearchProviderAsync(SelectedOnlineTextureProvider.Id, OnlineTextureSearchText);
            foreach (var result in results)
            {
                OnlineTextureResults.Add(result);
            }

            SelectedOnlineTextureResult = OnlineTextureResults.FirstOrDefault();
            OnlineTextureStatus = results.Count == 0
                ? "No matching online materials found."
                : $"Found {results.Count} CC0 material(s). Select one to import.";
        }
        catch (Exception ex)
        {
            _sessionLogService.WriteException("SearchOnlineTexturesAsync", ex, SelectedOnlineTextureProvider.Id);
            OnlineTextureStatus = $"Online texture search failed: {ex.Message}";
        }
        finally
        {
            IsOnlineTextureBusy = false;
        }
    }

    private bool CanSearchOnlineTextures() =>
        !IsOnlineTextureBusy && SelectedOnlineTextureProvider is not null;

    [RelayCommand(CanExecute = nameof(CanImportSelectedOnlineTexture))]
    private async Task ImportSelectedOnlineTextureAsync()
    {
        if (SelectedOnlineTextureResult is null)
        {
            OnlineTextureStatus = "Select an online material first.";
            return;
        }

        try
        {
            IsOnlineTextureBusy = true;
            OnlineTextureStatus = $"Importing {SelectedOnlineTextureResult.DisplayName}...";
            var imported = await _textureLibraryService.ImportExternalTextureAsync(SelectedOnlineTextureResult);
            SelectedTextureKey = imported.Key;
            SelectedTextureCategory = "All";
            TextureSearchText = imported.DisplayName;
            OnlineTextureStatus = $"Imported {imported.DisplayName} from {imported.ProviderName}.";
            _statusBarVm.Message = OnlineTextureStatus;
        }
        catch (Exception ex)
        {
            _sessionLogService.WriteException("ImportSelectedOnlineTextureAsync", ex, SelectedOnlineTextureResult.AssetId);
            OnlineTextureStatus = $"Online texture import failed: {ex.Message}";
        }
        finally
        {
            IsOnlineTextureBusy = false;
        }
    }

    private bool CanImportSelectedOnlineTexture() =>
        !IsOnlineTextureBusy && SelectedOnlineTextureResult is not null;

    [RelayCommand]
    private void ToggleSurfaceSelection(string surfaceId)
    {
        var brush = GetSelectedBrush();
        if (brush is null || !brush.HasSurface(surfaceId))
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
            surfaceIds.Where(brush.HasSurface));
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
            .Where(brush.HasSurface)
            .ToArray();
        _surfaceSelectionService.Replace(brush.Id, validSurfaceIds);
    }

    private static Vector2 GetFitScale(Brush brush, string surfaceId)
    {
        static float FitFor(float dimension) => dimension <= 0.001f ? 1f : 64f / dimension;

        if (brush.HasExplicitGeometry)
        {
            var face = brush.Geometry?.Faces.FirstOrDefault(candidate => candidate.Id == surfaceId);
            if (face is not null)
            {
                var vertices = face.Vertices.Select(vertex => Vector3.Multiply(vertex, brush.Transform.Scale)).ToArray();
                var normal = face.GetNormal();
                var axisU = GetFaceAxisU(normal, vertices);
                var axisV = Vector3.Normalize(Vector3.Cross(normal, axisU));
                var minU = vertices.Min(vertex => Vector3.Dot(vertex, axisU));
                var maxU = vertices.Max(vertex => Vector3.Dot(vertex, axisU));
                var minV = vertices.Min(vertex => Vector3.Dot(vertex, axisV));
                var maxV = vertices.Max(vertex => Vector3.Dot(vertex, axisV));
                return new Vector2(FitFor(maxU - minU), FitFor(maxV - minV));
            }
        }

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

    private static Vector3 GetFaceAxisU(Vector3 normal, IReadOnlyList<Vector3> vertices)
    {
        for (int i = 1; i < vertices.Count; i++)
        {
            var candidate = vertices[i] - vertices[0];
            if (candidate.LengthSquared() > 0.000001f)
            {
                return Vector3.Normalize(candidate);
            }
        }

        var fallback = MathF.Abs(Vector3.Dot(normal, Vector3.UnitY)) < 0.99f
            ? Vector3.UnitY
            : Vector3.UnitX;
        return Vector3.Normalize(Vector3.Cross(fallback, normal));
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

    private bool TryGetSubtractSelection(out Brush? cutter, out string errorMessage)
    {
        cutter = null;
        errorMessage = "Select one brush that intersects another brush.";

        cutter = GetSelectedBrush();
        if (cutter is null)
        {
            return false;
        }

        var selectedCutter = cutter;
        if (_sceneService.Scene.Brushes.Any(candidate => !ReferenceEquals(candidate, selectedCutter) && _booleanKernel.HasIntersection(selectedCutter, candidate)))
        {
            return true;
        }

        errorMessage = "No intersecting target brushes were found for the selected cutter.";
        return false;
    }

    private bool TryGetMergeSelection(out List<Brush>? selectedBrushes, out string errorMessage)
    {
        selectedBrushes = null;
        errorMessage = "Select two or more brushes that can become one solid.";

        var orderedSelection = GetSelectedBrushesInBooleanOrder();
        if (orderedSelection.Count < 2)
        {
            return false;
        }

        if (orderedSelection.Select(brush => brush.Operation).Distinct().Skip(1).Any())
        {
            errorMessage = "Merged brushes must use the same operation.";
            return false;
        }

        if (_booleanKernel.Merge(orderedSelection) is null)
        {
            errorMessage = "Those brushes cannot be represented as one valid merged brush.";
            return false;
        }

        selectedBrushes = orderedSelection;
        return true;
    }

    // ── Window close guard ─────────────────────────────────────────────────────

    public bool RequestClose()
    {
        if (!_isDirty) return true;
        var result = _messageService
            .ConfirmUnsavedChangesAsync("Unsaved Changes", "You have unsaved changes. Save before closing?")
            .GetAwaiter()
            .GetResult();
        if (result == EditorUnsavedChangesResult.Cancel) return false;
        if (result == EditorUnsavedChangesResult.Save)
        {
            SaveFileAsync().GetAwaiter().GetResult();
            return !_isDirty;
        }
        return true;
    }

    public async Task<bool> RequestCloseAsync()
    {
        if (!_isDirty) return true;

        var result = await _messageService.ConfirmUnsavedChangesAsync("Unsaved Changes", "You have unsaved changes. Save before closing?");
        if (result == EditorUnsavedChangesResult.Cancel) return false;
        if (result == EditorUnsavedChangesResult.Save)
        {
            await SaveFileAsync();
            return !_isDirty;
        }

        return true;
    }

    private bool ConfirmDiscard()
    {
        if (!_isDirty) return true;
        return _messageService
            .ConfirmDiscardAsync("Unsaved Changes", "Discard unsaved changes?")
            .GetAwaiter()
            .GetResult();
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

    private void NotifyBrushOperationStateChanged()
    {
        OnPropertyChanged(nameof(IsAdditiveBrushOperationActive));
        OnPropertyChanged(nameof(IsSubtractiveBrushOperationActive));
    }

    private List<Brush> GetSelectedBrushes() =>
        _selectionService.SelectedEntityIds
            .Select(id => _sceneService.Scene.Brushes.FirstOrDefault(brush => brush.Id == id))
            .Where(brush => brush is not null)
            .Cast<Brush>()
            .ToList();

    private List<Brush> GetSelectedBrushesInBooleanOrder()
    {
        var selectedBrushes = GetSelectedBrushes();
        if (_selectionService.PrimarySelectionId is not Guid primaryId)
        {
            return selectedBrushes;
        }

        return selectedBrushes
            .OrderByDescending(brush => brush.Id == primaryId)
            .ThenBy(brush => _sceneService.Scene.Brushes.TakeWhile(candidate => !ReferenceEquals(candidate, brush)).Count())
            .ToList();
    }
}
