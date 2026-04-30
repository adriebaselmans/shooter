using System.Numerics;
using MapEditor.Core.Entities;

namespace Shooter.Game;

public sealed class PickupRuntime
{
    public required Guid Id { get; init; }
    public required PickupKind Kind { get; init; }
    public Vector3 Position;
    public bool Active = true;
}

public sealed class PickupSystem
{
    public List<PickupRuntime> Active { get; } = new();
    public const float OverlapRadius = 0.7f;
    public const float RotateSpeedRadPerSec = MathF.PI / 2f;
    public float SpinAngle;

    public PickupSystem(GameWorld world)
    {
        foreach (var p in world.Pickups)
        {
            Active.Add(new PickupRuntime { Id = p.Id, Kind = p.Kind, Position = p.Position });
        }
    }

    public void Update(float dt, Player player, WeaponSystem weapons)
    {
        SpinAngle += RotateSpeedRadPerSec * dt;
        var eye = player.Position;
        float r2 = OverlapRadius + Player.Radius;
        r2 *= r2;
        foreach (var p in Active)
        {
            if (!p.Active) continue;
            // 3D distance squared (with vertical leniency).
            var d = p.Position - eye;
            if (d.LengthSquared() < r2)
            {
                ApplyEffect(p, player, weapons);
                p.Active = false;
            }
        }
        Active.RemoveAll(p => !p.Active);
    }

    public static Vector3 ColorFor(PickupKind k) => k switch
    {
        PickupKind.HealthSmall => new Vector3(0.30f, 1.00f, 0.30f),
        PickupKind.HealthLarge => new Vector3(0.10f, 0.85f, 0.10f),
        PickupKind.AmmoAk47 => new Vector3(0.95f, 0.85f, 0.20f),
        PickupKind.AmmoShotgun => new Vector3(1.00f, 0.55f, 0.10f),
        PickupKind.AmmoRocket => new Vector3(1.00f, 0.30f, 0.10f),
        PickupKind.WeaponShotgun => new Vector3(0.20f, 0.55f, 1.00f),
        PickupKind.WeaponRocketLauncher => new Vector3(0.10f, 0.30f, 1.00f),
        _ => Vector3.One,
    };

    private static void ApplyEffect(PickupRuntime p, Player player, WeaponSystem weapons)
    {
        switch (p.Kind)
        {
            case PickupKind.HealthSmall: player.ApplyHealth(25, 100); break;
            case PickupKind.HealthLarge: player.ApplyHealth(50, 200); break;
            case PickupKind.AmmoAk47: weapons.GiveAmmo(WeaponKind.Ak47, 30); break;
            case PickupKind.AmmoShotgun: weapons.GiveAmmo(WeaponKind.Shotgun, 10); break;
            case PickupKind.AmmoRocket: weapons.GiveAmmo(WeaponKind.RocketLauncher, 5); break;
            case PickupKind.WeaponShotgun: weapons.GiveWeapon(WeaponKind.Shotgun, 16); break;
            case PickupKind.WeaponRocketLauncher: weapons.GiveWeapon(WeaponKind.RocketLauncher, 5); break;
        }
    }
}
