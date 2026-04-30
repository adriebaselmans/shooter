using System.Numerics;

namespace Shooter.Game;

/// <summary>A single soot smudge from a rocket detonation or other explosion.</summary>
public readonly record struct Scorch(Vector3 Position, Vector3 Normal, float HalfSize, float Seed);

/// <summary>Cycling pool of scorch decals. Bigger and softer than bullet holes; rendered with a
/// noisy radial smudge shader so each detonation reads as a grenade-style smoke stain.</summary>
public sealed class ScorchManager
{
    public const int Cap = 64;
    /// <summary>Lift the decal slightly off the surface to avoid z-fighting with the wall it
    /// is attached to.</summary>
    public const float ZOffset = 0.012f;

    private readonly Queue<Scorch> _scorches = new();
    public IEnumerable<Scorch> Scorches => _scorches;
    public int Count => _scorches.Count;

    /// <summary><paramref name="splashRadius"/> is the gameplay splash radius; the visible scorch
    /// is sized to cover roughly half of it so it looks like the centre of the blast rather
    /// than the full damage volume.</summary>
    public void Add(Vector3 hitPoint, Vector3 normal, float splashRadius)
    {
        if (_scorches.Count >= Cap) _scorches.Dequeue();
        float halfSize = MathF.Max(0.6f, splashRadius * 0.55f);
        float seed = (float)Random.Shared.NextDouble();
        _scorches.Enqueue(new Scorch(hitPoint + normal * ZOffset, normal, halfSize, seed));
    }
}
