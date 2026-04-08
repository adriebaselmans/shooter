using System.Numerics;
using MapEditor.Core.Commands;
using MapEditor.Core.Entities;
using MapEditor.Core;
using MapEditor.Rendering.Cameras;

namespace MapEditor.App.Tools;

/// <summary>Moves the primary selected brush in orthographic viewports using drag interaction.</summary>
public sealed class MoveTool : IEditorTool
{
    private Brush? _activeBrush;
    private Transform? _originalTransform;
    private Vector3 _dragStartWorld;
    private ViewAxis? _activeAxis;
    private EditorViewportKind? _activeViewportKind;
    private Scene? _activeScene;

    public EditorToolKind Kind => EditorToolKind.Move;
    public string DisplayName => "Move";
    public bool IsDragging =>
        _activeBrush is not null &&
        _originalTransform is not null &&
        _activeAxis is not null &&
        _activeScene is not null &&
        _activeViewportKind is not null;

    public void OnPointerDown(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        TryStartMoveDrag(context, pointerEvent, null);
    }

    public bool TryStartMoveDrag(ToolContext context, ViewportPointerEvent pointerEvent, Guid? requiredBrushId)
    {
        if ((pointerEvent.Button != ViewportPointerButton.Left &&
             pointerEvent.Button != ViewportPointerButton.Middle) ||
            context.ViewAxis is null)
        {
            return false;
        }

        var hitId = context.HitTestEntity(pointerEvent.Position);
        if (hitId is null)
        {
            return false;
        }

        if (requiredBrushId is not null && hitId.Value != requiredBrushId.Value)
        {
            return false;
        }

        var brush = context.SceneService.Scene.Brushes.FirstOrDefault(b => b.Id == hitId.Value);
        if (brush is null)
        {
            return false;
        }

        context.SelectionService.SetSingle(brush.Id);
        context.RefreshSelectionDetails();

        var startWorld = context.TryGetSnappedWorldPoint(pointerEvent.Position);
        if (startWorld is null)
        {
            return false;
        }

        _activeBrush = brush;
        _originalTransform = brush.Transform;
        _dragStartWorld = startWorld.Value;
        _activeAxis = context.ViewAxis;
        _activeViewportKind = context.ViewportKind;
        _activeScene = context.SceneService.Scene;
        context.SetStatusMessage("Dragging selection.");
        return true;
    }

    public void OnPointerMove(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        var activeBrush = _activeBrush;
        var originalTransform = _originalTransform;
        var activeAxis = _activeAxis;

        if (activeBrush is null ||
            originalTransform is null ||
            activeAxis is null ||
            _activeScene is null ||
            context.ViewportKind != _activeViewportKind)
        {
            return;
        }

        if (!_activeScene.Brushes.Contains(activeBrush))
        {
            ResetState();
            return;
        }

        var currentWorld = context.TryGetSnappedWorldPoint(pointerEvent.Position);
        if (currentWorld is null)
        {
            return;
        }

        var delta = currentWorld.Value - _dragStartWorld;
        activeBrush.Transform = ApplyVisibleAxisDelta(originalTransform, delta, activeAxis.Value);
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
            context.SetStatusMessage("Selection moved.");
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
        _activeViewportKind = null;
        _activeScene = null;
    }

    private static Transform ApplyVisibleAxisDelta(Transform original, Vector3 delta, ViewAxis axis)
    {
        var position = original.Position;
        switch (axis)
        {
            case ViewAxis.Top:
                position = new Vector3(original.Position.X + delta.X, original.Position.Y, original.Position.Z + delta.Z);
                break;
            case ViewAxis.Front:
                position = new Vector3(original.Position.X + delta.X, original.Position.Y + delta.Y, original.Position.Z);
                break;
            case ViewAxis.Side:
                position = new Vector3(original.Position.X, original.Position.Y + delta.Y, original.Position.Z + delta.Z);
                break;
        }

        return new Transform
        {
            Position = position,
            EulerDegrees = original.EulerDegrees,
            Scale = original.Scale
        };
    }
}
