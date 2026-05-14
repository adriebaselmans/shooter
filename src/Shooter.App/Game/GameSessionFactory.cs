using System.Numerics;
using MapEditor.Core;
using Shooter.Physics;

namespace Shooter.Game;

/// <summary>Builds a fully initialized GameSession from scene data.</summary>
public static class GameSessionFactory
{
    public static GameSession Create(Scene scene)
    {
        var world = GameWorld.FromScene(scene);
        var lighting = new LightingEnvironment();
        ApplyWorldLightingDefaults(world, lighting);

        var collision = new CollisionWorld(world);
        var player = new Player();
        var spawnPoint = world.PlayerSpawns.Count > 0
            ? world.PlayerSpawns[Random.Shared.Next(world.PlayerSpawns.Count)]
            : Vector3.Zero;
        player.Position = SnapSpawnToFloor(spawnPoint, collision);

        var weapons = new WeaponSystem();
        var pickups = new PickupSystem(world);
        var holes = new BulletHoleManager();
        var tracers = new TracerSystem();
        var rockets = new RocketSystem();
        var muzzleFlash = new MuzzleFlash();
        var scorches = new ScorchManager();
        var particles = new ParticleSystem();
        var combat = new GameCombatController(collision, player, weapons, holes, tracers, rockets, muzzleFlash, scorches, particles);

        return new GameSession(
            world,
            collision,
            player,
            weapons,
            pickups,
            holes,
            tracers,
            rockets,
            muzzleFlash,
            scorches,
            particles,
            lighting,
            combat);
    }

    private static Vector3 SnapSpawnToFloor(Vector3 spawnPoint, CollisionWorld collision)
    {
        var rayOrigin = spawnPoint + new Vector3(0f, 2f, 0f);
        var hit = collision.RayCast(rayOrigin, -Vector3.UnitY);
        if (hit.Hit && hit.Distance < 200f)
            return new Vector3(spawnPoint.X, hit.Point.Y + Player.Radius + 0.02f, spawnPoint.Z);
        return spawnPoint + new Vector3(0f, Player.Radius + 0.05f, 0f);
    }

    private static void ApplyWorldLightingDefaults(GameWorld world, LightingEnvironment lighting)
    {
        lighting.SunDirection = Vector3.Normalize(world.SunDirection);

        float peak = MathF.Max(world.SunColor.X, MathF.Max(world.SunColor.Y, world.SunColor.Z));
        if (peak > 1e-4f)
        {
            lighting.SunColor = world.SunColor / peak;
            lighting.SunIntensity = peak;
        }
    }
}
