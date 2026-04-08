using System.Numerics;
using MapEditor.Core.Entities;
using MapEditor.Core;
using MapEditor.Formats.Dto;

namespace MapEditor.Formats;

/// <summary>
/// Bidirectional mapping between <see cref="Scene"/> and <see cref="MapDto"/>.
/// No file I/O — pure data transformation.
/// </summary>
public static class MapSerializer
{
    /// <summary>Converts a <see cref="MapDto"/> to a <see cref="Scene"/>.</summary>
    public static Scene ToScene(MapDto dto)
    {
        var scene = new Scene
        {
            AdditionalData = CloneExtensionData(dto.AdditionalData)
        };

        var ws = dto.WorldSettings;
        scene.WorldSettings.AmbientColor = ToVec3(ws.AmbientColor, Vector3.Zero);
        scene.WorldSettings.SkyColor     = ToVec3(ws.SkyColor, new Vector3(0.2f, 0.3f, 0.4f));

        foreach (var b in dto.Brushes)
        {
            var brush = new Brush(ParseGuid(b.Id), b.Name)
            {
                Operation    = ParseOperation(b.Operation),
                Primitive    = ParsePrimitive(b.PrimitiveType),
                Transform    = ToTransform(b.Transform),
                MaterialName = b.MaterialName
            };
            scene.AddBrush(brush);
        }

        foreach (var l in dto.Lights)
        {
            var light = new LightEntity(ParseGuid(l.Id), l.Name)
            {
                LightType = ParseLightType(l.LightType),
                Transform = ToTransform(l.Transform),
                Color     = ToVec3(l.Color, Vector3.One),
                Intensity = l.Intensity,
                Range     = l.Range,
                ConeAngle = l.ConeAngle ?? 30.0f
            };
            scene.AddLight(light);
        }

        foreach (var sp in dto.SpawnPoints)
        {
            var spawnPoint = new SpawnPoint(ParseGuid(sp.Id), sp.Name)
            {
                Transform = ToTransform(sp.Transform),
                SpawnType = sp.SpawnType
            };
            scene.AddSpawnPoint(spawnPoint);
        }

        return scene;
    }

    /// <summary>Converts a <see cref="Scene"/> to a <see cref="MapDto"/>.</summary>
    public static MapDto FromScene(Scene scene)
    {
        var dto = new MapDto
        {
            AdditionalData = CloneExtensionData(scene.AdditionalData),
            WorldSettings = new WorldSettingsDto
            {
                AmbientColor = FromVec3(scene.WorldSettings.AmbientColor),
                SkyColor     = FromVec3(scene.WorldSettings.SkyColor)
            }
        };

        foreach (var b in scene.Brushes)
        {
            dto.Brushes.Add(new BrushDto
            {
                Id           = b.Id.ToString(),
                Name         = b.Name,
                Operation    = b.Operation == BrushOperation.Additive ? "additive" : "subtractive",
                PrimitiveType = b.Primitive.ToString().ToLowerInvariant(),
                Transform    = FromTransform(b.Transform),
                MaterialName = b.MaterialName
            });
        }

        foreach (var l in scene.Lights)
        {
            var ld = new LightDto
            {
                Id        = l.Id.ToString(),
                Name      = l.Name,
                LightType = l.LightType == Core.Entities.LightType.Point ? "point" : "spot",
                Transform = FromTransform(l.Transform),
                Color     = FromVec3(l.Color),
                Intensity = l.Intensity,
                Range     = l.Range
            };
            if (l.LightType == Core.Entities.LightType.Spot)
                ld.ConeAngle = l.ConeAngle;
            dto.Lights.Add(ld);
        }

        foreach (var sp in scene.SpawnPoints)
        {
            dto.SpawnPoints.Add(new Dto.SpawnPointDto
            {
                Id        = sp.Id.ToString(),
                Name      = sp.Name,
                Transform = FromTransform(sp.Transform),
                SpawnType = sp.SpawnType
            });
        }

        return dto;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Transform ToTransform(TransformDto t) => new()
    {
        Position     = ToVec3(t.Position, Vector3.Zero),
        EulerDegrees = ToVec3(t.Rotation, Vector3.Zero),
        Scale        = ToVec3(t.Scale, Vector3.One)
    };

    private static TransformDto FromTransform(Transform t) => new()
    {
        Position = FromVec3(t.Position),
        Rotation = FromVec3(t.EulerDegrees),
        Scale    = FromVec3(t.Scale)
    };

    private static Vector3 ToVec3(float[]? arr, Vector3 fallback) =>
        arr is { Length: >= 3 }
            ? new Vector3(arr[0], arr[1], arr[2])
            : fallback;

    private static float[] FromVec3(Vector3 v) => [v.X, v.Y, v.Z];

    private static Dictionary<string, System.Text.Json.JsonElement>? CloneExtensionData(
        Dictionary<string, System.Text.Json.JsonElement>? source)
    {
        if (source is null || source.Count == 0)
        {
            return null;
        }

        var clone = new Dictionary<string, System.Text.Json.JsonElement>(source.Count);
        foreach (var (key, value) in source)
        {
            clone[key] = value.Clone();
        }

        return clone;
    }

    private static Guid ParseGuid(string s) =>
        Guid.TryParse(s, out var g) ? g : Guid.NewGuid();

    private static BrushOperation ParseOperation(string s) =>
        s.Equals("subtractive", StringComparison.OrdinalIgnoreCase)
            ? BrushOperation.Subtractive
            : BrushOperation.Additive;

    private static BrushPrimitive ParsePrimitive(string s) =>
        s.ToLowerInvariant() switch
        {
            "cylinder" => BrushPrimitive.Cylinder,
            "cone"     => BrushPrimitive.Cone,
            "wedge"    => BrushPrimitive.Wedge,
            _          => BrushPrimitive.Box
        };

    private static Core.Entities.LightType ParseLightType(string s) =>
        s.Equals("spot", StringComparison.OrdinalIgnoreCase)
            ? Core.Entities.LightType.Spot
            : Core.Entities.LightType.Point;
}
