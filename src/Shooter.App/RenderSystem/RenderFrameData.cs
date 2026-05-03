using Shooter.Game;

namespace Shooter.RenderSystem;

public sealed record RenderFrameData(
    Player Player,
    GameWorld World,
    WeaponSystem Weapons,
    PickupSystem Pickups,
    BulletHoleManager Holes,
    TracerSystem Tracers,
    RocketSystem Rockets,
    MuzzleFlash? MuzzleFlash,
    ScorchManager Scorches,
    ParticleSystem Particles,
    LightingEnvironment Lighting,
    bool ShowDebug,
    float FpsValue);
