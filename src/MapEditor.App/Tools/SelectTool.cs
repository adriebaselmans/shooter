using MapEditor.App.Services;

namespace MapEditor.App.Tools;

/// <summary>Selects entities by hit testing the active viewport.</summary>
public sealed class SelectTool : IEditorTool
{
    private readonly ResizeTool _resizeTool;
    private readonly MoveTool _moveTool;

    public SelectTool(ResizeTool resizeTool, MoveTool moveTool)
    {
        _resizeTool = resizeTool;
        _moveTool = moveTool;
    }

    public EditorToolKind Kind => EditorToolKind.Select;
    public string DisplayName => "Select";

    public void OnPointerDown(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        if (pointerEvent.IsAltPressed)
        {
            return;
        }

        if (pointerEvent.Button == ViewportPointerButton.Middle)
        {
            TryStartBrushMove(context, pointerEvent);
            return;
        }

        if (pointerEvent.Button != ViewportPointerButton.Left)
        {
            return;
        }

        if (_resizeTool.TryStartHandleDrag(context, pointerEvent))
        {
            return;
        }

        var hitEntityId = context.HitTestEntity(pointerEvent.Position);
        if (hitEntityId is null)
        {
            if (!pointerEvent.IsShiftPressed)
            {
                context.SelectionService.Clear();
                context.RefreshSelectionDetails();
            }

            return;
        }

        if (pointerEvent.IsShiftPressed)
        {
            context.SelectionService.Toggle(hitEntityId.Value);
        }
        else
        {
            context.SelectionService.SetSingle(hitEntityId.Value);
        }

        context.RefreshSelectionDetails();
        context.SetStatusMessage("Selection updated.");
    }

    public void OnPointerMove(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        if (_resizeTool.IsDragging)
        {
            _resizeTool.OnPointerMove(context, pointerEvent);
            return;
        }

        if (_moveTool.IsDragging)
        {
            _moveTool.OnPointerMove(context, pointerEvent);
        }
    }

    public void OnPointerUp(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        if (_resizeTool.IsDragging)
        {
            _resizeTool.OnPointerUp(context, pointerEvent);
            return;
        }

        if (_moveTool.IsDragging)
        {
            _moveTool.OnPointerUp(context, pointerEvent);
        }
    }

    public void Cancel()
    {
        _resizeTool.Cancel();
        _moveTool.Cancel();
    }

    public bool ShouldCaptureOrthographicMiddlePointer(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        if (_moveTool.IsDragging)
        {
            return true;
        }

        if (pointerEvent.Button != ViewportPointerButton.Middle || context.ViewAxis is null)
        {
            return false;
        }

        var selectedId = context.SelectionService.PrimarySelectionId;
        if (selectedId is null)
        {
            return false;
        }

        var hitId = context.HitTestEntity(pointerEvent.Position);
        return hitId is not null && hitId.Value == selectedId.Value;
    }

    private void TryStartBrushMove(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        var selectedId = context.SelectionService.PrimarySelectionId;
        if (selectedId is null)
        {
            return;
        }

        _moveTool.TryStartMoveDrag(context, pointerEvent, selectedId.Value);
    }
}
