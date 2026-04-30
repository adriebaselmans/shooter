using System.Numerics;
using Shooter.Input;
using Shooter.Physics;

namespace Shooter.Game;

/// <summary>First-person player state and movement.</summary>
public sealed class Player
{
    public const float Radius = 0.4f;
    public const float HalfHeight = 0.9f;
    public const float EyeOffset = 0.7f;
    public const float WalkSpeed = 6f;
    public const float JumpSpeed = 7f;
    public const float Gravity = -20f;

    public Vector3 Position;            // capsule center
    public Vector3 Velocity;
    public float Yaw;                   // radians
    public float Pitch;                 // radians, clamped
    public bool Grounded;

    public int Health = 100;
    public int MaxHealth = 100;

    public Vector3 EyePosition => Position + new Vector3(0, EyeOffset, 0);

    public Vector3 Forward()
    {
        float cy = MathF.Cos(Yaw), sy = MathF.Sin(Yaw);
        float cp = MathF.Cos(Pitch), sp = MathF.Sin(Pitch);
        return new Vector3(sy * cp, sp, -cy * cp);
    }

    public Vector3 ForwardFlat()
    {
        float cy = MathF.Cos(Yaw), sy = MathF.Sin(Yaw);
        return new Vector3(sy, 0, -cy);
    }

    public Vector3 Right()
    {
        float cy = MathF.Cos(Yaw), sy = MathF.Sin(Yaw);
        return new Vector3(cy, 0, sy);
    }

    public void Update(float dt, InputState input, CollisionWorld col)
    {
        // Mouse look
        const float mouseSens = 0.0025f;
        Yaw += input.MouseDelta.X * mouseSens;
        Pitch -= input.MouseDelta.Y * mouseSens;
        const float pitchLimit = MathF.PI / 2f - 0.02f;
        if (Pitch > pitchLimit) Pitch = pitchLimit;
        if (Pitch < -pitchLimit) Pitch = -pitchLimit;

        // Horizontal movement (camera-relative)
        Vector3 wishDir = Vector3.Zero;
        if (input.IsDown(InputKey.W)) wishDir += ForwardFlat();
        if (input.IsDown(InputKey.S)) wishDir -= ForwardFlat();
        if (input.IsDown(InputKey.D)) wishDir += Right();
        if (input.IsDown(InputKey.A)) wishDir -= Right();
        if (wishDir.LengthSquared() > 1e-4f) wishDir = Vector3.Normalize(wishDir);

        Velocity.X = wishDir.X * WalkSpeed;
        Velocity.Z = wishDir.Z * WalkSpeed;

        // Jump
        if (Grounded && input.IsDown(InputKey.Space))
        {
            Velocity.Y = JumpSpeed;
            Grounded = false;
        }

        // Gravity
        Velocity.Y += Gravity * dt;
        // Terminal velocity: also a tunneling safeguard. The collider is a 0.4 m sphere and
        // the thinnest floor brush we expect is ~0.5 m, so cap fall speed so a single step
        // can never exceed ~half the collider's diameter at 60 fps.
        const float terminalVelocity = -24f;
        if (Velocity.Y < terminalVelocity) Velocity.Y = terminalVelocity;

        // Move horizontally then vertically (axis-separated to feel snappy)
        var moveH = new Vector3(Velocity.X, 0, Velocity.Z) * dt;
        var (newPosH, _) = col.MoveSphere(Position, Radius, moveH);
        Position = newPosH;

        var moveV = new Vector3(0, Velocity.Y * dt, 0);
        var (newPosV, normalV) = col.MoveSphere(Position, Radius, moveV);
        Position = newPosV;

        // Ground test
        if (normalV.Y > 0.6f)
        {
            Grounded = true;
            if (Velocity.Y < 0) Velocity.Y = 0;
        }
        else if (normalV.Y < -0.6f && Velocity.Y > 0)
        {
            Velocity.Y = 0;     // bumped head
        }
        else
        {
            Grounded = false;
        }
    }

    public void ApplyHealth(int amount, int newCap)
    {
        if (newCap > MaxHealth) MaxHealth = newCap;
        Health = Math.Min(MaxHealth, Health + amount);
    }
}
