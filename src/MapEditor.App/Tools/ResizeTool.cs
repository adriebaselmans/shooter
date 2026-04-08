using MapEditor.Core.Commands;
using MapEditor.Core.Entities;
using MapEditor.Core;
using MapEditor.Rendering.Cameras;
using MapEditor.Rendering.Infrastructure;

namespace MapEditor.App.Tools;

/// <summary>Resizes the primary selected brush in orthographic viewports by dragging corner handles.</summary>
public sealed class ResizeTool
{
    private Brush? _activeBrush;
    private Transform? _originalTransform;
    private ViewAxis? _activeAxis;
    private ResizeHandleKind? _activeHandle;
    private EditorViewportKind? _activeViewportKind;
    private Scene? _activeScene;

    public bool IsDragging =>
        _activeBrush is not null &&
        _originalTransform is not null &&
        _activeAxis is not null &&
        _activeHandle is not null;

    public bool TryStartHandleDrag(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        if (IsDragging)
        {
            return false;
        }

        if (pointerEvent.Button != ViewportPointerButton.Left || context.ViewAxis is null)
        {
            return false;
        }

        var selectedId = context.SelectionService.PrimarySelectionId;
        if (selectedId is null)
        {
            return false;
        }

        var brush = context.SceneService.Scene.Brushes.FirstOrDefault(b => b.Id == selectedId.Value);
        if (brush is null)
        {
            return false;
        }

        var worldPoint = context.TryGetSnappedWorldPoint(pointerEvent.Position);
        if (worldPoint is null || context.OrthographicCamera is null)
        {
            return false;
        }

        float handleRadius = ResizeHandleMath.GetHandleSize(context.OrthographicCamera.Zoom, context.GridSize) * 1.5f;
        var handle = ResizeHandleMath.HitTestHandle(
            brush.Transform,
            context.ViewAxis.Value,
            worldPoint.Value,
            handleRadius);
        if (handle is null)
        {
            return false;
        }

        _activeBrush = brush;
        _originalTransform = brush.Transform;
        _activeAxis = context.ViewAxis;
        _activeHandle = handle.Value;
        _activeViewportKind = context.ViewportKind;
        _activeScene = context.SceneService.Scene;
        context.SetStatusMessage("Dragging resize handle.");
        return true;
    }

    public void OnPointerMove(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        var activeBrush = _activeBrush;
        var originalTransform = _originalTransform;
        var activeAxis = _activeAxis;
        var activeHandle = _activeHandle;

        if (activeBrush is null ||
            originalTransform is null ||
            activeAxis is null ||
            activeHandle is null ||
            context.ViewportKind != _activeViewportKind)
        {
            return;
        }

        if (_activeScene is null || !_activeScene.Brushes.Contains(activeBrush))
        {
            ResetState();
            return;
        }

        var worldPoint = context.TryGetSnappedWorldPoint(pointerEvent.Position);
        if (worldPoint is null)
        {
            return;
        }

        activeBrush.Transform = ResizeHandleMath.ResizeFromHandle(
            originalTransform,
            activeAxis.Value,
            activeHandle.Value,
            worldPoint.Value,
            context.GridSize);
        context.RefreshSelectionDetails();
    }

    public void OnPointerUp(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        if (_activeBrush is null || _originalTransform is null || context.ViewportKind != _activeViewportKind)
        {
            return;
        }

        if (_activeScene is null || !_activeScene.Brushes.Contains(_activeBrush))
        {
            ResetState();
            return;
        }

        var finalTransform = _activeBrush.Transform;
        if (finalTransform != _originalTransform)
        {
            context.SceneService.Execute(
                new TransformBrushCommand(context.SceneService.Scene, _activeBrush, _originalTransform, finalTransform));
            context.SetStatusMessage("Selection resized.");
        }

        ResetState();
    }

    public void Cancel()
    {
        if (_activeBrush is not null &&
            _originalTransform is not null &&
            _activeScene is not null &&
            _activeScene.Brushes.Contains(_activeBrush))
        {
            _activeBrush.Transform = _originalTransform;
        }

        ResetState();
    }

    private void ResetState()
    {
        _activeBrush = null;
        _originalTransform = null;
        _activeAxis = null;
        _activeHandle = null;
        _activeViewportKind = null;
        _activeScene = null;
    }
}
