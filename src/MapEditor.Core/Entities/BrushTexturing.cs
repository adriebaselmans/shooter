using System.Collections.ObjectModel;
using System.Numerics;

namespace MapEditor.Core.Entities;

/// <summary>Stable logical surface identifiers for supported brush primitives.</summary>
public static class BrushSurfaceIds
{
    public const string Top = "top";
    public const string Bottom = "bottom";
    public const string Front = "front";
    public const string Back = "back";
    public const string Right = "right";
    public const string Left = "left";
    public const string Side = "side";
    public const string Base = "base";
    public const string Slope = "slope";

    private static readonly IReadOnlyList<string> BoxSurfaces =
        new ReadOnlyCollection<string>([Top, Bottom, Front, Back, Right, Left]);

    private static readonly IReadOnlyList<string> CylinderSurfaces =
        new ReadOnlyCollection<string>([Top, Bottom, Side]);

    private static readonly IReadOnlyList<string> ConeSurfaces =
        new ReadOnlyCollection<string>([Base, Side]);

    private static readonly IReadOnlyList<string> WedgeSurfaces =
        new ReadOnlyCollection<string>([Front, Back, Bottom, Left, Right]);

    public static IReadOnlyList<string> GetSurfaceIds(BrushPrimitive primitive) => primitive switch
    {
        BrushPrimitive.Box => BoxSurfaces,
        BrushPrimitive.Cylinder => CylinderSurfaces,
        BrushPrimitive.Cone => ConeSurfaces,
        BrushPrimitive.Wedge => WedgeSurfaces,
        _ => Array.Empty<string>()
    };

    public static bool IsValid(BrushPrimitive primitive, string surfaceId) =>
        GetSurfaceIds(primitive).Contains(surfaceId, StringComparer.Ordinal);
}

/// <summary>Texture mapping data for a logical brush surface.</summary>
public readonly record struct SurfaceMapping(
    string TextureKey,
    Vector2 Offset,
    Vector2 Scale,
    float RotationDegrees,
    bool TextureLocked)
{
    public static SurfaceMapping Default(string textureKey) => new(
        string.IsNullOrWhiteSpace(textureKey) ? "default" : textureKey,
        Vector2.Zero,
        Vector2.One,
        0f,
        TextureLocked: true);
}
