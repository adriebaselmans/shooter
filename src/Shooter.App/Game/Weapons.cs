using System.Numerics;
using Shooter.Game;
using Shooter.Input;
using Shooter.Physics;

namespace Shooter.Game;

/// <summary>Player-held weapons. Index in this enum matches inventory slot 1/2/3 in the HUD.</summary>
public enum WeaponKind
{
    /// <summary>AK-47 assault rifle: full-auto, fast fire-rate, hit-scan.</summary>
    Ak47,
    /// <summary>Pump shotgun: slow, multi-pellet hit-scan with spread.</summary>
    Shotgun,
    /// <summary>Rocket launcher: launches a real projectile (not hit-scan).</summary>
    RocketLauncher,
}

/// <summary>How a weapon's shots are realised in the world.</summary>
public enum FireMode
{
    /// <summary>Instantaneous ray-cast (one or many pellets).</summary>
    Hitscan,
    /// <summary>Spawns a projectile that travels with finite velocity (rockets).</summary>
    Projectile,
}

public sealed class WeaponDef
{
    public required WeaponKind Kind { get; init; }
    public required string DisplayName { get; init; }
    public required int Damage { get; init; }
    public required float FireRateHz { get; init; }
    public required int AmmoMax { get; init; }
    public bool InfiniteAmmo { get; init; }
    public bool Automatic { get; init; }
    public int Pellets { get; init; } = 1;
    public float SpreadDegrees { get; init; }
    public FireMode Mode { get; init; } = FireMode.Hitscan;
    /// <summary>Projectile speed in m/s (only used when <see cref="Mode"/> = <see cref="FireMode.Projectile"/>).</summary>
    public float ProjectileSpeed { get; init; } = 30f;
    /// <summary>Splash radius for projectile detonations.</summary>
    public float SplashRadius { get; init; } = 3f;
    /// <summary>Multiplier on the viewmodel's recoil kick. 1.0 is the baseline (shotgun-ish);
    /// fast automatic weapons should use a smaller value, big weapons larger.</summary>
    public float RecoilStrength { get; init; } = 1.0f;
}

public sealed class WeaponState
{
    public required WeaponDef Def { get; init; }
    public bool Owned { get; set; }
    public int Ammo { get; set; }
    public float Cooldown { get; set; }
}

public sealed class WeaponSystem
{
    public IReadOnlyList<WeaponState> Weapons { get; }
    public int CurrentIndex { get; private set; }
    public WeaponState Current => Weapons[CurrentIndex];

    private readonly Random _rng = new();

    public WeaponSystem()
    {
        var ak47 = new WeaponDef
        {
            Kind = WeaponKind.Ak47,
            DisplayName = "AK-47",
            Damage = 18,
            FireRateHz = 9f,
            AmmoMax = 240,
            Automatic = true,
            SpreadDegrees = 1.8f,
            RecoilStrength = 0.35f,
        };
        var shotgun = new WeaponDef
        {
            Kind = WeaponKind.Shotgun,
            DisplayName = "Shotgun",
            Damage = 9,
            FireRateHz = 1.1f,
            AmmoMax = 60,
            Automatic = false,
            Pellets = 8,
            SpreadDegrees = 6f,
            RecoilStrength = 1.10f,
        };
        var rocket = new WeaponDef
        {
            Kind = WeaponKind.RocketLauncher,
            DisplayName = "Rocket Launcher",
            Damage = 100,
            FireRateHz = 0.8f,
            AmmoMax = 20,
            Automatic = false,
            Mode = FireMode.Projectile,
            ProjectileSpeed = 28f,
            SplashRadius = 3.5f,
            RecoilStrength = 1.40f,
        };

        Weapons =
        [
            new WeaponState { Def = ak47,    Owned = true, Ammo = 90 },
            new WeaponState { Def = shotgun, Owned = true, Ammo = 16 },
            new WeaponState { Def = rocket,  Owned = true, Ammo = 5 },
        ];
    }

    public void Update(float dt)
    {
        foreach (var w in Weapons) if (w.Cooldown > 0) w.Cooldown -= dt;
    }

    public void HandleSelectInput(InputState input)
    {
        if (input.WasPressed(InputKey.Num1)) TrySelect(0);
        if (input.WasPressed(InputKey.Num2)) TrySelect(1);
        if (input.WasPressed(InputKey.Num3)) TrySelect(2);
        if (input.ScrollDelta != 0)
        {
            int dir = input.ScrollDelta > 0 ? 1 : -1;
            for (int step = 0; step < Weapons.Count; step++)
            {
                int next = ((CurrentIndex + dir * (step + 1)) % Weapons.Count + Weapons.Count) % Weapons.Count;
                if (Weapons[next].Owned) { CurrentIndex = next; break; }
            }
        }
    }

    public void TrySelect(int index)
    {
        if (index < 0 || index >= Weapons.Count) return;
        if (!Weapons[index].Owned) return;
        CurrentIndex = index;
    }

    public void GiveWeapon(WeaponKind kind, int startingAmmo)
    {
        var ws = Weapons.First(w => w.Def.Kind == kind);
        ws.Owned = true;
        ws.Ammo = Math.Clamp(ws.Ammo + Math.Max(startingAmmo, 1), 0, ws.Def.AmmoMax);
    }

    public void GiveAmmo(WeaponKind kind, int amount)
    {
        var ws = Weapons.First(w => w.Def.Kind == kind);
        ws.Ammo = Math.Min(ws.Ammo + amount, ws.Def.AmmoMax);
    }

    /// <summary>Outcome of a fire attempt.
    /// <para><see cref="Hits"/> is populated for hit-scan weapons (one entry per pellet that connected).</para>
    /// <para><see cref="Projectile"/> is populated for projectile weapons (rockets); the caller is expected
    /// to spawn a projectile travelling along <c>Direction</c> from the shared muzzle anchor.</para></summary>
    public readonly record struct FireResult(
        bool Fired,
        IReadOnlyList<RayHit> Hits,
        IReadOnlyList<ShotRay> Rays,
        ProjectileSpawn? Projectile);

    /// <summary>Resolved visual ray for a hitscan shot. Used by the caller for tracers and
    /// impact feedback even when the pellet did not hit geometry.</summary>
    public readonly record struct ShotRay(Vector3 End, bool Hit);

    /// <summary>Description of a projectile to be spawned by the caller.</summary>
    public readonly record struct ProjectileSpawn(WeaponKind Owner, Vector3 Direction, float Speed, int Damage, float SplashRadius);

    /// <summary>Fires the current weapon. Returns whether it fired and per-pellet hits / projectile data.</summary>
    public FireResult TryFire(Vector3 origin, Vector3 forward, CollisionWorld col, bool triggerHeld)
    {
        var w = Current;
        if (w.Cooldown > 0) return new FireResult(false, Array.Empty<RayHit>(), Array.Empty<ShotRay>(), null);
        if (!w.Def.Automatic && !triggerHeld) return new FireResult(false, Array.Empty<RayHit>(), Array.Empty<ShotRay>(), null);
        if (!w.Def.InfiniteAmmo && w.Ammo <= 0) return new FireResult(false, Array.Empty<RayHit>(), Array.Empty<ShotRay>(), null);

        if (!w.Def.InfiniteAmmo) w.Ammo--;
        w.Cooldown = 1f / w.Def.FireRateHz;

        if (w.Def.Mode == FireMode.Projectile)
        {
            var dir = ApplySpread(forward, w.Def.SpreadDegrees);
            return new FireResult(
                true,
                Array.Empty<RayHit>(),
                Array.Empty<ShotRay>(),
                new ProjectileSpawn(w.Def.Kind, dir, w.Def.ProjectileSpeed, w.Def.Damage, w.Def.SplashRadius));
        }

        var hits = new List<RayHit>(w.Def.Pellets);
        var rays = new List<ShotRay>(w.Def.Pellets);
        for (int i = 0; i < w.Def.Pellets; i++)
        {
            var dir = ApplySpread(forward, w.Def.SpreadDegrees);
            var hit = col.RayCast(origin, dir, TracerSystem.MaxRange);
            if (hit.Hit)
            {
                hits.Add(hit);
                rays.Add(new ShotRay(hit.Point, true));
            }
            else
            {
                rays.Add(new ShotRay(origin + dir * TracerSystem.MaxRange, false));
            }
        }
        return new FireResult(true, hits, rays, null);
    }

    private Vector3 ApplySpread(Vector3 forward, float degrees)
    {
        if (degrees <= 0f) return forward;
        // Random cone offset.
        float ang = degrees * MathF.PI / 180f;
        float u = (float)_rng.NextDouble();
        float v = (float)_rng.NextDouble();
        float theta = u * 2f * MathF.PI;
        float r = MathF.Sin(ang) * MathF.Sqrt(v);
        // Build a basis around forward
        Vector3 up = MathF.Abs(forward.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, up));
        up = Vector3.Cross(right, forward);
        Vector3 offset = right * (MathF.Cos(theta) * r) + up * (MathF.Sin(theta) * r);
        return Vector3.Normalize(forward + offset);
    }
}
