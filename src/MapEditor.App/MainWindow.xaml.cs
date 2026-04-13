using MapEditor.App.Services;
using MapEditor.App.ViewModels;
using MapEditor.App.Views;
using MapEditor.Rendering.Cameras;
using MapEditor.Rendering.Infrastructure;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MapEditor.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly SessionLogService _sessionLogService;
    private ViewportPanel? _maximizedViewport;

    public MainWindow(MainViewModel vm, SessionLogService sessionLogService)
    {
        _vm = vm;
        _sessionLogService = sessionLogService;
        InitializeComponent();
        DataContext = _vm;

        _sessionLogService.WriteInfo("Main window constructed. Initializing OpenGL context manager.");
        GlContextManager.Initialise();
        _sessionLogService.WriteInfo("Shared OpenGL context manager initialized.");

        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _sessionLogService.WriteInfo("Main window loaded. Attaching viewport renderers.");
        var textureCatalog = _vm.TextureLibrary;
        ViewportTop.AttachOrthographic(_vm.SceneService, _vm.ActiveToolService, _vm.SelectionService, _vm.SurfaceSelectionService, _vm.StatusBar, textureCatalog, () => _vm.NewBrushPrimitive, () => MapEditor.Core.Entities.BrushOperation.Additive, ViewAxis.Top);
        ViewportFront.AttachOrthographic(_vm.SceneService, _vm.ActiveToolService, _vm.SelectionService, _vm.SurfaceSelectionService, _vm.StatusBar, textureCatalog, () => _vm.NewBrushPrimitive, () => MapEditor.Core.Entities.BrushOperation.Additive, ViewAxis.Front);
        ViewportSide.AttachOrthographic(_vm.SceneService, _vm.ActiveToolService, _vm.SelectionService, _vm.SurfaceSelectionService, _vm.StatusBar, textureCatalog, () => _vm.NewBrushPrimitive, () => MapEditor.Core.Entities.BrushOperation.Additive, ViewAxis.Side);
        ViewportPersp.AttachPerspective(_vm.SceneService, _vm.ActiveToolService, _vm.SelectionService, _vm.SurfaceSelectionService, _vm.StatusBar, textureCatalog, () => _vm.NewBrushPrimitive, () => MapEditor.Core.Entities.BrushOperation.Additive);

        ViewportTop.LayoutToggleRequested += OnViewportLayoutToggleRequested;
        ViewportFront.LayoutToggleRequested += OnViewportLayoutToggleRequested;
        ViewportSide.LayoutToggleRequested += OnViewportLayoutToggleRequested;
        ViewportPersp.LayoutToggleRequested += OnViewportLayoutToggleRequested;
        _sessionLogService.WriteInfo("Viewport renderers attached.");
    }

    private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_vm.RequestClose())
            e.Cancel = true;
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnResetCameraClick(object sender, RoutedEventArgs e)
    {
        ViewportPersp.PerspectiveRenderer?.Camera.ResetToDefault();
    }

    private void OnRestoreViewportLayoutClick(object sender, RoutedEventArgs e)
    {
        RestoreViewportLayout();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = TryHandleEditorShortcut(e.Key, Keyboard.Modifiers, e.OriginalSource);
    }

    internal bool TryHandleEditorShortcut(Key key, ModifierKeys modifiers, object? originalSource) =>
        EditorShortcutRouter.TryHandle(_vm, key, modifiers, originalSource);

    private void OnPropertyEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CommitPropertyEdits();
        e.Handled = true;
    }

    private void OnPropertyEditorLostFocus(object sender, RoutedEventArgs e)
    {
        CommitPropertyEdits();
    }

    private void OnSurfaceMappingEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CommitSurfaceMappingEdits();
        e.Handled = true;
    }

    private void OnSurfaceMappingEditorLostFocus(object sender, RoutedEventArgs e)
    {
        CommitSurfaceMappingEdits();
    }

    private void OnTextureLockChanged(object sender, RoutedEventArgs e)
    {
        CommitSurfaceMappingEdits();
    }

    private void CommitPropertyEdits()
    {
        if (_vm.CommitPropertyEditsCommand.CanExecute(null))
        {
            _vm.CommitPropertyEditsCommand.Execute(null);
        }
    }

    private void CommitSurfaceMappingEdits()
    {
        if (_vm.CommitSurfaceMappingEditsCommand.CanExecute(null))
        {
            _vm.CommitSurfaceMappingEditsCommand.Execute(null);
        }
    }

    private void OnSurfaceChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string surfaceId })
        {
            return;
        }

        if (_vm.ToggleSurfaceSelectionCommand.CanExecute(surfaceId))
        {
            _vm.ToggleSurfaceSelectionCommand.Execute(surfaceId);
        }
    }

    private void OnTextureWorkflowHelpClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, TextureWorkflowHelp.Message, TextureWorkflowHelp.Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnViewportLayoutToggleRequested(object? sender, EventArgs e)
    {
        if (sender is not ViewportPanel viewport)
        {
            return;
        }

        if (_maximizedViewport == viewport)
        {
            RestoreViewportLayout();
            return;
        }

        MaximizeViewport(viewport);
    }

    private void MaximizeViewport(ViewportPanel viewport)
    {
        _maximizedViewport = viewport;

        foreach (var panel in GetViewports())
        {
            panel.Visibility = panel == viewport ? Visibility.Visible : Visibility.Collapsed;
            panel.IsLayoutMaximized = panel == viewport;
        }

        ViewportColumnSplitter.Visibility = Visibility.Collapsed;
        ViewportRowSplitter.Visibility = Visibility.Collapsed;

        Grid.SetRow(viewport, 0);
        Grid.SetColumn(viewport, 0);
        Grid.SetRowSpan(viewport, 3);
        Grid.SetColumnSpan(viewport, 3);

        _vm.StatusBar.Message = $"{viewport.ViewportLabel} maximized.";
    }

    private void RestoreViewportLayout()
    {
        _maximizedViewport = null;

        ViewportTop.Visibility = Visibility.Visible;
        ViewportTop.IsLayoutMaximized = false;
        Grid.SetRow(ViewportTop, 0);
        Grid.SetColumn(ViewportTop, 0);
        Grid.SetRowSpan(ViewportTop, 1);
        Grid.SetColumnSpan(ViewportTop, 1);

        ViewportPersp.Visibility = Visibility.Visible;
        ViewportPersp.IsLayoutMaximized = false;
        Grid.SetRow(ViewportPersp, 0);
        Grid.SetColumn(ViewportPersp, 2);
        Grid.SetRowSpan(ViewportPersp, 1);
        Grid.SetColumnSpan(ViewportPersp, 1);

        ViewportFront.Visibility = Visibility.Visible;
        ViewportFront.IsLayoutMaximized = false;
        Grid.SetRow(ViewportFront, 2);
        Grid.SetColumn(ViewportFront, 0);
        Grid.SetRowSpan(ViewportFront, 1);
        Grid.SetColumnSpan(ViewportFront, 1);

        ViewportSide.Visibility = Visibility.Visible;
        ViewportSide.IsLayoutMaximized = false;
        Grid.SetRow(ViewportSide, 2);
        Grid.SetColumn(ViewportSide, 2);
        Grid.SetRowSpan(ViewportSide, 1);
        Grid.SetColumnSpan(ViewportSide, 1);

        ViewportColumnSplitter.Visibility = Visibility.Visible;
        ViewportRowSplitter.Visibility = Visibility.Visible;

        _vm.StatusBar.Message = "4-view layout restored.";
    }

    private IEnumerable<ViewportPanel> GetViewports()
    {
        yield return ViewportTop;
        yield return ViewportPersp;
        yield return ViewportFront;
        yield return ViewportSide;
    }
}
