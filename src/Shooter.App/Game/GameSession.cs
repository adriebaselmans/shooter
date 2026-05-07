using System.Numerics;
using MapEditor.Core;
using Shooter.Input;
using Shooter.Physics;
using Shooter.Render;

namespace Shooter.Game;

/// <summary>Owns the live gameplay/runtime state for one loaded map session.</summary>
public sealed class GameSession
{
    private readonly CollisionWorld _collision;
    private float _fpsAccum;
    private int _fpsFrames;
    private float _fpsValue;

    public GameWorld World { get; }
    public Player Player { get; }
    public WeaponSystem Weapons { get; }
    public PickupSystem Pickups { get; }
    public BulletHoleManager Holes { get; }
    public TracerSystem Tracers { get; }
    public RocketSystem Rockets { get; }
    public MuzzleFlash MuzzleFlash { get; }
    public ScorchManager Scorches { get; }
    public ParticleSystem Particles { get; }
    public LightingEnvironment Lighting { get; }
    public bool ShowDebug { get; private set; }

    private GameSession(
        GameWorld world,
        CollisionWorld collision,
        Player player,
        WeaponSystem weapons,
        PickupSystem pickups,
        BulletHoleManager holes,
        TracerSystem tracers,
        RocketSystem rockets,
        MuzzleFlash muzzleFlash,
        ScorchManager scorches,
        ParticleSystem particles,
        LightingEnvironment lighting)
    {
        World = world;
        _collision = collision;
        Player = player;
        Weapons = weapons;
        Pickups = pickups;
        Holes = holes;
        Tracers = tracers;
        Rockets = rockets;
        MuzzleFlash = muzzleFlash;
        Scorches = scorches;
        Particles = particles;
        Lighting = lighting;
    }

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

        return new GameSession(
            world,
            collision,
            player,
            new WeaponSystem(),
            new PickupSystem(world),
            new BulletHoleManager(),
            new TracerSystem(),
            new RocketSystem(),
            new MuzzleFlash(),
            new ScorchManager(),
            new ParticleSystem(),
            lighting);
    }

    public void ToggleDebug() => ShowDebug = !ShowDebug;

    public void Update(float dt, InputState input)
    {
        Player.Update(dt, input, _collision);
        Weapons.Update(dt);
        Weapons.HandleSelectInput(input);
        Pickups.Update(dt, Player, Weapons);
        Tracers.Update(dt);
        Rockets.Update(dt, _collision);
        MuzzleFlash.Update(dt);
        Particles.Update(dt);

        foreach (var rocket in Rockets.Active)
            Particles.EmitRocketTrail(rocket, dt);

        HandleRocketDetonations();
        HandleFire(input);
        UpdateFps(dt);
        input.EndFrame();
    }

    public RenderSystem.RenderFrameData CreateFrameData() => new(
        Player,
        World,
        Weapons,
        Pickups,
        Holes,
        Tracers,
        Rockets,
        MuzzleFlash,
        Scorches,
        Particles,
        Lighting,
        ShowDebug,
        _fpsValue);

    private void HandleRocketDetonations()
    {
        foreach (var det in Rockets.Detonations)
        {
            if (det.ImpactPoint is { } point && det.ImpactNormal is { } normal)
            {
                Scorches.Add(point, normal, det.SplashRadius);
                Particles.EmitExplosion(point, normal, det.SplashRadius);
            }
        }
    }

    private void HandleFire(InputState input)
    {
        bool firePressed = input.WasPressed(InputKey.MouseLeft);
        bool fireHeld = input.IsDown(InputKey.MouseLeft);
        bool wantFire = Weapons.Current.Def.Automatic ? fireHeld : firePressed;
        if (!wantFire) return;

        var origin = Player.EyePosition;
        var forward = Player.Forward();
        var right = Player.Right();
        var up = Vector3.Cross(right, forward);
        var m = WeaponViewmodelRenderer.MuzzleViewOffset;
        var muzzle = origin + right * m.X + up * m.Y + forward * (-m.Z);
        var result = Weapons.TryFire(origin, forward, _collision, triggerHeld: true);
        if (!result.Fired) return;

        var flash = Weapons.Current.Def.Kind switch
        {
            WeaponKind.Ak47 => (Offset: new Vector3(0.27f, -0.16f, -0.80f), Scale: 1.05f),
            WeaponKind.Shotgun => (Offset: new Vector3(0.20f, -0.16f, -0.62f), Scale: 1.20f),
            WeaponKind.RocketLauncher => (Offset: new Vector3(0.20f, -0.13f, -0.78f), Scale: 2.80f),
            _ => (Offset: new Vector3(0.20f, -0.16f, -0.61f), Scale: 1.0f),
        };
        MuzzleFlash.Trigger(flash.Offset, flash.Scale);
        Particles.EmitMuzzleSmoke(muzzle, forward, Weapons.Current.Def.Kind);

        if (result.Projectile is { } projectile)
        {
            Rockets.Spawn(muzzle, projectile.Direction, projectile.Speed, projectile.Damage, projectile.SplashRadius);
            return;
        }

        foreach (var hit in result.Hits)
        {
            Holes.Add(hit.Point, hit.Normal);
            Particles.EmitImpactDust(hit.Point, hit.Normal, Weapons.Current.Def.Kind);
        }

        int tracerBudget = Weapons.Current.Def.Kind switch
        {
            WeaponKind.Ak47 => 1,
            WeaponKind.Shotgun => Math.Min(3, result.Rays.Count),
            _ => Math.Min(1, result.Rays.Count),
        };
        float tracerLifetime = Weapons.Current.Def.Kind == WeaponKind.Shotgun ? 0.075f : 0.05f;
        for (int i = 0; i < tracerBudget; i++)
        {
            int rayIndex = tracerBudget == 1
                ? 0
                : (int)MathF.Round(i * (result.Rays.Count - 1) / (float)(tracerBudget - 1));
            Tracers.Add(muzzle, result.Rays[rayIndex].End, tracerLifetime);
        }
    }

    private void UpdateFps(float dt)
    {
        _fpsAccum += dt;
        _fpsFrames++;
        if (_fpsAccum >= 0.5f)
        {
            _fpsValue = _fpsFrames / _fpsAccum;
            _fpsAccum = 0f;
            _fpsFrames = 0;
        }
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
