using MapEditor.App.ViewModels;
using MapEditor.App.Views;
using MapEditor.Rendering.Cameras;
using MapEditor.Rendering.Infrastructure;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MapEditor.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private ViewportPanel? _maximizedViewport;

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = _vm;

        GlContextManager.Initialise();

        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewportTop.AttachOrthographic(_vm.SceneService, _vm.ActiveToolService, _vm.SelectionService, _vm.StatusBar, () => _vm.NewBrushPrimitive, ViewAxis.Top);
        ViewportFront.AttachOrthographic(_vm.SceneService, _vm.ActiveToolService, _vm.SelectionService, _vm.StatusBar, () => _vm.NewBrushPrimitive, ViewAxis.Front);
        ViewportSide.AttachOrthographic(_vm.SceneService, _vm.ActiveToolService, _vm.SelectionService, _vm.StatusBar, () => _vm.NewBrushPrimitive, ViewAxis.Side);
        ViewportPersp.AttachPerspective(_vm.SceneService, _vm.ActiveToolService, _vm.SelectionService, _vm.StatusBar, () => _vm.NewBrushPrimitive);

        ViewportTop.LayoutToggleRequested += OnViewportLayoutToggleRequested;
        ViewportFront.LayoutToggleRequested += OnViewportLayoutToggleRequested;
        ViewportSide.LayoutToggleRequested += OnViewportLayoutToggleRequested;
        ViewportPersp.LayoutToggleRequested += OnViewportLayoutToggleRequested;
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

    private void CommitPropertyEdits()
    {
        if (_vm.CommitPropertyEditsCommand.CanExecute(null))
        {
            _vm.CommitPropertyEditsCommand.Execute(null);
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
