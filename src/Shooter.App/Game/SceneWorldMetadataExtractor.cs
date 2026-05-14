using System.Numerics;
using MapEditor.Core;

namespace Shooter.Game;

/// <summary>Extracts spawns, pickups, and sun metadata from editor scenes.</summary>
internal static class SceneWorldMetadataExtractor
{
    public static IReadOnlyList<Vector3> ExtractPlayerSpawns(Scene scene) => scene.SpawnPoints
        .Where(spawn => spawn.SpawnType.Equals("player", StringComparison.OrdinalIgnoreCase))
        .Select(spawn => spawn.Transform.Position)
        .ToArray();

    public static IReadOnlyList<PickupSpec> ExtractPickups(Scene scene) => scene.Pickups
        .Select(pickup => new PickupSpec(pickup.Id, pickup.Kind, pickup.Transform.Position))
        .ToList();

    public static (Vector3 SunDirection, Vector3 SunColor) ResolveSun(Scene scene)
    {
        var sunDirection = Vector3.Normalize(new Vector3(-0.4f, -1.0f, -0.3f));
        var sunColor = new Vector3(1f, 0.97f, 0.9f);

        if (scene.Lights.Count == 0)
            return (sunDirection, sunColor);

        var light = scene.Lights[0];
        var rotation = MapEditor.Core.Geometry.TransformMath.BuildModelMatrix(light.Transform);
        var forward = Vector3.TransformNormal(-Vector3.UnitY, rotation);
        if (forward.LengthSquared() > 1e-5f)
            sunDirection = Vector3.Normalize(forward);
        sunColor = light.Color * MathF.Max(0.2f, light.Intensity);
        return (sunDirection, sunColor);
    }
}
