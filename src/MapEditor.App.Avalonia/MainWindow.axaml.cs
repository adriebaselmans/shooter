using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using MapEditor.App.Avalonia.Interop;
using MapEditor.App.Avalonia.Views;
using MapEditor.App.ViewModels;
using MapEditor.Core.Entities;
using MapEditor.Rendering.Cameras;
using System.ComponentModel;

namespace MapEditor.App.Avalonia;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private ViewportPanel? _maximizedViewport;
    private MaterialLibraryWindow? _materialLibraryWindow;
    private bool _closeConfirmed;
    private bool _closeConfirmationPending;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = null!; // Dummy for designer
    }

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Opened += OnOpened;
        Closing += OnClosing;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var textureCatalog = _viewModel.TextureLibrary;
        ViewportTop.AttachOrthographic(_viewModel.SceneService, _viewModel.ActiveToolService, _viewModel.SelectionService, _viewModel.SurfaceSelectionService, _viewModel.StatusBar, textureCatalog, () => _viewModel.NewBrushPrimitive, () => BrushOperation.Additive, ViewAxis.Top);
        ViewportFront.AttachOrthographic(_viewModel.SceneService, _viewModel.ActiveToolService, _viewModel.SelectionService, _viewModel.SurfaceSelectionService, _viewModel.StatusBar, textureCatalog, () => _viewModel.NewBrushPrimitive, () => BrushOperation.Additive, ViewAxis.Front);
        ViewportSide.AttachOrthographic(_viewModel.SceneService, _viewModel.ActiveToolService, _viewModel.SelectionService, _viewModel.SurfaceSelectionService, _viewModel.StatusBar, textureCatalog, () => _viewModel.NewBrushPrimitive, () => BrushOperation.Additive, ViewAxis.Side);
        ViewportPersp.AttachPerspective(_viewModel.SceneService, _viewModel.ActiveToolService, _viewModel.SelectionService, _viewModel.SurfaceSelectionService, _viewModel.StatusBar, textureCatalog, () => _viewModel.NewBrushPrimitive, () => BrushOperation.Additive);

        ViewportTop.LayoutToggleRequested += OnViewportLayoutToggleRequested;
        ViewportFront.LayoutToggleRequested += OnViewportLayoutToggleRequested;
        ViewportSide.LayoutToggleRequested += OnViewportLayoutToggleRequested;
        ViewportPersp.LayoutToggleRequested += OnViewportLayoutToggleRequested;
        RestoreViewportLayout();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsTextureBrowserVisible))
        {
            return;
        }

        if (_viewModel.IsTextureBrowserVisible)
        {
            ShowMaterialLibraryWindow();
        }
        else
        {
            _materialLibraryWindow?.Close();
        }
    }

    private void ShowMaterialLibraryWindow()
    {
        if (_materialLibraryWindow is not null)
        {
            _materialLibraryWindow.Activate();
            return;
        }

        _materialLibraryWindow = new MaterialLibraryWindow(_viewModel)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        _materialLibraryWindow.Closed += (_, _) =>
        {
            _materialLibraryWindow = null;
            if (_viewModel.IsTextureBrowserVisible)
            {
                _viewModel.IsTextureBrowserVisible = false;
            }
        };

        _materialLibraryWindow.Show(this);
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _materialLibraryWindow?.Close();
            return;
        }

        e.Cancel = true;
        if (_closeConfirmationPending)
        {
            return;
        }

        _closeConfirmationPending = true;
        try
        {
            if (await _viewModel.RequestCloseAsync())
            {
                _closeConfirmed = true;
                Close();
            }
        }
        finally
        {
            _closeConfirmationPending = false;
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = EditorShortcutRouter.TryHandle(
            _viewModel,
            AvaloniaInputMapper.ToEditorKey(e.Key),
            AvaloniaInputMapper.ToEditorModifiers(e.KeyModifiers),
            e.Source is TextBox);
    }

    internal bool TryHandleEditorShortcut(MapEditor.App.Tools.EditorKey key, MapEditor.App.Tools.EditorModifierKeys modifiers, bool isTextEditingSource) =>
        EditorShortcutRouter.TryHandle(_viewModel, key, modifiers, isTextEditingSource);

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private void OnResetCameraClick(object? sender, RoutedEventArgs e)
    {
        ViewportPersp.PerspectiveRenderer?.Camera.ResetToDefault();
    }

    private void OnBrushPrimitiveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string primitiveName } ||
            !Enum.TryParse<BrushPrimitive>(primitiveName, ignoreCase: true, out var primitive))
        {
            return;
        }

        _viewModel.ToggleCreateBrushPrimitiveCommand.Execute(primitive);
        e.Handled = true;
    }

    private void OnPropertyEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CommitPropertyEdits();
        e.Handled = true;
    }

    private void OnPropertyEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        CommitPropertyEdits();
    }

    private void OnSurfaceMappingEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CommitSurfaceMappingEdits();
        e.Handled = true;
    }

    private void OnSurfaceMappingEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        CommitSurfaceMappingEdits();
    }

    private void OnTextureLockChanged(object? sender, RoutedEventArgs e)
    {
        CommitSurfaceMappingEdits();
    }

    private void OnSurfaceChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string surfaceId })
        {
            return;
        }

        if (_viewModel.ToggleSurfaceSelectionCommand.CanExecute(surfaceId))
        {
            _viewModel.ToggleSurfaceSelectionCommand.Execute(surfaceId);
        }

        e.Handled = true;
    }

    private void CommitPropertyEdits()
    {
        if (_viewModel.CommitPropertyEditsCommand.CanExecute(null))
        {
            _viewModel.CommitPropertyEditsCommand.Execute(null);
        }
    }

    private void CommitSurfaceMappingEdits()
    {
        if (_viewModel.CommitSurfaceMappingEditsCommand.CanExecute(null))
        {
            _viewModel.CommitSurfaceMappingEditsCommand.Execute(null);
        }
    }

    private void OnRestoreViewportLayoutClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb && !tb.IsChecked.GetValueOrDefault())
        {
            // Toggle off -> maximize Perspective
            MaximizeViewport(ViewportPersp);
            return;
        }
        RestoreViewportLayout();
    }

    private void OnViewToggleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton btn && btn.Tag is string viewportName)
        {
            SplitViewToggle.IsChecked = false;
            var target = GetViewports().FirstOrDefault(v => v.Name == viewportName);
            if (target != null)
                MaximizeViewport(target);
        }
    }

    private void OnViewportLayoutToggleRequested(object? sender, EventArgs e)
    {
        if (sender is not ViewportPanel viewport)
        {
            return;
        }

        if (_maximizedViewport == viewport)
        {
            SplitViewToggle.IsChecked = true;
            RestoreViewportLayout();
            return;
        }

        SplitViewToggle.IsChecked = false;
        MaximizeViewport(viewport);
    }

    private void MaximizeViewport(ViewportPanel viewport)
    {
        _maximizedViewport = viewport;

        foreach (var panel in GetViewports())
        {
            panel.IsVisible = panel == viewport;
            panel.IsLayoutMaximized = panel == viewport;
        }

        ViewportColumnSplitter.IsVisible = false;
        ViewportRowSplitter.IsVisible = false;

        ViewportContainer.ColumnDefinitions = ColumnDefinitions.Parse("*,0,0");
        ViewportContainer.RowDefinitions = RowDefinitions.Parse("*,0,0");

        Grid.SetRow(viewport, 0);
        Grid.SetColumn(viewport, 0);
        Grid.SetRowSpan(viewport, 3);
        Grid.SetColumnSpan(viewport, 3);

        _viewModel.StatusBar.Message = $"{viewport.ViewportLabel} maximized.";
    }

    private void RestoreViewportLayout()
    {
        _maximizedViewport = null;

        ViewportContainer.ColumnDefinitions = ColumnDefinitions.Parse("*,5,*");
        ViewportContainer.RowDefinitions = RowDefinitions.Parse("*,5,*");

        RestoreViewport(ViewportTop, 0, 0);
        RestoreViewport(ViewportPersp, 0, 2);
        RestoreViewport(ViewportFront, 2, 0);
        RestoreViewport(ViewportSide, 2, 2);

        ViewportColumnSplitter.IsVisible = true;
        ViewportRowSplitter.IsVisible = true;
        SplitViewToggle.IsChecked = true;

        _viewModel.StatusBar.Message = "4-view layout restored.";
    }

    private static void RestoreViewport(ViewportPanel viewport, int row, int column)
    {
        viewport.IsVisible = true;
        viewport.IsLayoutMaximized = false;
        Grid.SetRow(viewport, row);
        Grid.SetColumn(viewport, column);
        Grid.SetRowSpan(viewport, 1);
        Grid.SetColumnSpan(viewport, 1);
    }

    private IEnumerable<ViewportPanel> GetViewports()
    {
        yield return ViewportTop;
        yield return ViewportPersp;
        yield return ViewportFront;
        yield return ViewportSide;
    }
}