using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using MapEditor.App.Services;
using MapEditor.App.Tools;
using MapEditor.App.ViewModels;
using MapEditor.Core;
using MapEditor.Core.Entities;
using MapEditor.Rendering.Cameras;
using MapEditor.Rendering.Infrastructure;
using MapEditor.Rendering.Renderers;
using Silk.NET.OpenGL;

namespace MapEditor.App.Views;

/// <summary>
/// Composite viewport: label bar + GlViewportHost.
/// Hosts either a perspective or orthographic renderer.
/// </summary>
public partial class ViewportPanel : UserControl
{
    private const float GridSize = 32f;

    // ── Dependency properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty ViewportLabelProperty =
        DependencyProperty.Register(nameof(ViewportLabel), typeof(string), typeof(ViewportPanel),
            new PropertyMetadata("Viewport"));

    public static readonly DependencyProperty IsLayoutMaximizedProperty =
        DependencyProperty.Register(
            nameof(IsLayoutMaximized),
            typeof(bool),
            typeof(ViewportPanel),
            new PropertyMetadata(false, OnIsLayoutMaximizedChanged));

    public string ViewportLabel
    {
        get => (string)GetValue(ViewportLabelProperty);
        set => SetValue(ViewportLabelProperty, value);
    }

    public bool IsLayoutMaximized
    {
        get => (bool)GetValue(IsLayoutMaximizedProperty);
        set => SetValue(IsLayoutMaximizedProperty, value);
    }

    public event EventHandler? LayoutToggleRequested;

    // ── State ─────────────────────────────────────────────────────────────────

    private PerspectiveViewportRenderer? _perspRenderer;
    private OrthographicViewportRenderer? _orthoRenderer;
    private SceneService? _sceneService;
    private ActiveToolService? _activeToolService;
    private SelectionService? _selectionService;
    private StatusBarViewModel? _statusBarViewModel;
    private Func<BrushPrimitive>? _brushPrimitiveProvider;
    private EditorViewportKind _viewportKind;
    private ViewAxis? _viewAxis;
    private Point _lastPointerPosition;
    private CameraDragMode _cameraDragMode;

    public ViewportPanel()
    {
        InitializeComponent();
        GlHost.RenderFrame += OnRenderFrame;
        GlHost.PointerInput += OnPointerInput;
        GlHost.KeyInput += OnKeyInput;
    }

    /// <summary>Attaches a perspective renderer. Call after GlHost is initialised.</summary>
    public void AttachPerspective(
        SceneService sceneService,
        ActiveToolService activeToolService,
        SelectionService selectionService,
        StatusBarViewModel statusBarViewModel,
        Func<BrushPrimitive> brushPrimitiveProvider)
    {
        _sceneService = sceneService;
        _activeToolService = activeToolService;
        _selectionService = selectionService;
        _statusBarViewModel = statusBarViewModel;
        _brushPrimitiveProvider = brushPrimitiveProvider;
        _viewportKind = EditorViewportKind.Perspective;
        _viewAxis = null;
        _selectionService.SelectionChanged += OnSelectionChanged;

        GlHost.RenderFrame -= OnRenderFrame;
        GlHost.RenderFrame += (_, gl) =>
        {
            _perspRenderer ??= new PerspectiveViewportRenderer(gl);
            SyncSelectionVisuals();
            _perspRenderer.Render(_sceneService.Scene, GlHost.PixelWidth, GlHost.PixelHeight);
        };
    }

    /// <summary>Attaches an orthographic renderer. Call after GlHost is initialised.</summary>
    public void AttachOrthographic(
        SceneService sceneService,
        ActiveToolService activeToolService,
        SelectionService selectionService,
        StatusBarViewModel statusBarViewModel,
        Func<BrushPrimitive> brushPrimitiveProvider,
        ViewAxis axis)
    {
        _sceneService = sceneService;
        _activeToolService = activeToolService;
        _selectionService = selectionService;
        _statusBarViewModel = statusBarViewModel;
        _brushPrimitiveProvider = brushPrimitiveProvider;
        _viewportKind = axis switch
        {
            ViewAxis.Top => EditorViewportKind.Top,
            ViewAxis.Front => EditorViewportKind.Front,
            _ => EditorViewportKind.Side
        };
        _viewAxis = axis;
        _selectionService.SelectionChanged += OnSelectionChanged;

        GlHost.RenderFrame -= OnRenderFrame;
        GlHost.RenderFrame += (_, gl) =>
        {
            _orthoRenderer ??= new OrthographicViewportRenderer(gl, axis);
            SyncSelectionVisuals();
            _orthoRenderer.Render(_sceneService.Scene, GlHost.PixelWidth, GlHost.PixelHeight);
        };
    }

    public PerspectiveViewportRenderer? PerspectiveRenderer => _perspRenderer;
    public OrthographicViewportRenderer? OrthographicRenderer => _orthoRenderer;

    private void OnRenderFrame(object? sender, GL gl) { }

    private static void OnIsLayoutMaximizedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ViewportPanel panel)
        {
            return;
        }

        panel.LayoutToggleButton.Content = (bool)e.NewValue ? "Rest" : "Max";
        panel.LayoutToggleButton.ToolTip = (bool)e.NewValue ? "Restore 4-view layout" : "Maximize viewport";
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => SyncSelectionVisuals();

    private void SyncSelectionVisuals()
    {
        if (_selectionService is null)
        {
            return;
        }

        if (_perspRenderer is not null)
        {
            _perspRenderer.SelectedEntityIds.Clear();
            foreach (var id in _selectionService.SelectedEntityIds)
            {
                _perspRenderer.SelectedEntityIds.Add(id);
            }
        }

        if (_orthoRenderer is not null)
        {
            _orthoRenderer.SelectedEntityIds.Clear();
            foreach (var id in _selectionService.SelectedEntityIds)
            {
                _orthoRenderer.SelectedEntityIds.Add(id);
            }

            _orthoRenderer.PrimarySelectedEntityId = _selectionService.PrimarySelectionId;
        }
    }

    private void OnPointerInput(object? sender, ViewportPointerEvent pointerEvent)
    {
        if (_sceneService is null || _activeToolService is null || _selectionService is null || _statusBarViewModel is null)
        {
            return;
        }

        var context = BuildToolContext();
        if (HandleCameraNavigation(context, pointerEvent))
        {
            if (pointerEvent.Action != ViewportPointerAction.Wheel)
            {
                UpdateCursorStatus(pointerEvent.Position);
            }

            _lastPointerPosition = pointerEvent.Position;
            return;
        }

        UpdateCursorStatus(pointerEvent.Position);

        switch (pointerEvent.Action)
        {
            case ViewportPointerAction.Down:
                _activeToolService.CurrentTool.OnPointerDown(context, pointerEvent);
                break;
            case ViewportPointerAction.Move:
                _activeToolService.CurrentTool.OnPointerMove(context, pointerEvent);
                break;
            case ViewportPointerAction.Up:
                _activeToolService.CurrentTool.OnPointerUp(context, pointerEvent);
                break;
            case ViewportPointerAction.Leave:
                break;
        }

        _lastPointerPosition = pointerEvent.Position;
    }

    private void OnKeyInput(object? sender, ViewportKeyEvent keyEvent)
    {
        if (Window.GetWindow(this) is not MainWindow window)
        {
            return;
        }

        keyEvent.Handled = window.TryHandleEditorShortcut(keyEvent.Key, keyEvent.Modifiers, this);
    }

    private ToolContext BuildToolContext()
    {
        return new ToolContext
        {
            SceneService = _sceneService!,
            SelectionService = _selectionService!,
            StatusBarViewModel = _statusBarViewModel!,
            ViewportKind = _viewportKind,
            PixelWidth = Math.Max(1, GlHost.PixelWidth),
            PixelHeight = Math.Max(1, GlHost.PixelHeight),
            GridSize = GridSize,
            SelectedBrushPrimitive = _brushPrimitiveProvider?.Invoke() ?? BrushPrimitive.Box,
            OrthographicCamera = _orthoRenderer?.Camera,
            PerspectiveCamera = _perspRenderer?.Camera,
            ViewAxis = _viewAxis,
            TryGetWorldPoint = TryGetWorldPoint,
            TryGetSnappedWorldPoint = position =>
            {
                var worldPoint = TryGetWorldPoint(position);
                return worldPoint is null ? null : GridSnapper.Snap(worldPoint.Value, GridSize);
            },
            HitTestEntity = HitTestEntity,
            RefreshSelectionDetails = RefreshSelectionDetails,
            SetStatusMessage = message => _statusBarViewModel!.Message = message,
            SetActiveTool = toolKind => _activeToolService!.SetTool(toolKind)
        };
    }

    private void RefreshSelectionDetails()
    {
        if (DataContext is MainViewModel vm)
        {
            vm.RefreshSelectionDetails();
        }
    }

    private void UpdateCursorStatus(Point position)
    {
        if (_statusBarViewModel is null)
        {
            return;
        }

        var worldPoint = TryGetWorldPoint(position);
        if (worldPoint is null)
        {
            return;
        }

        _statusBarViewModel.CursorPos =
            $"{worldPoint.Value.X:0}, {worldPoint.Value.Y:0}, {worldPoint.Value.Z:0}";
    }

    private bool HandleCameraNavigation(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        if (_viewAxis is not null)
        {
            return HandleOrthographicCameraNavigation(context, pointerEvent);
        }

        return HandlePerspectiveCameraNavigation(pointerEvent);
    }

    private bool HandleOrthographicCameraNavigation(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        if (_orthoRenderer is null)
        {
            return false;
        }

        if (pointerEvent.Action == ViewportPointerAction.Wheel)
        {
            _orthoRenderer.Camera.Zoom = Math.Clamp(
                _orthoRenderer.Camera.Zoom * (pointerEvent.WheelDelta > 0 ? 0.9f : 1.1f),
                16f,
                10000f);
            return true;
        }

        if (pointerEvent.Action == ViewportPointerAction.Down &&
            pointerEvent.Button == ViewportPointerButton.Middle &&
            ShouldRouteOrthographicMiddlePointerToTool(context, pointerEvent))
        {
            return false;
        }

        if (pointerEvent.Action == ViewportPointerAction.Down && pointerEvent.Button == ViewportPointerButton.Middle)
        {
            _cameraDragMode = CameraDragMode.OrthographicPan;
            return true;
        }

        if (_cameraDragMode == CameraDragMode.OrthographicPan && pointerEvent.Action == ViewportPointerAction.Move)
        {
            var delta = pointerEvent.Position - _lastPointerPosition;
            var worldPerPixel = (_orthoRenderer.Camera.Zoom * 2f) / Math.Max(1, GlHost.PixelHeight);
            _orthoRenderer.Camera.Pan -= (float)(delta.X * worldPerPixel);
            _orthoRenderer.Camera.PanY += (float)(delta.Y * worldPerPixel);
            return true;
        }

        if (_cameraDragMode == CameraDragMode.OrthographicPan &&
            pointerEvent.Action == ViewportPointerAction.Up &&
            pointerEvent.Button == ViewportPointerButton.Middle)
        {
            _cameraDragMode = CameraDragMode.None;
            return true;
        }

        return false;
    }

    private bool ShouldRouteOrthographicMiddlePointerToTool(ToolContext context, ViewportPointerEvent pointerEvent) =>
        _activeToolService?.CurrentTool is SelectTool selectTool &&
        selectTool.ShouldCaptureOrthographicMiddlePointer(context, pointerEvent);

    private bool HandlePerspectiveCameraNavigation(ViewportPointerEvent pointerEvent)
    {
        if (_perspRenderer is null)
        {
            return false;
        }

        if (pointerEvent.Action == ViewportPointerAction.Wheel)
        {
            _perspRenderer.Camera.ZoomBy(pointerEvent.WheelDelta > 0 ? 0.9f : 1.1f);
            return true;
        }

        if (pointerEvent.Action == ViewportPointerAction.Down)
        {
            _cameraDragMode = pointerEvent.Button switch
            {
                ViewportPointerButton.Right when pointerEvent.IsAltPressed => CameraDragMode.PerspectiveZoom,
                ViewportPointerButton.Right => CameraDragMode.PerspectiveOrbit,
                ViewportPointerButton.Middle => CameraDragMode.PerspectivePan,
                ViewportPointerButton.Left when pointerEvent.IsAltPressed => CameraDragMode.PerspectiveOrbit,
                _ => CameraDragMode.None
            };

            return _cameraDragMode != CameraDragMode.None;
        }

        if (pointerEvent.Action == ViewportPointerAction.Move)
        {
            var delta = pointerEvent.Position - _lastPointerPosition;
            switch (_cameraDragMode)
            {
                case CameraDragMode.PerspectiveOrbit:
                    _perspRenderer.Camera.Orbit((float)delta.X * 0.35f, (float)-delta.Y * 0.35f);
                    return true;
                case CameraDragMode.PerspectivePan:
                    PanPerspectiveCamera(delta);
                    return true;
                case CameraDragMode.PerspectiveZoom:
                    _perspRenderer.Camera.ZoomBy(1f + (float)delta.Y * 0.01f);
                    return true;
            }
        }

        if (_cameraDragMode is CameraDragMode.PerspectiveOrbit or CameraDragMode.PerspectivePan or CameraDragMode.PerspectiveZoom &&
            pointerEvent.Action == ViewportPointerAction.Up)
        {
            _cameraDragMode = CameraDragMode.None;
            return true;
        }

        return false;
    }

    private void PanPerspectiveCamera(System.Windows.Vector delta)
    {
        if (_perspRenderer is null)
        {
            return;
        }

        var camera = _perspRenderer.Camera;
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        if (!float.IsFinite(forward.X) || !float.IsFinite(forward.Y) || !float.IsFinite(forward.Z) || forward.LengthSquared() < 0.0001f)
        {
            return;
        }

        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        if (!float.IsFinite(right.X) || !float.IsFinite(right.Y) || !float.IsFinite(right.Z) || right.LengthSquared() < 0.0001f)
        {
            return;
        }

        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var scale = camera.Distance * 0.0025f;

        camera.Pan((-right * (float)delta.X + up * (float)delta.Y) * scale);
    }

    private Vector3? TryGetWorldPoint(Point position)
    {
        return _viewAxis is not null
            ? TryGetOrthographicWorldPoint(position, _viewAxis.Value)
            : TryGetPerspectiveGroundPoint(position);
    }

    private Vector3? TryGetOrthographicWorldPoint(Point position, ViewAxis axis)
    {
        if (_orthoRenderer is null || GlHost.PixelWidth <= 0 || GlHost.PixelHeight <= 0)
        {
            return null;
        }

        return OrthographicViewportProjection.Project(
            position,
            _orthoRenderer.Camera,
            GlHost.PixelWidth,
            GlHost.PixelHeight,
            axis);
    }

    private Vector3? TryGetPerspectiveGroundPoint(Point position)
    {
        if (_perspRenderer is null || GlHost.PixelWidth <= 0 || GlHost.PixelHeight <= 0)
        {
            return null;
        }

        var ray = CreatePerspectiveRay(position);
        return IntersectPlane(ray, Vector3.UnitY, Vector3.Zero);
    }

    private ViewportRay CreatePerspectiveRay(Point position)
    {
        var camera = _perspRenderer!.Camera;
        float aspect = (float)GlHost.PixelWidth / Math.Max(1, GlHost.PixelHeight);
        float ndcX = (float)((position.X / GlHost.PixelWidth) * 2d - 1d);
        float ndcY = (float)(1d - (position.Y / GlHost.PixelHeight) * 2d);
        float tanHalfFov = MathF.Tan(float.DegreesToRadians(camera.FieldOfView) * 0.5f);

        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var direction = Vector3.Normalize(
            forward +
            right * (ndcX * aspect * tanHalfFov) +
            up * (ndcY * tanHalfFov));

        return new ViewportRay(camera.Position, direction);
    }

    private Guid? HitTestEntity(Point position)
    {
        if (_sceneService is null)
        {
            return null;
        }

        return _viewAxis is not null
            ? HitTestOrthographicBrush(position, _viewAxis.Value)
            : HitTestPerspectiveBrush(position);
    }

    private Guid? HitTestOrthographicBrush(Point position, ViewAxis axis)
    {
        var worldPoint = TryGetOrthographicWorldPoint(position, axis);
        if (worldPoint is null)
        {
            return null;
        }

        for (int i = _sceneService!.Scene.Brushes.Count - 1; i >= 0; i--)
        {
            var brush = _sceneService.Scene.Brushes[i];
            var half = brush.Transform.Scale * 0.5f;
            var center = brush.Transform.Position;
            bool hit = axis switch
            {
                ViewAxis.Top =>
                    Math.Abs(worldPoint.Value.X - center.X) <= half.X &&
                    Math.Abs(worldPoint.Value.Z - center.Z) <= half.Z,
                ViewAxis.Front =>
                    Math.Abs(worldPoint.Value.X - center.X) <= half.X &&
                    Math.Abs(worldPoint.Value.Y - center.Y) <= half.Y,
                _ =>
                    Math.Abs(worldPoint.Value.Y - center.Y) <= half.Y &&
                    Math.Abs(worldPoint.Value.Z - center.Z) <= half.Z
            };

            if (hit)
            {
                return brush.Id;
            }
        }

        return null;
    }

    private Guid? HitTestPerspectiveBrush(Point position)
    {
        if (_perspRenderer is null)
        {
            return null;
        }

        var ray = CreatePerspectiveRay(position);
        Guid? hitId = null;
        float nearestDistance = float.MaxValue;

        foreach (var brush in _sceneService!.Scene.Brushes)
        {
            var half = brush.Transform.Scale * 0.5f;
            var min = brush.Transform.Position - half;
            var max = brush.Transform.Position + half;
            if (!TryIntersectAabb(ray, min, max, out var distance))
            {
                continue;
            }

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                hitId = brush.Id;
            }
        }

        return hitId;
    }

    private static Vector3? IntersectPlane(ViewportRay ray, Vector3 planeNormal, Vector3 planePoint)
    {
        var denominator = Vector3.Dot(planeNormal, ray.Direction);
        if (MathF.Abs(denominator) < 0.0001f)
        {
            return null;
        }

        var t = Vector3.Dot(planePoint - ray.Origin, planeNormal) / denominator;
        return t < 0f ? null : ray.Origin + ray.Direction * t;
    }

    private static bool TryIntersectAabb(ViewportRay ray, Vector3 min, Vector3 max, out float distance)
    {
        distance = 0f;

        float tMin = float.NegativeInfinity;
        float tMax = float.PositiveInfinity;

        if (!UpdateAxis(ray.Origin.X, ray.Direction.X, min.X, max.X, ref tMin, ref tMax) ||
            !UpdateAxis(ray.Origin.Y, ray.Direction.Y, min.Y, max.Y, ref tMin, ref tMax) ||
            !UpdateAxis(ray.Origin.Z, ray.Direction.Z, min.Z, max.Z, ref tMin, ref tMax))
        {
            return false;
        }

        distance = tMin >= 0f ? tMin : tMax;
        return distance >= 0f;
    }

    private static bool UpdateAxis(
        float origin,
        float direction,
        float min,
        float max,
        ref float tMin,
        ref float tMax)
    {
        if (MathF.Abs(direction) < 0.0001f)
        {
            return origin >= min && origin <= max;
        }

        float inv = 1f / direction;
        float t1 = (min - origin) * inv;
        float t2 = (max - origin) * inv;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = Math.Max(tMin, t1);
        tMax = Math.Min(tMax, t2);
        return tMin <= tMax;
    }

    private enum CameraDragMode
    {
        None,
        OrthographicPan,
        PerspectiveOrbit,
        PerspectivePan,
        PerspectiveZoom
    }

    private void OnHeaderMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2)
        {
            return;
        }

        LayoutToggleRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnLayoutToggleClick(object sender, RoutedEventArgs e)
    {
        LayoutToggleRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
