using Shooter.Input;
using Shooter.Physics;

namespace Shooter.Game;

/// <summary>Owns the live gameplay/runtime state for one loaded map session.</summary>
public sealed class GameSession
{
    private readonly CollisionWorld _collision;
    private readonly GameCombatController _combat;
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

    internal GameSession(
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
        LightingEnvironment lighting,
        GameCombatController combat)
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
        _combat = combat;
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

        _combat.UpdateTransientEffects(dt);
        _combat.HandleFire(input);
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
}
