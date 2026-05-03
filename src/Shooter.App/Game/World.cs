using System.Numerics;
using MapEditor.Core;
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

    private GameWorld(
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

    public static GameWorld FromScene(Scene scene)
    {
        var brushes = new List<WorldBrush>(scene.Brushes.Count);
        var allTris = new List<WorldTriangle>(2048);

        foreach (var brush in scene.Brushes)
        {
            var mesh = MeshGenerator.GenerateMesh(brush);
            var model = TransformMath.BuildModelMatrix(brush.Transform);
            Matrix4x4.Invert(model, out var inv);
            var normalMat = Matrix4x4.Transpose(inv);

            var tris = ExtractWorldTriangles(mesh, model, normalMat);
            var (bmin, bmax) = ComputeBounds(tris);

            var texturePath = ResolveMaterialTexturePath(brush.MaterialName);
            var visuals = InferSurfaceVisuals(brush.MaterialName, texturePath);
            var authored = brush.MaterialProperties;
            bool useAuthored = authored.Kind != BrushMaterialKind.Standard || authored != BrushMaterialProperties.Default;
            var wb = new WorldBrush
            {
                BrushId = brush.Id,
                Mesh = mesh,
                Model = model,
                NormalMatrix = normalMat,
                BoundsMin = bmin - new Vector3(0.01f),
                BoundsMax = bmax + new Vector3(0.01f),
                Triangles = tris,
                MaterialName = brush.MaterialName,
                TexturePath = texturePath,
                TintColor = ColorFromMaterial(brush.MaterialName, texturePath),
                MaterialKind = useAuthored ? authored.Kind : BrushMaterialKind.Standard,
                Roughness = useAuthored ? authored.Roughness : visuals.Roughness,
                SpecularStrength = useAuthored ? authored.SpecularStrength : visuals.SpecularStrength,
                DetailNormalStrength = useAuthored ? authored.NormalStrength : visuals.DetailNormalStrength,
                EmissiveStrength = useAuthored ? authored.EmissiveStrength : 0.0f,
                Opacity = useAuthored ? authored.Opacity : 1.0f,
                FlowSpeed = useAuthored ? authored.FlowSpeed : Vector2.Zero,
                DistortionStrength = useAuthored ? authored.DistortionStrength : 0.0f,
                FresnelStrength = useAuthored ? authored.FresnelStrength : 0.0f,
                PulseStrength = useAuthored ? authored.PulseStrength : 0.0f,
            };
            brushes.Add(wb);
            allTris.AddRange(tris);
        }

        var spawns = scene.SpawnPoints
            .Where(s => s.SpawnType.Equals("player", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Transform.Position)
            .ToArray();

        // Light: use first scene light direction if available, else default top-down sun.
        var sunDir = Vector3.Normalize(new Vector3(-0.4f, -1.0f, -0.3f));
        var sunColor = new Vector3(1f, 0.97f, 0.9f);
        if (scene.Lights.Count > 0)
        {
            var l = scene.Lights[0];
            // Lights have no explicit direction in current schema; derive from euler.
            var rot = TransformMath.BuildModelMatrix(l.Transform);
            var fwd = Vector3.TransformNormal(-Vector3.UnitY, rot);
            if (fwd.LengthSquared() > 1e-5f) sunDir = Vector3.Normalize(fwd);
            sunColor = l.Color * MathF.Max(0.2f, l.Intensity);
        }

        var pickupList = scene.Pickups
            .Select(p => new PickupSpec(p.Id, p.Kind, p.Transform.Position))
            .ToList();

        return new GameWorld(
            brushes, allTris, spawns,
            scene.WorldSettings.AmbientColor,
            scene.WorldSettings.SkyColor,
            sunDir, sunColor, pickupList);
    }

    private static IReadOnlyList<WorldTriangle> ExtractWorldTriangles(Mesh mesh, Matrix4x4 model, Matrix4x4 normalMat)
    {
        var verts = mesh.Vertices;
        var idx = mesh.Indices;
        var tris = new WorldTriangle[idx.Length / 3];
        for (int t = 0; t < tris.Length; t++)
        {
            int i0 = (int)idx[t * 3 + 0] * Mesh.FloatsPerVertex;
            int i1 = (int)idx[t * 3 + 1] * Mesh.FloatsPerVertex;
            int i2 = (int)idx[t * 3 + 2] * Mesh.FloatsPerVertex;
            var v0 = Vector3.Transform(new Vector3(verts[i0], verts[i0 + 1], verts[i0 + 2]), model);
            var v1 = Vector3.Transform(new Vector3(verts[i1], verts[i1 + 1], verts[i1 + 2]), model);
            var v2 = Vector3.Transform(new Vector3(verts[i2], verts[i2 + 1], verts[i2 + 2]), model);
            var n = Vector3.Cross(v1 - v0, v2 - v0);
            var len = n.Length();
            n = len > 1e-6f ? n / len : Vector3.UnitY;
            tris[t] = new WorldTriangle(v0, v1, v2, n);
        }
        return tris;
    }

    private static (Vector3 min, Vector3 max) ComputeBounds(IReadOnlyList<WorldTriangle> tris)
    {
        if (tris.Count == 0) return (Vector3.Zero, Vector3.Zero);
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var t in tris)
        {
            min = Vector3.Min(min, Vector3.Min(t.V0, Vector3.Min(t.V1, t.V2)));
            max = Vector3.Max(max, Vector3.Max(t.V0, Vector3.Max(t.V1, t.V2)));
        }
        return (min, max);
    }

    private static string? ResolveMaterialTexturePath(string materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName)) return null;
        string ext = Path.GetExtension(materialName);
        if (!IsImageExtension(ext)) return null;

        // Absolute path explicitly embedded in the map.
        if (Path.IsPathRooted(materialName) && File.Exists(materialName))
            return materialName;

        // Repo assets-relative path, e.g. textures/dust/ground_sand_1k.jpg.
        string assetRelative = Path.Combine(global::Shooter.AssetLocator.Root, materialName);
        if (File.Exists(assetRelative)) return assetRelative;

        // Map-relative / cwd-relative path for portability when launching from repo root.
        string cwdRelative = Path.GetFullPath(materialName, Directory.GetCurrentDirectory());
        if (File.Exists(cwdRelative)) return cwdRelative;

        return null;
    }

    private static bool IsImageExtension(string ext) =>
        ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase);

    private static Vector3 ColorFromMaterial(string name, string? texturePath)
    {
        if (!string.IsNullOrWhiteSpace(texturePath))
            return Vector3.One; // textured brushes should show their imported albedo unchanged
        if (string.IsNullOrEmpty(name) || name == "default")
            return new Vector3(0.78f, 0.78f, 0.82f);
        unchecked
        {
            int h = 17;
            foreach (var c in name) h = h * 31 + c;
            float r = 0.55f + ((h & 0xFF) / 255f) * 0.4f;
            float g = 0.55f + (((h >> 8) & 0xFF) / 255f) * 0.4f;
            float b = 0.55f + (((h >> 16) & 0xFF) / 255f) * 0.4f;
            return new Vector3(r, g, b);
        }
    }

    private static SurfaceVisuals InferSurfaceVisuals(string materialName, string? texturePath)
    {
        string key = $"{materialName} {texturePath}".ToLowerInvariant();
        var result = new SurfaceVisuals(0.86f, 0.04f, 0.0f);

        if (!string.IsNullOrWhiteSpace(texturePath))
            result = result with { DetailNormalStrength = 0.18f };

        if (key.Contains("sand"))
            return new SurfaceVisuals(0.97f, 0.02f, 0.16f);
        if (key.Contains("plaster"))
            return new SurfaceVisuals(0.90f, 0.04f, 0.22f);
        if (key.Contains("stone") || key.Contains("brick"))
            return new SurfaceVisuals(0.84f, 0.05f, 0.20f);
        if (key.Contains("concrete"))
            return new SurfaceVisuals(0.84f, 0.07f, 0.30f);
        if (key.Contains("wood") || key.Contains("plank") || key.Contains("trim"))
            return new SurfaceVisuals(0.72f, 0.10f, 0.26f);
        if (key.Contains("awning"))
            return new SurfaceVisuals(0.68f, 0.08f, 0.12f);
        return result;
    }
}

public readonly record struct SurfaceVisuals(float Roughness, float SpecularStrength, float DetailNormalStrength);
public readonly record struct PickupSpec(Guid Id, PickupKind Kind, Vector3 Position);
