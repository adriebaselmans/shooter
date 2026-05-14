using System.Numerics;
using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;

namespace Shooter.Game;

/// <summary>A single triangle in world space (positions + outward normal).</summary>
public readonly record struct WorldTriangle(Vector3 V0, Vector3 V1, Vector3 V2, Vector3 Normal);

/// <summary>Per-brush geometry bundle used both for rendering and collision.</summary>
public sealed class WorldBrush
{
    public required Guid BrushId { get; init; }
    public required Mesh Mesh { get; init; }                // editor mesh (model-space)
    public required Matrix4x4 Model { get; init; }          // world matrix
    public required Matrix4x4 NormalMatrix { get; init; }
    public required Vector3 BoundsMin { get; init; }
    public required Vector3 BoundsMax { get; init; }
    public required IReadOnlyList<WorldTriangle> Triangles { get; init; }
    public string MaterialName { get; init; } = "default";
    public string? TexturePath { get; init; }
    public Vector3 TintColor { get; init; } = new(0.75f, 0.75f, 0.78f);
    public BrushMaterialKind MaterialKind { get; init; } = BrushMaterialKind.Standard;
    public float Roughness { get; init; } = 0.85f;
    public float SpecularStrength { get; init; } = 0.04f;
    public float DetailNormalStrength { get; init; } = 0.0f;
    public float EmissiveStrength { get; init; } = 0.0f;
    public float Opacity { get; init; } = 1.0f;
    public Vector2 FlowSpeed { get; init; } = Vector2.Zero;
    public float DistortionStrength { get; init; } = 0.0f;
    public float FresnelStrength { get; init; } = 0.0f;
    public float PulseStrength { get; init; } = 0.0f;
}

/// <summary>Loaded game world: geometry, spawns, pickups, ambient/light info.</summary>
public sealed class GameWorld
{
    public IReadOnlyList<WorldBrush> Brushes { get; }
    public IReadOnlyList<WorldTriangle> AllTriangles { get; }
    public IReadOnlyList<Vector3> PlayerSpawns { get; }
    public Vector3 AmbientColor { get; }
    public Vector3 SkyColor { get; }
    public Vector3 SunDirection { get; }
    public Vector3 SunColor { get; }
    public IReadOnlyList<PickupSpec> Pickups { get; }

    internal GameWorld(
        IReadOnlyList<WorldBrush> brushes,
        IReadOnlyList<WorldTriangle> allTris,
        IReadOnlyList<Vector3> playerSpawns,
        Vector3 ambient,
        Vector3 sky,
        Vector3 sunDir,
        Vector3 sunColor,
        IReadOnlyList<PickupSpec> pickups)
    {
        Brushes = brushes;
        AllTriangles = allTris;
        PlayerSpawns = playerSpawns;
        AmbientColor = ambient;
        SkyColor = sky;
        SunDirection = sunDir;
        SunColor = sunColor;
        Pickups = pickups;
    }

    public static GameWorld FromScene(MapEditor.Core.Scene scene) => GameWorldFactory.FromScene(scene);
}

public readonly record struct SurfaceVisuals(float Roughness, float SpecularStrength, float DetailNormalStrength);
public readonly record struct PickupSpec(Guid Id, PickupKind Kind, Vector3 Position);
