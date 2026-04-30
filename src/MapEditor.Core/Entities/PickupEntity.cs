namespace MapEditor.Core.Entities;

/// <summary>Kinds of upgrade pickups consumed by the game runtime.</summary>
public enum PickupKind
{
    HealthSmall,
    HealthLarge,
    AmmoAk47,
    AmmoShotgun,
    AmmoRocket,
    WeaponShotgun,
    WeaponRocketLauncher,
}

/// <summary>An upgrade pickup placed in the scene (health, ammo, or weapon).</summary>
public sealed class PickupEntity : IEntity
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Pickup";
    public Transform Transform { get; set; } = Transform.Identity;

    /// <summary>Functional category of this pickup.</summary>
    public PickupKind Kind { get; set; } = PickupKind.HealthSmall;

    /// <summary>Effect magnitude (e.g. health amount or ammo count). 0 ⇒ use kind default at runtime.</summary>
    public int Amount { get; set; }

    public PickupEntity() { }
    public PickupEntity(Guid id, string name) { Id = id; Name = name; }
}
