using System.Numerics;

namespace Shooter.Game;

/// <summary>Single source of truth for sun direction, sky parameters, and exposure used by
/// the sky shader, the IBL probe, the shadow camera, the lit shaders, and the post pass.
/// Plain data; no behavior beyond defaults that produce a believable late-afternoon look.</summary>
public sealed class LightingEnvironment
{
    /// <summary>Direction the sun's light *travels* (i.e. -toSun). Must be normalized.</summary>
    public Vector3 SunDirection { get; set; } = Vector3.Normalize(new Vector3(-0.5f, -0.65f, -0.4f));

    /// <summary>Sun color in linear HDR. Values can sit around 1.0; intensity scales them up.</summary>
    public Vector3 SunColor { get; set; } = new(1.00f, 0.92f, 0.78f);

    /// <summary>HDR multiplier applied to <see cref="SunColor"/> in the direct-light term.
    /// Values in the 3..6 range work well with the default ACES tone curve and Exposure=1.0.</summary>
    public float SunIntensity { get; set; } = 4.0f;

    /// <summary>Atmosphere thickness for the analytic sky (2..10). Larger is hazier.</summary>
    public float Turbidity { get; set; } = 3.0f;

    /// <summary>Below-horizon ground tint visible in the sky cube and in IBL convolution.</summary>
    public Vector3 GroundAlbedo { get; set; } = new(0.18f, 0.16f, 0.14f);

    /// <summary>Post-pass exposure scalar applied before the ACES tone curve.</summary>
    public float Exposure { get; set; } = 1.0f;

    /// <summary>Multiplier on the irradiance ambient term in lit shaders. ~1.0 by default.</summary>
    public float IrradianceIntensity { get; set; } = 1.0f;

    /// <summary>Direction *toward* the sun. Convenience for shaders that want `dot(N, toSun)`.</summary>
    public Vector3 ToSun => -SunDirection;

    // -----------------------------------------------------------------
    // SSAO
    // -----------------------------------------------------------------

    /// <summary>How strongly SSAO darkens the final image. 0 disables, 1 multiplies the AO
    /// factor straight in. ~0.7 is a comfortable default for a forward renderer.</summary>
    public float SsaoStrength { get; set; } = 0.7f;

    /// <summary>Hemisphere sample radius in view-space metres. Larger reaches further but
    /// can introduce halos around silhouettes.</summary>
    public float SsaoRadius { get; set; } = 0.5f;

    /// <summary>View-space depth bias added before the occlusion comparison; combats
    /// self-occlusion on flat surfaces.</summary>
    public float SsaoBias { get; set; } = 0.025f;

    // -----------------------------------------------------------------
    // Auto-exposure
    // -----------------------------------------------------------------

    /// <summary>When true, <see cref="Exposure"/> is replaced each frame by an automatically
    /// computed value derived from the average log-luminance of the rendered HDR frame.</summary>
    public bool AutoExposureEnabled { get; set; } = true;

    /// <summary>Target middle-gray value the camera tries to expose toward.
    /// 0.18 is the photographic standard.</summary>
    public float ExposureKey { get; set; } = 0.18f;

    /// <summary>How fast (1/sec) the exposure adapts toward the new target. Real eyes
    /// adapt to bright much faster than to dark.</summary>
    public float ExposureAdaptRateBrighten { get; set; } = 4.0f;

    public float ExposureAdaptRateDarken { get; set; } = 1.5f;

    /// <summary>Hard clamps on auto-exposure to prevent runaway in pure-black or pure-white
    /// frames (e.g. while looking straight at the sun).</summary>
    public float MinExposure { get; set; } = 0.3f;

    public float MaxExposure { get; set; } = 3.0f;
}
