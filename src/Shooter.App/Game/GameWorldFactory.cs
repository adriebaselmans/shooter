using MapEditor.Core;

namespace Shooter.Game;

/// <summary>Builds runtime GameWorld instances from editor scenes.</summary>
internal static class GameWorldFactory
{
    public static GameWorld FromScene(Scene scene)
    {
        var brushes = new List<WorldBrush>(scene.Brushes.Count);
        var allTriangles = new List<WorldTriangle>(2048);

        foreach (var brush in scene.Brushes)
        {
            var worldBrush = WorldBrushFactory.Build(brush);
            brushes.Add(worldBrush);
            allTriangles.AddRange(worldBrush.Triangles);
        }

        var spawns = SceneWorldMetadataExtractor.ExtractPlayerSpawns(scene);
        var (sunDirection, sunColor) = SceneWorldMetadataExtractor.ResolveSun(scene);
        var pickups = SceneWorldMetadataExtractor.ExtractPickups(scene);

        return new GameWorld(
            brushes,
            allTriangles,
            spawns,
            scene.WorldSettings.AmbientColor,
            scene.WorldSettings.SkyColor,
            sunDirection,
            sunColor,
            pickups);
    }
}
