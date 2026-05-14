using System.Numerics;
using Shooter.Input;
using Shooter.Physics;
using Shooter.Render;

namespace Shooter.Game;

/// <summary>Owns combat-feedback orchestration: firing, tracers, impacts, rocket trails, and detonations.</summary>
internal sealed class GameCombatController
{
    private readonly CollisionWorld _collision;
    private readonly Player _player;
    private readonly WeaponSystem _weapons;
    private readonly BulletHoleManager _holes;
    private readonly TracerSystem _tracers;
    private readonly RocketSystem _rockets;
    private readonly MuzzleFlash _muzzleFlash;
    private readonly ScorchManager _scorches;
    private readonly ParticleSystem _particles;

    public GameCombatController(
        CollisionWorld collision,
        Player player,
        WeaponSystem weapons,
        BulletHoleManager holes,
        TracerSystem tracers,
        RocketSystem rockets,
        MuzzleFlash muzzleFlash,
        ScorchManager scorches,
        ParticleSystem particles)
    {
        _collision = collision;
        _player = player;
        _weapons = weapons;
        _holes = holes;
        _tracers = tracers;
        _rockets = rockets;
        _muzzleFlash = muzzleFlash;
        _scorches = scorches;
        _particles = particles;
    }

    public void UpdateTransientEffects(float dt)
    {
        foreach (var rocket in _rockets.Active)
            _particles.EmitRocketTrail(rocket, dt);

        foreach (var det in _rockets.Detonations)
        {
            if (det.ImpactPoint is { } point && det.ImpactNormal is { } normal)
            {
                _scorches.Add(point, normal, det.SplashRadius);
                _particles.EmitExplosion(point, normal, det.SplashRadius);
            }
        }
    }

    public void HandleFire(InputState input)
    {
        bool firePressed = input.WasPressed(InputKey.MouseLeft);
        bool fireHeld = input.IsDown(InputKey.MouseLeft);
        bool wantFire = _weapons.Current.Def.Automatic ? fireHeld : firePressed;
        if (!wantFire) return;

        var origin = _player.EyePosition;
        var forward = _player.Forward();
        var right = _player.Right();
        var up = Vector3.Cross(right, forward);
        var muzzleAnchor = WeaponViewmodelRenderer.MuzzleViewOffset;
        var muzzle = origin + right * muzzleAnchor.X + up * muzzleAnchor.Y + forward * (-muzzleAnchor.Z);
        var result = _weapons.TryFire(origin, forward, _collision, triggerHeld: true);
        if (!result.Fired) return;

        TriggerMuzzleFeedback(muzzle, forward);

        if (result.Projectile is { } projectile)
        {
            _rockets.Spawn(muzzle, projectile.Direction, projectile.Speed, projectile.Damage, projectile.SplashRadius);
            return;
        }

        SpawnImpactFeedback();
        SpawnRepresentativeTracers(muzzle, result);

        void SpawnImpactFeedback()
        {
            foreach (var hit in result.Hits)
            {
                _holes.Add(hit.Point, hit.Normal);
                _particles.EmitImpactDust(hit.Point, hit.Normal, _weapons.Current.Def.Kind);
            }
        }
    }

    private void TriggerMuzzleFeedback(Vector3 muzzle, Vector3 forward)
    {
        var flash = _weapons.Current.Def.Kind switch
        {
            WeaponKind.Ak47 => (Offset: new Vector3(0.27f, -0.16f, -0.80f), Scale: 1.05f),
            WeaponKind.Shotgun => (Offset: new Vector3(0.20f, -0.16f, -0.62f), Scale: 1.20f),
            WeaponKind.RocketLauncher => (Offset: new Vector3(0.20f, -0.13f, -0.78f), Scale: 2.80f),
            _ => (Offset: new Vector3(0.20f, -0.16f, -0.61f), Scale: 1.0f),
        };

        _muzzleFlash.Trigger(flash.Offset, flash.Scale);
        _particles.EmitMuzzleSmoke(muzzle, forward, _weapons.Current.Def.Kind);
    }

    private void SpawnRepresentativeTracers(Vector3 muzzle, WeaponSystem.FireResult result)
    {
        int tracerBudget = _weapons.Current.Def.Kind switch
        {
            WeaponKind.Ak47 => 1,
            WeaponKind.Shotgun => Math.Min(3, result.Rays.Count),
            _ => Math.Min(1, result.Rays.Count),
        };
        float tracerLifetime = _weapons.Current.Def.Kind == WeaponKind.Shotgun ? 0.075f : 0.05f;
        for (int i = 0; i < tracerBudget; i++)
        {
            int rayIndex = tracerBudget == 1
                ? 0
                : (int)MathF.Round(i * (result.Rays.Count - 1) / (float)(tracerBudget - 1));
            _tracers.Add(muzzle, result.Rays[rayIndex].End, tracerLifetime);
        }
    }
}
