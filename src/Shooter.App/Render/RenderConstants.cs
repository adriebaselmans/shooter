using System.Numerics;

namespace Shooter.Render;

public static class RenderConstants
{
    /// <summary>View-space position of the held weapon barrel tip anchor.</summary>
    public static readonly Vector3 ViewmodelMuzzleOffset = new(0.20f, -0.16f, -0.55f);

    /// <summary>Vertical FOV used by world and viewmodel projections.</summary>
    public const float ViewmodelFovYRadians = 75f * MathF.PI / 180f;

    /// <summary>World-space length the rocket model is aligned/scaled to.</summary>
    public const float RocketLength = 0.7f;
}
