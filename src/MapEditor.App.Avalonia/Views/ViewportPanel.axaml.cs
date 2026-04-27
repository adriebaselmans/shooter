using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MapEditor.App.Avalonia.Interop;
using MapEditor.App.Infrastructure;
using MapEditor.App.Services;
using MapEditor.App.Tools;
using MapEditor.App.ViewModels;
using MapEditor.App.Views;
using MapEditor.Core;
using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;
using MapEditor.Rendering.Cameras;
using MapEditor.Rendering.Infrastructure;
using MapEditor.Rendering.Renderers;
using Silk.NET.OpenGL;

namespace MapEditor.App.Avalonia.Views;

public partial class ViewportPanel : UserControl
{
    private const float GridSize = 32f;

    public static readonly StyledProperty<string> ViewportLabelProperty =
        AvaloniaProperty.Register<ViewportPanel, string>(nameof(ViewportLabel), "Viewport");

    public static readonly StyledProperty<bool> IsLayoutMaximizedProperty =
        AvaloniaProperty.Register<ViewportPanel, bool>(nameof(IsLayoutMaximized));

    private PerspectiveViewportRenderer? _perspectiveRenderer;
    private OrthographicViewportRenderer? _orthographicRenderer;
    private SceneService? _sceneService;
    private ActiveToolService? _activeToolService;
    private SelectionService? _selectionService;
    private SurfaceSelectionService? _surfaceSelectionService;
    private StatusBarViewModel? _statusBarViewModel;
    private ITextureCatalog? _textureCatalog;
    private Func<BrushPrimitive>? _brushPrimitiveProvider;
    private Func<BrushOperation>? _brushOperationProvider;
    private EventHandler<GL>? _renderFrameHandler;
    private bool _isDisposed;
    private EditorViewportKind _viewportKind;
    private ViewAxis? _viewAxis;
    private ViewportPoint _lastPointerPosition;
    private CameraDragMode _cameraDragMode;

    public ViewportPanel()
    {
        InitializeComponent();
        _renderFrameHandler = OnRenderFrame;
        GlHost.RenderFrame += _renderFrameHandler;
        GlHost.OpenGlDeinitializing += OnOpenGlDeinitializing;
    }

    public string ViewportLabel
    {
        get => GetValue(ViewportLabelProperty);
        set => SetValue(ViewportLabelProperty, value);
    }

    public bool IsLayoutMaximized
    {
        get => GetValue(IsLayoutMaximizedProperty);
        set => SetValue(IsLayoutMaximizedProperty, value);
    }

    public PerspectiveViewportRenderer? PerspectiveRenderer => _perspectiveRenderer;
    public OrthographicViewportRenderer? OrthographicRenderer => _orthographicRenderer;

    public event EventHandler? LayoutToggleRequested;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLayoutMaximizedProperty && LayoutToggleButton is not null)
        {
            LayoutToggleButton.Content = IsLayoutMaximized ? "Rest" : "Max";
        }
    }

    public void AttachPerspective(
        SceneService sceneService,
        ActiveToolService activeToolService,
        SelectionService selectionService,
        SurfaceSelectionService surfaceSelectionService,
        StatusBarViewModel statusBarViewModel,
        ITextureCatalog textureCatalog,
        Func<BrushPrimitive> brushPrimitiveProvider,
        Func<BrushOperation> brushOperationProvider)
    {
        AttachCommon(sceneService, activeToolService, selectionService, surfaceSelectionService, statusBarViewModel, textureCatalog, brushPrimitiveProvider, brushOperationProvider);
        _viewportKind = EditorViewportKind.Perspective;
        _viewAxis = null;

        ReplaceRenderHandler((_, gl) =>
        {
            if (_isDisposed || _sceneService is null)
            {
                return;
            }

            _perspectiveRenderer ??= new PerspectiveViewportRenderer(gl, _textureCatalog);
            SyncSelectionVisuals();
            _perspectiveRenderer.Render(_sceneService.Scene, GlHost.PixelWidth, GlHost.PixelHeight);
        });
    }

    public void AttachOrthographic(
        SceneService sceneService,
        ActiveToolService activeToolService,
        SelectionService selectionService,
        SurfaceSelectionService surfaceSelectionService,
        StatusBarViewModel statusBarViewModel,
        ITextureCatalog textureCatalog,
        Func<BrushPrimitive> brushPrimitiveProvider,
        Func<BrushOperation> brushOperationProvider,
        ViewAxis axis)
    {
        AttachCommon(sceneService, activeToolService, selectionService, surfaceSelectionService, statusBarViewModel, textureCatalog, brushPrimitiveProvider, brushOperationProvider);
        _viewportKind = axis switch
        {
            ViewAxis.Top => EditorViewportKind.Top,
            ViewAxis.Front => EditorViewportKind.Front,
            _ => EditorViewportKind.Side
        };
        _viewAxis = axis;

        ReplaceRenderHandler((_, gl) =>
        {
            if (_isDisposed || _sceneService is null)
            {
                return;
            }

            _orthographicRenderer ??= new OrthographicViewportRenderer(gl, axis, _textureCatalog);
            SyncSelectionVisuals();
            _orthographicRenderer.Render(_sceneService.Scene, GlHost.PixelWidth, GlHost.PixelHeight);
        });
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachViewportEvents();
        base.OnDetachedFromVisualTree(e);
    }

    private void ReplaceRenderHandler(EventHandler<GL> handler)
    {
        if (_renderFrameHandler is not null)
        {
            GlHost.RenderFrame -= _renderFrameHandler;
        }

        _renderFrameHandler = handler;
        GlHost.RenderFrame += _renderFrameHandler;
    }

    private void DetachViewportEvents()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_renderFrameHandler is not null)
        {
            GlHost.RenderFrame -= _renderFrameHandler;
            _renderFrameHandler = null;
        }

        if (_selectionService is not null)
        {
            _selectionService.SelectionChanged -= OnSelectionChanged;
        }

        if (_surfaceSelectionService is not null)
        {
            _surfaceSelectionService.SelectionChanged -= OnSelectionChanged;
        }

        if (InputSurface is not null)
        {
            InputSurface.PointerPressed -= OnViewportPointerPressed;
            InputSurface.PointerReleased -= OnViewportPointerReleased;
            InputSurface.PointerMoved -= OnViewportPointerMoved;
            InputSurface.PointerWheelChanged -= OnViewportPointerWheelChanged;
            InputSurface.PointerExited -= OnViewportPointerExited;
        }
    }

    private void OnOpenGlDeinitializing(object? sender, EventArgs e)
    {
        GlHost.OpenGlDeinitializing -= OnOpenGlDeinitializing;
        DisposeRendererResources();
    }

    private void DisposeRendererResources()
    {
        _perspectiveRenderer?.Dispose();
        _orthographicRenderer?.Dispose();
        _perspectiveRenderer = null;
        _orthographicRenderer = null;
    }

    private void AttachCommon(
        SceneService sceneService,
        ActiveToolService activeToolService,
        SelectionService selectionService,
        SurfaceSelectionService surfaceSelectionService,
        StatusBarViewModel statusBarViewModel,
        ITextureCatalog textureCatalog,
        Func<BrushPrimitive> brushPrimitiveProvider,
        Func<BrushOperation> brushOperationProvider)
    {
        _sceneService = sceneService;
        _activeToolService = activeToolService;
        _selectionService = selectionService;
        _surfaceSelectionService = surfaceSelectionService;
        _statusBarViewModel = statusBarViewModel;
        _textureCatalog = textureCatalog;
        _brushPrimitiveProvider = brushPrimitiveProvider;
        _brushOperationProvider = brushOperationProvider;
        _selectionService.SelectionChanged += OnSelectionChanged;
        _surfaceSelectionService.SelectionChanged += OnSelectionChanged;
    }

    private void OnRenderFrame(object? sender, GL gl)
    {
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => SyncSelectionVisuals();

    private void SyncSelectionVisuals()
    {
        if (_selectionService is null)
        {
            return;
        }

        if (_perspectiveRenderer is not null)
        {
            _perspectiveRenderer.SelectedEntityIds.Clear();
            foreach (var id in _selectionService.SelectedEntityIds)
            {
                _perspectiveRenderer.SelectedEntityIds.Add(id);
            }

            _perspectiveRenderer.SelectedSurfaceBrushId = _surfaceSelectionService?.BrushId;
            _perspectiveRenderer.SelectedSurfaceIds.Clear();
            if (_surfaceSelectionService is not null)
            {
                foreach (var surfaceId in _surfaceSelectionService.SelectedSurfaceIds)
                {
                    _perspectiveRenderer.SelectedSurfaceIds.Add(surfaceId);
                }
            }
        }

        if (_orthographicRenderer is not null)
        {
            _orthographicRenderer.SelectedEntityIds.Clear();
            foreach (var id in _selectionService.SelectedEntityIds)
            {
                _orthographicRenderer.SelectedEntityIds.Add(id);
            }

            _orthographicRenderer.PrimarySelectedEntityId = _selectionService.PrimarySelectionId;
        }
    }

    private void OnPointerInput(object? sender, ViewportPointerEvent pointerEvent)
    {
        if (_sceneService is null || _activeToolService is null || _selectionService is null || _statusBarViewModel is null)
        {
            return;
        }

        var context = BuildToolContext();
        if (HandlePerspectiveSurfaceSelection(pointerEvent))
        {
            _lastPointerPosition = pointerEvent.Position;
            return;
        }

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
        }

        _lastPointerPosition = pointerEvent.Position;
    }

    private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        InputSurface.Focus();
        e.Pointer.Capture(InputSurface);
        ForwardPointerEvent(e, ViewportPointerAction.Down, GetPressedButton(e), 0);
        e.Handled = true;
    }

    private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        ForwardPointerEvent(e, ViewportPointerAction.Up, GetReleasedButton(e), 0);
        if (e.Pointer.Captured == InputSurface)
        {
            e.Pointer.Capture(null);
        }
        e.Handled = true;
    }

    private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        ForwardPointerEvent(e, ViewportPointerAction.Move, ViewportPointerButton.None, 0);
        e.Handled = true;
    }

    private void OnViewportPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        ForwardPointerEvent(e, ViewportPointerAction.Wheel, ViewportPointerButton.None, e.Delta.Y > 0 ? 120 : -120);
        e.Handled = true;
    }

    private void OnViewportPointerExited(object? sender, PointerEventArgs e)
    {
        ForwardPointerEvent(e, ViewportPointerAction.Leave, ViewportPointerButton.None, 0);
    }

    private void ForwardPointerEvent(PointerEventArgs e, ViewportPointerAction action, ViewportPointerButton button, int wheelDelta)
    {
        var position = e.GetPosition(InputSurface);
        OnPointerInput(InputSurface, new ViewportPointerEvent(
            action,
            button,
            new ViewportPoint(position.X, position.Y),
            wheelDelta,
            AvaloniaInputMapper.ToEditorModifiers(e.KeyModifiers)));
    }

    private static ViewportPointerButton GetPressedButton(PointerPressedEventArgs e) =>
        e.GetCurrentPoint(null).Properties.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => ViewportPointerButton.Left,
            PointerUpdateKind.MiddleButtonPressed => ViewportPointerButton.Middle,
            PointerUpdateKind.RightButtonPressed => ViewportPointerButton.Right,
            _ => ViewportPointerButton.None
        };

    private static ViewportPointerButton GetReleasedButton(PointerReleasedEventArgs e) =>
        e.InitialPressMouseButton switch
        {
            MouseButton.Left => ViewportPointerButton.Left,
            MouseButton.Middle => ViewportPointerButton.Middle,
            MouseButton.Right => ViewportPointerButton.Right,
            _ => ViewportPointerButton.None
        };

    private bool HandlePerspectiveSurfaceSelection(ViewportPointerEvent pointerEvent)
    {
        if (_viewportKind != EditorViewportKind.Perspective ||
            _sceneService is null ||
            _activeToolService?.CurrentToolKind != EditorToolKind.Select ||
            _selectionService is null ||
            _surfaceSelectionService is null ||
            _statusBarViewModel is null ||
            pointerEvent.Action != ViewportPointerAction.Down ||
            pointerEvent.Button != ViewportPointerButton.Left ||
            !pointerEvent.IsShiftPressed ||
            pointerEvent.IsAltPressed)
        {
            return false;
        }

        var ray = CreatePerspectiveRay(pointerEvent.Position);
        var hit = BrushSurfaceHitTester.HitTest(ray, _sceneService.Scene.Brushes);
        if (hit is null)
        {
            if (!pointerEvent.IsControlPressed)
            {
                _surfaceSelectionService.Clear();
                _statusBarViewModel.Message = "No face under cursor.";
            }

            return true;
        }

        _selectionService.SetSingle(hit.Value.BrushId);
        var nextSelection = _surfaceSelectionService.BrushId == hit.Value.BrushId && pointerEvent.IsControlPressed
            ? _surfaceSelectionService.SelectedSurfaceIds.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        if (!nextSelection.Add(hit.Value.SurfaceId))
        {
            nextSelection.Remove(hit.Value.SurfaceId);
        }

        _surfaceSelectionService.Replace(hit.Value.BrushId, nextSelection);
        _statusBarViewModel.Message = nextSelection.Count switch
        {
            0 => "Face selection cleared.",
            1 => $"Face selected: {hit.Value.SurfaceId}.",
            _ => $"{nextSelection.Count} faces selected."
        };
        return true;
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
            SelectedBrushOperation = _brushOperationProvider?.Invoke() ?? BrushOperation.Additive,
            OrthographicCamera = _orthographicRenderer?.Camera,
            PerspectiveCamera = _perspectiveRenderer?.Camera,
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
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.RefreshSelectionDetails();
        }
    }

    private void UpdateCursorStatus(ViewportPoint position)
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

        _statusBarViewModel.CursorPos = $"{worldPoint.Value.X:0}, {worldPoint.Value.Y:0}, {worldPoint.Value.Z:0}";
    }

    private bool HandleCameraNavigation(ToolContext context, ViewportPointerEvent pointerEvent) =>
        _viewAxis is not null
            ? HandleOrthographicCameraNavigation(context, pointerEvent)
            : HandlePerspectiveCameraNavigation(pointerEvent);

    private bool HandleOrthographicCameraNavigation(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        if (_orthographicRenderer is null)
        {
            return false;
        }

        if (pointerEvent.Action == ViewportPointerAction.Wheel)
        {
            _orthographicRenderer.Camera.Zoom = Math.Clamp(
                _orthographicRenderer.Camera.Zoom * (pointerEvent.WheelDelta > 0 ? 0.9f : 1.1f),
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
            var worldPerPixel = (_orthographicRenderer.Camera.Zoom * 2f) / Math.Max(1, GlHost.PixelHeight);
            _orthographicRenderer.Camera.Pan -= (float)(delta.X * worldPerPixel);
            _orthographicRenderer.Camera.PanY += (float)(delta.Y * worldPerPixel);
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
        if (_perspectiveRenderer is null)
        {
            return false;
        }

        if (pointerEvent.Action == ViewportPointerAction.Wheel)
        {
            _perspectiveRenderer.Camera.ZoomBy(pointerEvent.WheelDelta > 0 ? 0.9f : 1.1f);
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
                    _perspectiveRenderer.Camera.Orbit((float)delta.X * 0.35f, (float)-delta.Y * 0.35f);
                    return true;
                case CameraDragMode.PerspectivePan:
                    PanPerspectiveCamera(delta);
                    return true;
                case CameraDragMode.PerspectiveZoom:
                    _perspectiveRenderer.Camera.ZoomBy(1f + (float)delta.Y * 0.01f);
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

    private void PanPerspectiveCamera(ViewportVector delta)
    {
        if (_perspectiveRenderer is null)
        {
            return;
        }

        var camera = _perspectiveRenderer.Camera;
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

    private Vector3? TryGetWorldPoint(ViewportPoint position) =>
        _viewAxis is not null
            ? TryGetOrthographicWorldPoint(position, _viewAxis.Value)
            : TryGetPerspectiveGroundPoint(position);

    private Vector3? TryGetOrthographicWorldPoint(ViewportPoint position, ViewAxis axis)
    {
        if (_orthographicRenderer is null || GlHost.PixelWidth <= 0 || GlHost.PixelHeight <= 0)
        {
            return null;
        }

        return OrthographicViewportProjection.Project(position, _orthographicRenderer.Camera, GlHost.PixelWidth, GlHost.PixelHeight, axis);
    }

    private Vector3? TryGetPerspectiveGroundPoint(ViewportPoint position)
    {
        if (_perspectiveRenderer is null || GlHost.PixelWidth <= 0 || GlHost.PixelHeight <= 0)
        {
            return null;
        }

        var ray = CreatePerspectiveRay(position);
        return IntersectPlane(ray, Vector3.UnitY, Vector3.Zero);
    }

    private ViewportRay CreatePerspectiveRay(ViewportPoint position)
    {
        var camera = _perspectiveRenderer!.Camera;
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

    private Guid? HitTestEntity(ViewportPoint position)
    {
        if (_sceneService is null)
        {
            return null;
        }

        return _viewAxis is not null
            ? HitTestOrthographicBrush(position, _viewAxis.Value)
            : HitTestPerspectiveBrush(position);
    }

    private Guid? HitTestOrthographicBrush(ViewportPoint position, ViewAxis axis)
    {
        var worldPoint = TryGetOrthographicWorldPoint(position, axis);
        if (worldPoint is null)
        {
            return null;
        }

        for (int i = _sceneService!.Scene.Brushes.Count - 1; i >= 0; i--)
        {
            var brush = _sceneService.Scene.Brushes[i];
            if (!BrushBounds.TryGetWorldBounds(brush, out var min, out var max))
            {
                continue;
            }

            bool hit = axis switch
            {
                ViewAxis.Top => worldPoint.Value.X >= min.X && worldPoint.Value.X <= max.X && worldPoint.Value.Z >= min.Z && worldPoint.Value.Z <= max.Z,
                ViewAxis.Front => worldPoint.Value.X >= min.X && worldPoint.Value.X <= max.X && worldPoint.Value.Y >= min.Y && worldPoint.Value.Y <= max.Y,
                _ => worldPoint.Value.Y >= min.Y && worldPoint.Value.Y <= max.Y && worldPoint.Value.Z >= min.Z && worldPoint.Value.Z <= max.Z
            };

            if (hit)
            {
                return brush.Id;
            }
        }

        return null;
    }

    private Guid? HitTestPerspectiveBrush(ViewportPoint position)
    {
        if (_perspectiveRenderer is null)
        {
            return null;
        }

        var ray = CreatePerspectiveRay(position);
        Guid? hitId = null;
        float nearestDistance = float.MaxValue;

        foreach (var brush in _sceneService!.Scene.Brushes)
        {
            if (!BrushBounds.TryGetWorldBounds(brush, out var min, out var max))
            {
                continue;
            }

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

    private static bool UpdateAxis(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
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

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount < 2)
        {
            return;
        }

        LayoutToggleRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnLayoutToggleClick(object? sender, RoutedEventArgs e)
    {
        LayoutToggleRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private enum CameraDragMode
    {
        None,
        OrthographicPan,
        PerspectiveOrbit,
        PerspectivePan,
        PerspectiveZoom
    }
}
