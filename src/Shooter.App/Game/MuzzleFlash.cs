namespace Shooter.Game;

using System.Numerics;

/// <summary>Short-lived muzzle-flash state. View-space rendered, so it inherits the viewmodel's
/// recoil offset automatically and never desyncs from the rendered barrel tip.</summary>
public sealed class MuzzleFlash
{
    /// <summary>Total time a flash is visible after a single trigger.</summary>
    public const float Duration = 0.055f;

    public float TimeRemaining;
    /// <summary>Random base rotation in radians, picked at trigger time. Renderer adds a fixed
    /// secondary cross at +90° so the flash looks like a sparkly star.</summary>
    public float SeedAngle;
    /// <summary>Random scale multiplier picked at trigger time (~0.8..1.2).</summary>
    public float SeedScale = 1f;
    /// <summary>View-space anchor for the flash, taken from the firing weapon. Lets each
    /// weapon line its flash up with the visible barrel tip.</summary>
    public Vector3 ViewOffset;
    /// <summary>Per-weapon size multiplier (rocket launcher = big, pistols = small).</summary>
    public float WeaponScale = 1f;

    public bool IsActive => TimeRemaining > 0f;
    /// <summary>0..1, peaks at 1 right after Trigger and decays linearly.</summary>
    public float Intensity => IsActive ? TimeRemaining / Duration : 0f;

    public void Trigger(Vector3 viewOffset, float weaponScale)
    {
        TimeRemaining = Duration;
        SeedAngle = (float)(Random.Shared.NextDouble() * MathF.Tau);
        SeedScale = 0.85f + (float)Random.Shared.NextDouble() * 0.35f;
        ViewOffset = viewOffset;
        WeaponScale = weaponScale;
    }

    public void Update(float dt)
    {
        if (TimeRemaining > 0f) TimeRemaining = MathF.Max(0f, TimeRemaining - dt);
    }
}
