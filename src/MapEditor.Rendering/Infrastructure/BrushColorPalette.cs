using MapEditor.Core.Entities;
using System.Numerics;

namespace MapEditor.Rendering.Infrastructure;

internal static class BrushColorPalette
{
    private static readonly Vector4 SelectionOutline = new(1.00f, 0.72f, 0.22f, 1f);

    public static Vector4 GetOrthographicFill(BrushOperation operation) => operation switch
    {
        BrushOperation.Subtractive => new Vector4(0.78f, 0.18f, 0.18f, 0.22f),
        _ => new Vector4(0.34f, 0.52f, 0.78f, 0.20f)
    };

    public static Vector4 GetOrthographicOutline(BrushOperation operation) => operation switch
    {
        BrushOperation.Subtractive => new Vector4(1.00f, 0.42f, 0.42f, 1f),
        _ => new Vector4(0.80f, 0.88f, 1.00f, 1f)
    };

    public static Vector4 GetPerspectiveSolid(BrushOperation operation)
    {
        var fill = GetOrthographicFill(operation);
        return new Vector4(fill.X, fill.Y, fill.Z, operation == BrushOperation.Subtractive ? 0.38f : 0.36f);
    }

    public static Vector4 GetWireframe(BrushOperation operation, bool selected) =>
        selected ? SelectionOutline : GetOrthographicOutline(operation);

    public static Vector4 GetSelectionOutline() => SelectionOutline;
}
