using System.Numerics;
using MapEditor.Core;
using MapEditor.Core.Commands;
using MapEditor.Core.Entities;

namespace Shooter.Game;

/// <summary>Builds the small procedural fallback arena used when no map file is available.</summary>
public static class FallbackSceneFactory
{
    public static Scene Build()
    {
        var scene = new Scene();

        static Brush Box(string name, Vector3 center, Vector3 size, float pitchDeg = 0f)
        {
            return new Brush(Guid.NewGuid(), name)
            {
                Transform = Transform.Identity with
                {
                    Position = center,
                    Scale = size,
                    EulerDegrees = new Vector3(pitchDeg, 0f, 0f),
                },
            };
        }

        AddBrush(scene, Box("Floor", new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60)));
        AddBrush(scene, Box("WallN", new Vector3(0, 2.5f, -30f), new Vector3(60, 5, 1)));
        AddBrush(scene, Box("WallS", new Vector3(0, 2.5f, 30f), new Vector3(60, 5, 1)));
        AddBrush(scene, Box("WallE", new Vector3(30f, 2.5f, 0), new Vector3(1, 5, 60)));
        AddBrush(scene, Box("WallW", new Vector3(-30f, 2.5f, 0), new Vector3(1, 5, 60)));

        AddBrush(scene, Box("CentralPlatform", new Vector3(0, 0.8f, 0), new Vector3(8, 1.6f, 8)));
        AddBrush(scene, Box("RampN", new Vector3(0, 0.8f, -7f), new Vector3(3, 0.4f, 5.4f), pitchDeg: -17f));
        AddBrush(scene, Box("RampS", new Vector3(0, 0.8f, 7f), new Vector3(3, 0.4f, 5.4f), pitchDeg: 17f));

        AddBrush(scene, Box("PlatformE", new Vector3(11, 1.0f, -10), new Vector3(5, 2.0f, 5)));
        AddBrush(scene, Box("PlatformW", new Vector3(-11, 1.0f, 10), new Vector3(5, 2.0f, 5)));

        AddBrush(scene, Box("ColumnA", new Vector3(8, 1.5f, 8), new Vector3(1.2f, 3, 1.2f)));
        AddBrush(scene, Box("ColumnB", new Vector3(-8, 1.5f, 8), new Vector3(1.2f, 3, 1.2f)));
        AddBrush(scene, Box("ColumnC", new Vector3(8, 1.5f, -8), new Vector3(1.2f, 3, 1.2f)));
        AddBrush(scene, Box("ColumnD", new Vector3(-8, 1.5f, -8), new Vector3(1.2f, 3, 1.2f)));

        AddBrush(scene, Box("CrateE1", new Vector3(15, 0.6f, 0), new Vector3(2, 1.2f, 2)));
        AddBrush(scene, Box("CrateE2", new Vector3(18, 0.6f, 4), new Vector3(2, 1.2f, 2)));
        AddBrush(scene, Box("CrateW1", new Vector3(-15, 0.6f, 0), new Vector3(2, 1.2f, 2)));
        AddBrush(scene, Box("CrateW2", new Vector3(-18, 0.6f, -4), new Vector3(2, 1.2f, 2)));

        AddBrush(scene, Box("AlleyN", new Vector3(-2.5f, 0.75f, 18), new Vector3(7, 1.5f, 0.6f)));
        AddBrush(scene, Box("AlleyS", new Vector3(2.5f, 0.75f, 22), new Vector3(7, 1.5f, 0.6f)));

        AddSpawn(scene, new SpawnPoint(Guid.NewGuid(), "Spawn")
        {
            SpawnType = "player",
            Transform = Transform.Identity with { Position = new Vector3(0, 0.1f, 20) },
        });

        AddPickup(scene, PickupKind.HealthSmall, new Vector3(11, 2.3f, -10));
        AddPickup(scene, PickupKind.HealthLarge, new Vector3(0, 1.9f, 0));
        AddPickup(scene, PickupKind.AmmoAk47, new Vector3(15, 1.4f, 0));
        AddPickup(scene, PickupKind.AmmoShotgun, new Vector3(-15, 1.4f, 0));
        AddPickup(scene, PickupKind.AmmoRocket, new Vector3(-11, 2.3f, 10));
        AddPickup(scene, PickupKind.WeaponShotgun, new Vector3(-2.5f, 1.6f, 18));
        AddPickup(scene, PickupKind.WeaponRocketLauncher, new Vector3(0, 0.5f, -22));

        return scene;
    }

    private static void AddBrush(Scene scene, Brush brush) => new CreateBrushCommand(scene, brush).Execute();
    private static void AddSpawn(Scene scene, SpawnPoint spawn) => new CreateSpawnPointCommand(scene, spawn).Execute();
    private static void AddPickup(Scene scene, PickupKind kind, Vector3 position) =>
        new CreatePickupCommand(scene, new PickupEntity(Guid.NewGuid(), kind.ToString())
        {
            Kind = kind,
            Transform = Transform.Identity with { Position = position },
        }).Execute();
}
