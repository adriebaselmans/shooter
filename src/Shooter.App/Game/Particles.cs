using System.Numerics;

namespace Shooter.Game;

public enum ParticleKind
{
    Smoke,
    Dust,
    Ember,
}

public sealed class Particle
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector4 Color;
    public float Size;
    public float Age;
    public float Lifetime;
    public ParticleKind Kind;
    public float Drag;
    public float UpwardAccel;
}

public sealed class ParticleSystem
{
    public const int Capacity = 1024;
    public List<Particle> Active { get; } = [];

    public void Update(float dt)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            var p = Active[i];
            p.Age += dt;
            if (p.Age >= p.Lifetime)
            {
                Active.RemoveAt(i);
                continue;
            }

            float drag = MathF.Exp(-p.Drag * dt);
            p.Velocity *= drag;
            p.Velocity += Vector3.UnitY * (p.UpwardAccel * dt);
            p.Position += p.Velocity * dt;
        }
    }

    public void EmitRocketTrail(Rocket rocket, float dt)
    {
        float rate = 42f * dt;
        int count = Math.Max(1, (int)MathF.Ceiling(rate));
        Vector3 basePos = rocket.Position - rocket.Forward * 0.22f;
        for (int i = 0; i < count; i++)
        {
            Vector3 jitter = new(
                Range(-0.05f, 0.05f),
                Range(-0.03f, 0.03f),
                Range(-0.05f, 0.05f));
            Spawn(new Particle
            {
                Position = basePos + jitter,
                Velocity = -rocket.Forward * Range(0.4f, 1.1f) + new Vector3(Range(-0.25f, 0.25f), Range(0.08f, 0.22f), Range(-0.25f, 0.25f)),
                Color = new Vector4(0.34f, 0.34f, 0.36f, 0.42f),
                Size = Range(0.12f, 0.22f),
                Lifetime = Range(0.35f, 0.70f),
                Kind = ParticleKind.Smoke,
                Drag = 2.6f,
                UpwardAccel = 0.26f,
            });
        }
    }

    public void EmitExplosion(Vector3 point, Vector3 normal, float radius)
    {
        int smokeCount = Math.Clamp((int)(radius * 10f), 8, 20);
        for (int i = 0; i < smokeCount; i++)
        {
            Vector3 dir = RandomDirHemisphere(normal);
            Spawn(new Particle
            {
                Position = point + normal * 0.03f + dir * Range(0.05f, 0.18f),
                Velocity = dir * Range(0.8f, 2.0f) + Vector3.UnitY * Range(0.1f, 0.7f),
                Color = new Vector4(0.38f, 0.34f, 0.30f, 0.48f),
                Size = Range(0.28f, 0.55f),
                Lifetime = Range(0.55f, 1.10f),
                Kind = ParticleKind.Dust,
                Drag = 2.0f,
                UpwardAccel = 0.18f,
            });
        }

        int emberCount = Math.Clamp((int)(radius * 6f), 6, 16);
        for (int i = 0; i < emberCount; i++)
        {
            Vector3 dir = RandomDirHemisphere(normal);
            Spawn(new Particle
            {
                Position = point + normal * 0.04f,
                Velocity = dir * Range(1.4f, 4.0f),
                Color = new Vector4(1.0f, 0.72f, 0.24f, 0.58f),
                Size = Range(0.05f, 0.11f),
                Lifetime = Range(0.20f, 0.45f),
                Kind = ParticleKind.Ember,
                Drag = 4.5f,
                UpwardAccel = -1.0f,
            });
        }
    }

    public void EmitMuzzleSmoke(Vector3 origin, Vector3 forward, WeaponKind weapon)
    {
        int count = weapon switch
        {
            WeaponKind.Ak47 => 3,
            WeaponKind.Shotgun => 6,
            WeaponKind.RocketLauncher => 8,
            _ => 4,
        };
        float speedMin = weapon == WeaponKind.RocketLauncher ? 0.45f : 0.25f;
        float speedMax = weapon == WeaponKind.RocketLauncher ? 1.10f : weapon == WeaponKind.Shotgun ? 0.90f : 0.75f;
        float baseSize = weapon == WeaponKind.RocketLauncher ? 0.14f : weapon == WeaponKind.Shotgun ? 0.11f : 0.09f;
        for (int i = 0; i < count; i++)
        {
            Vector3 jitter = new(Range(-0.02f, 0.02f), Range(-0.02f, 0.02f), Range(-0.02f, 0.02f));
            Spawn(new Particle
            {
                Position = origin + forward * 0.08f + jitter,
                Velocity = forward * Range(speedMin, speedMax) + new Vector3(Range(-0.12f, 0.12f), Range(0.04f, 0.18f), Range(-0.12f, 0.12f)),
                Color = new Vector4(0.48f, 0.48f, 0.50f, weapon == WeaponKind.RocketLauncher ? 0.32f : 0.26f),
                Size = Range(baseSize, baseSize + 0.08f),
                Lifetime = Range(0.18f, weapon == WeaponKind.RocketLauncher ? 0.55f : 0.36f),
                Kind = ParticleKind.Smoke,
                Drag = weapon == WeaponKind.RocketLauncher ? 2.8f : 3.6f,
                UpwardAccel = 0.14f,
            });
        }
    }

    public void EmitImpactDust(Vector3 point, Vector3 normal, WeaponKind weapon)
    {
        int count = weapon == WeaponKind.Shotgun ? 6 : 3;
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = RandomDirHemisphere(normal);
            Spawn(new Particle
            {
                Position = point + normal * 0.03f + dir * Range(0.01f, 0.05f),
                Velocity = normal * Range(0.12f, 0.45f) + dir * Range(0.10f, weapon == WeaponKind.Shotgun ? 0.85f : 0.45f),
                Color = new Vector4(0.58f, 0.52f, 0.44f, weapon == WeaponKind.Shotgun ? 0.34f : 0.24f),
                Size = Range(0.04f, weapon == WeaponKind.Shotgun ? 0.11f : 0.08f),
                Lifetime = Range(0.14f, weapon == WeaponKind.Shotgun ? 0.34f : 0.24f),
                Kind = ParticleKind.Dust,
                Drag = 4.5f,
                UpwardAccel = 0.08f,
            });
        }
    }

    private void Spawn(Particle particle)
    {
        if (Active.Count >= Capacity)
            Active.RemoveAt(0);
        Active.Add(particle);
    }

    private static float Range(float min, float max) => min + (float)Random.Shared.NextDouble() * (max - min);

    private static Vector3 RandomDirHemisphere(Vector3 normal)
    {
        Vector3 dir;
        do
        {
            dir = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
        } while (dir.LengthSquared() < 1e-4f);
        dir = Vector3.Normalize(dir);
        if (Vector3.Dot(dir, normal) < 0f) dir = -dir;
        return dir;
    }
}
