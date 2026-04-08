using System.Numerics;

namespace MapEditor.Rendering.Cameras;

/// <summary>Abstract camera providing view and projection matrices.</summary>
public abstract class Camera
{
    public abstract Matrix4x4 GetViewMatrix();
    public abstract Matrix4x4 GetProjectionMatrix(float aspectRatio);
}

/// <summary>Perspective camera with orbit/pan/zoom controls.</summary>
public sealed class PerspectiveCamera : Camera
{
    public const float DefaultDistance = 800f;
    public const float MinDistance = 32f;
    public const float MaxDistance = 50000f;

    public float Yaw        { get; set; } = -90f;
    public float Pitch      { get; set; } = -25f;
    public float Distance   { get; set; } = DefaultDistance;
    public Vector3 Target   { get; set; } = Vector3.Zero;
    public float FieldOfView { get; set; } = 60f;
    public float NearPlane  { get; set; } = 1f;
    public float FarPlane   { get; set; } = MaxDistance;

    public void ResetToDefault()
    {
        Yaw      = -90f;
        Pitch    = -25f;
        Distance = DefaultDistance;
        Target   = Vector3.Zero;
    }

    public void Orbit(float yawDelta, float pitchDelta)
    {
        if (!float.IsFinite(yawDelta) || !float.IsFinite(pitchDelta))
        {
            return;
        }

        Yaw += yawDelta;
        Pitch = Math.Clamp(Pitch + pitchDelta, -89f, 89f);
    }

    public void ZoomBy(float factor)
    {
        if (!float.IsFinite(factor) || factor <= 0f)
        {
            return;
        }

        Distance = ClampFinite(Distance * factor, MinDistance, MaxDistance, DefaultDistance);
    }

    public void Pan(Vector3 delta)
    {
        if (!IsFinite(delta))
        {
            return;
        }

        Target += delta;
    }

    public Vector3 Position
    {
        get
        {
            float distance = ClampFinite(Distance, MinDistance, MaxDistance, DefaultDistance);
            float yawRad   = float.DegreesToRadians(float.IsFinite(Yaw) ? Yaw : -90f);
            float pitchRad = float.DegreesToRadians(Math.Clamp(float.IsFinite(Pitch) ? Pitch : -25f, -89f, 89f));
            var target = IsFinite(Target) ? Target : Vector3.Zero;
            var offset = new Vector3(
                MathF.Cos(pitchRad) * MathF.Cos(yawRad),
                MathF.Sin(pitchRad),
                MathF.Cos(pitchRad) * MathF.Sin(yawRad)) * distance;
            return IsFinite(offset) ? target + offset : target + new Vector3(0f, 0f, -distance);
        }
    }

    public override Matrix4x4 GetViewMatrix() =>
        Matrix4x4.CreateLookAt(Position, IsFinite(Target) ? Target : Vector3.Zero, Vector3.UnitY);

    public override Matrix4x4 GetProjectionMatrix(float aspectRatio)
    {
        float nearPlane = ClampFinite(NearPlane, 0.01f, 1000f, 1f);
        float farPlane = MathF.Max(nearPlane + 1f, ClampFinite(FarPlane, 10f, MaxDistance * 4f, MaxDistance));
        return Matrix4x4.CreatePerspectiveFieldOfView(
            float.DegreesToRadians(Math.Clamp(float.IsFinite(FieldOfView) ? FieldOfView : 60f, 20f, 120f)),
            float.IsFinite(aspectRatio) && aspectRatio > 0.01f ? aspectRatio : 1f,
            nearPlane,
            farPlane);
    }

    private static float ClampFinite(float value, float min, float max, float fallback) =>
        !float.IsFinite(value) ? fallback : Math.Clamp(value, min, max);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);
}

/// <summary>Axis-aligned orthographic camera for 2D viewport projections.</summary>
public sealed class OrthographicCamera : Camera
{
    public float Pan    { get; set; } = 0f;
    public float PanY   { get; set; } = 0f;
    public float Zoom   { get; set; } = 500f; // half-height in world units
    public ViewAxis Axis { get; set; } = ViewAxis.Top;

    public override Matrix4x4 GetViewMatrix()
    {
        Vector3 eye, target, up;
        switch (Axis)
        {
            case ViewAxis.Top:
                eye    = new Vector3(Pan, -Zoom * 2, PanY);
                target = new Vector3(Pan, 0, PanY);
                up     = Vector3.UnitZ;
                break;
            case ViewAxis.Front:
                eye    = new Vector3(Pan, PanY, Zoom * 2);
                target = new Vector3(Pan, PanY, 0);
                up     = Vector3.UnitY;
                break;
            default: // Side
                eye    = new Vector3(-Zoom * 2, PanY, Pan);
                target = new Vector3(0, PanY, Pan);
                up     = Vector3.UnitY;
                break;
        }
        return Matrix4x4.CreateLookAt(eye, target, up);
    }

    public override Matrix4x4 GetProjectionMatrix(float aspectRatio)
    {
        float halfH = Zoom;
        float halfW = halfH * aspectRatio;
        return Matrix4x4.CreateOrthographicOffCenter(-halfW, halfW, -halfH, halfH, -100000f, 100000f);
    }
}

/// <summary>Orthographic projection axis.</summary>
public enum ViewAxis { Top, Front, Side }
