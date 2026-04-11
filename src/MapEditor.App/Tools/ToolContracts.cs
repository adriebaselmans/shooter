using System.Numerics;
using System.Windows;
using System.Windows.Input;
using MapEditor.App.Services;
using MapEditor.App.ViewModels;
using MapEditor.Core;
using MapEditor.Core.Entities;
using MapEditor.Rendering.Cameras;

namespace MapEditor.App.Tools;

/// <summary>Logical viewport identity used by editor tools.</summary>
public enum EditorViewportKind
{
    Top,
    Front,
    Side,
    Perspective
}

/// <summary>Normalized pointer action kinds emitted by the viewport host.</summary>
public enum ViewportPointerAction
{
    Down,
    Move,
    Up,
    Wheel,
    Leave
}

/// <summary>Normalized pointer buttons emitted by the viewport host.</summary>
public enum ViewportPointerButton
{
    None,
    Left,
    Middle,
    Right
}

/// <summary>Normalized pointer event for viewport interaction logic.</summary>
public readonly record struct ViewportPointerEvent(
    ViewportPointerAction Action,
    ViewportPointerButton Button,
    Point Position,
    int WheelDelta,
    ModifierKeys Modifiers)
{
    public bool IsAltPressed => Modifiers.HasFlag(ModifierKeys.Alt);
    public bool IsShiftPressed => Modifiers.HasFlag(ModifierKeys.Shift);
    public bool IsControlPressed => Modifiers.HasFlag(ModifierKeys.Control);
}

/// <summary>Simple world-space ray used for hit testing.</summary>
public readonly record struct ViewportRay(Vector3 Origin, Vector3 Direction);

/// <summary>
/// Shared context passed to all editor tools for the currently active viewport interaction.
/// </summary>
public sealed class ToolContext
{
    public required SceneService SceneService { get; init; }
    public required SelectionService SelectionService { get; init; }
    public required StatusBarViewModel StatusBarViewModel { get; init; }
    public required EditorViewportKind ViewportKind { get; init; }
    public required int PixelWidth { get; init; }
    public required int PixelHeight { get; init; }
    public required float GridSize { get; init; }
    public required BrushPrimitive SelectedBrushPrimitive { get; init; }
    public required BrushOperation SelectedBrushOperation { get; init; }
    public required Func<Point, Vector3?> TryGetWorldPoint { get; init; }
    public required Func<Point, Vector3?> TryGetSnappedWorldPoint { get; init; }
    public required Func<Point, Guid?> HitTestEntity { get; init; }
    public required Action RefreshSelectionDetails { get; init; }
    public required Action<string> SetStatusMessage { get; init; }
    public required Action<EditorToolKind> SetActiveTool { get; init; }
    public OrthographicCamera? OrthographicCamera { get; init; }
    public PerspectiveCamera? PerspectiveCamera { get; init; }
    public ViewAxis? ViewAxis { get; init; }
}

/// <summary>Tool interface used by viewport interaction handling.</summary>
public interface IEditorTool
{
    EditorToolKind Kind { get; }
    string DisplayName { get; }
    void OnPointerDown(ToolContext context, ViewportPointerEvent pointerEvent);
    void OnPointerMove(ToolContext context, ViewportPointerEvent pointerEvent);
    void OnPointerUp(ToolContext context, ViewportPointerEvent pointerEvent);
    void Cancel();
}

/// <summary>Known editor tools for iteration 1 interaction workflows.</summary>
public enum EditorToolKind
{
    Select,
    CreateBrush,
    Move
}

internal static class GridSnapper
{
    public static float Snap(float value, float gridSize) =>
        gridSize <= 0f ? value : MathF.Round(value / gridSize) * gridSize;

    public static Vector3 Snap(Vector3 value, float gridSize) => new(
        Snap(value.X, gridSize),
        Snap(value.Y, gridSize),
        Snap(value.Z, gridSize));
}
