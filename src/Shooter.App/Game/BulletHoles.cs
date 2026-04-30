using System.Numerics;

namespace Shooter.Game;

public readonly record struct BulletHole(Vector3 Position, Vector3 Normal);

public sealed class BulletHoleManager
{
    public const int Cap = 256;
    public const float QuadHalfSize = 0.08f;       // 16 cm wide
    public const float ZOffset = 0.005f;
    private readonly Queue<BulletHole> _holes = new();
    public IEnumerable<BulletHole> Holes => _holes;
    public int Count => _holes.Count;

    public void Add(Vector3 hitPoint, Vector3 normal)
    {
        if (_holes.Count >= Cap) _holes.Dequeue();
        _holes.Enqueue(new BulletHole(hitPoint + normal * ZOffset, normal));
    }
}
