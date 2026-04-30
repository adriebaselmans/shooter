using System.Numerics;
using Shooter.Physics;

namespace Shooter.Game;

/// <summary>A single rocket in flight.</summary>
public sealed class Rocket
{
    public Vector3 Position;
    public Vector3 Velocity;
    /// <summary>Direction the rocket is facing (unit). Used by the renderer to orient the model.</summary>
    public Vector3 Forward;
    public float Age;
    public int Damage;
    public float SplashRadius;
    public bool Dead;
    /// <summary>Detonation: where the rocket impacted (used to spawn decals).</summary>
    public Vector3? ImpactPoint;
    public Vector3? ImpactNormal;
}

/// <summary>Manages rocket projectiles: integration, segment collision against the world,
/// detonation. Cosmetic effects (decals, smoke) are pulled by the caller from <see cref="Detonations"/>.</summary>
public sealed class RocketSystem
{
    /// <summary>Lifetime cap for a rocket in seconds (failsafe).</summary>
    public const float MaxLifetime = 6f;
    public const int Capacity = 32;

    public List<Rocket> Active { get; } = new();
    /// <summary>Detonations that occurred during the last <see cref="Update"/> call. Caller drains.</summary>
    public List<Rocket> Detonations { get; } = new();

    public void Spawn(Vector3 origin, Vector3 direction, float speed, int damage, float splashRadius)
    {
        if (Active.Count >= Capacity) Active.RemoveAt(0); // FIFO drop
        var dir = Vector3.Normalize(direction);
        Active.Add(new Rocket
        {
            Position = origin,
            Velocity = dir * speed,
            Forward = dir,
            Damage = damage,
            SplashRadius = splashRadius,
        });
    }

    public void Update(float dt, CollisionWorld col)
    {
        Detonations.Clear();
        for (int i = 0; i < Active.Count; i++)
        {
            var r = Active[i];
            if (r.Dead) continue;
            r.Age += dt;
            var newPos = r.Position + r.Velocity * dt;

            // Segment vs world: ray from r.Position toward newPos, length = step.
            var step = newPos - r.Position;
            float stepLen = step.Length();
            if (stepLen > 1e-5f)
            {
                var dir = step / stepLen;
                var hit = col.RayCast(r.Position, dir);
                if (hit.Hit && hit.Distance <= stepLen)
                {
                    r.Position = hit.Point;
                    r.ImpactPoint = hit.Point;
                    r.ImpactNormal = hit.Normal;
                    r.Dead = true;
                    Detonations.Add(r);
                    continue;
                }
            }
            r.Position = newPos;
            if (r.Age >= MaxLifetime)
            {
                r.ImpactPoint = r.Position;
                r.ImpactNormal = -r.Forward;
                r.Dead = true;
                Detonations.Add(r);
            }
        }
        Active.RemoveAll(r => r.Dead);
    }
}
