using System.Numerics;

namespace Shooter.Game;

/// <summary>Short-lived bullet tracer drawn from muzzle to impact (or far point if no hit).</summary>
public sealed class Tracer
{
    public Vector3 Start;
    public Vector3 End;
    public float Ttl;       // seconds remaining
    public float Lifetime;  // total at spawn
}

public sealed class TracerSystem
{
    public const float DefaultLifetime = 0.06f;
    public const float MaxRange = 200f;
    public List<Tracer> Active { get; } = new();

    public void Add(Vector3 start, Vector3 end, float lifetime = DefaultLifetime)
    {
        Active.Add(new Tracer { Start = start, End = end, Ttl = lifetime, Lifetime = lifetime });
    }

    public void Update(float dt)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            Active[i].Ttl -= dt;
            if (Active[i].Ttl <= 0) Active.RemoveAt(i);
        }
    }
}
