using System.Collections.ObjectModel;
using System.Numerics;

namespace MapEditor.Core.Entities;

public enum BrushMaterialKind
{
    Standard,
    Water,
    Lava,
}

/// <summary>Authored material behavior for a brush surface family. UV scale/rotation stays in
/// <see cref="SurfaceMapping"/>; this struct controls shading/animation behavior.</summary>
public readonly record struct BrushMaterialProperties(
    BrushMaterialKind Kind,
    float Roughness,
    float SpecularStrength,
    float NormalStrength,
    float EmissiveStrength,
    float Opacity,
    Vector2 FlowSpeed,
    float DistortionStrength,
    float FresnelStrength,
    float PulseStrength)
{
    public static BrushMaterialProperties Default => Preset(BrushMaterialKind.Standard);

    public static BrushMaterialProperties Preset(BrushMaterialKind kind) => kind switch
    {
        BrushMaterialKind.Water => new(
            kind,
            Roughness: 0.08f,
            SpecularStrength: 0.55f,
            NormalStrength: 0.75f,
            EmissiveStrength: 0.00f,
            Opacity: 0.72f,
            FlowSpeed: new Vector2(0.05f, 0.02f),
            DistortionStrength: 0.06f,
            FresnelStrength: 0.70f,
            PulseStrength: 0.00f),
        BrushMaterialKind.Lava => new(
            kind,
            Roughness: 0.28f,
            SpecularStrength: 0.08f,
            NormalStrength: 0.45f,
            EmissiveStrength: 3.50f,
            Opacity: 1.00f,
            FlowSpeed: new Vector2(0.08f, 0.03f),
            DistortionStrength: 0.09f,
            FresnelStrength: 0.00f,
            PulseStrength: 0.45f),
        _ => new(
            kind,
            Roughness: 0.86f,
            SpecularStrength: 0.04f,
            NormalStrength: 0.18f,
            EmissiveStrength: 0.00f,
            Opacity: 1.00f,
            FlowSpeed: Vector2.Zero,
            DistortionStrength: 0.00f,
            FresnelStrength: 0.00f,
            PulseStrength: 0.00f),
    };
}


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
