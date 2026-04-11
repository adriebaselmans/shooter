using System.Numerics;
using MapEditor.Core;
using MapEditor.Core.Commands;
using MapEditor.Core.Entities;
using MapEditor.Rendering.Cameras;

namespace MapEditor.App.Tools;

/// <summary>Creates brushes by click-dragging in orthographic viewports.</summary>
public sealed class CreateBrushTool : IEditorTool
{
    private readonly SceneService _sceneService;
    private Brush? _activeBrush;
    private Vector3 _dragStartWorld;
    private ViewAxis? _activeAxis;

    public CreateBrushTool(SceneService sceneService)
    {
        _sceneService = sceneService;
    }

    public EditorToolKind Kind => EditorToolKind.CreateBrush;
    public string DisplayName => "Create Brush";

    public void OnPointerDown(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        if (pointerEvent.Button != ViewportPointerButton.Left)
        {
            return;
        }

        if (context.ViewAxis is null)
        {
            context.SetStatusMessage("Create Brush works in Top, Front, or Side viewports.");
            return;
        }

        var startWorld = context.TryGetSnappedWorldPoint(pointerEvent.Position);
        if (startWorld is null)
        {
            return;
        }

        _dragStartWorld = startWorld.Value;
        _activeAxis = context.ViewAxis;
        _activeBrush = CreateBrush(context, _dragStartWorld, _dragStartWorld);

        context.SceneService.Execute(new CreateBrushCommand(context.SceneService.Scene, _activeBrush));
        context.SelectionService.SetSingle(_activeBrush.Id);
        context.RefreshSelectionDetails();
        context.SetStatusMessage("Dragging to size new brush.");
    }

    public void OnPointerMove(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        if (_activeBrush is null || _activeAxis is null)
        {
            return;
        }

        var currentWorld = context.TryGetSnappedWorldPoint(pointerEvent.Position);
        if (currentWorld is null)
        {
            return;
        }

        _activeBrush.Transform = BuildTransform(_activeAxis.Value, _dragStartWorld, currentWorld.Value, context.GridSize);
        context.RefreshSelectionDetails();
    }

    public void OnPointerUp(ToolContext context, ViewportPointerEvent pointerEvent)
    {
        if (_activeBrush is null || _activeAxis is null)
        {
            return;
        }

        var visibleSize = GetVisiblePlaneSize(_activeBrush.Transform.Scale, _activeAxis.Value);
        if (visibleSize.X < context.GridSize || visibleSize.Y < context.GridSize)
        {
            context.SceneService.Undo();
            context.SelectionService.Clear();
            context.RefreshSelectionDetails();
            context.SetStatusMessage("Brush creation canceled.");
        }
        else
        {
            context.SetStatusMessage("Brush created.");
        }

        _activeBrush = null;
        _activeAxis = null;
        context.SetActiveTool(EditorToolKind.Select);
    }

    public void Cancel()
    {
        if (_activeBrush is not null && _sceneService.Scene.Brushes.Contains(_activeBrush))
        {
            _sceneService.Undo();
        }

        _activeBrush = null;
        _activeAxis = null;
    }

    private static Brush CreateBrush(ToolContext context, Vector3 startWorld, Vector3 endWorld)
    {
        return new Brush
        {
            Name = $"{context.SelectedBrushPrimitive} Brush",
            Primitive = context.SelectedBrushPrimitive,
            Operation = context.SelectedBrushOperation,
            Transform = BuildTransform(context.ViewAxis!.Value, startWorld, endWorld, context.GridSize)
        };
    }

    private static Transform BuildTransform(ViewAxis axis, Vector3 startWorld, Vector3 endWorld, float gridSize)
    {
        var min = Vector3.Min(startWorld, endWorld);
        var max = Vector3.Max(startWorld, endWorld);
        var thickness = MathF.Max(gridSize, 1f);

        return axis switch
        {
            ViewAxis.Top => new Transform
            {
                Position = new Vector3((min.X + max.X) * 0.5f, thickness * 0.5f, (min.Z + max.Z) * 0.5f),
                EulerDegrees = Vector3.Zero,
                Scale = new Vector3(MathF.Max(max.X - min.X, thickness), thickness, MathF.Max(max.Z - min.Z, thickness))
            },
            ViewAxis.Front => new Transform
            {
                Position = new Vector3((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f, thickness * 0.5f),
                EulerDegrees = Vector3.Zero,
                Scale = new Vector3(MathF.Max(max.X - min.X, thickness), MathF.Max(max.Y - min.Y, thickness), thickness)
            },
            _ => new Transform
            {
                Position = new Vector3(thickness * 0.5f, (min.Y + max.Y) * 0.5f, (min.Z + max.Z) * 0.5f),
                EulerDegrees = Vector3.Zero,
                Scale = new Vector3(thickness, MathF.Max(max.Y - min.Y, thickness), MathF.Max(max.Z - min.Z, thickness))
            }
        };
    }

    private static Vector2 GetVisiblePlaneSize(Vector3 scale, ViewAxis axis) => axis switch
    {
        ViewAxis.Top => new Vector2(scale.X, scale.Z),
        ViewAxis.Front => new Vector2(scale.X, scale.Y),
        _ => new Vector2(scale.Y, scale.Z)
    };
}
