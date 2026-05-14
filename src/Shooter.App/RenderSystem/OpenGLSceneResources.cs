using Shooter.Game;
using Shooter.Render;
using Silk.NET.OpenGL;

namespace Shooter.RenderSystem;

/// <summary>Owns scene-facing OpenGL renderers used during shadow, scene, overlay, and HUD passes.</summary>
internal sealed class OpenGLSceneResources : IDisposable
{
    public WorldRenderer WorldRenderer { get; }
    public DecalRenderer DecalRenderer { get; }
    public TracerRenderer TracerRenderer { get; }
    public HudRenderer HudRenderer { get; }
    public WeaponViewmodelRenderer WeaponViewmodelRenderer { get; }
    public RocketRenderer RocketRenderer { get; }
    public MuzzleFlashRenderer MuzzleFlashRenderer { get; }
    public ScorchRenderer ScorchRenderer { get; }
    public ParticleRenderer ParticleRenderer { get; }
    public SkyRenderer SkyRenderer { get; }

    private OpenGLSceneResources(
        WorldRenderer worldRenderer,
        DecalRenderer decalRenderer,
        TracerRenderer tracerRenderer,
        HudRenderer hudRenderer,
        WeaponViewmodelRenderer weaponViewmodelRenderer,
        RocketRenderer rocketRenderer,
        MuzzleFlashRenderer muzzleFlashRenderer,
        ScorchRenderer scorchRenderer,
        ParticleRenderer particleRenderer,
        SkyRenderer skyRenderer)
    {
        WorldRenderer = worldRenderer;
        DecalRenderer = decalRenderer;
        TracerRenderer = tracerRenderer;
        HudRenderer = hudRenderer;
        WeaponViewmodelRenderer = weaponViewmodelRenderer;
        RocketRenderer = rocketRenderer;
        MuzzleFlashRenderer = muzzleFlashRenderer;
        ScorchRenderer = scorchRenderer;
        ParticleRenderer = particleRenderer;
        SkyRenderer = skyRenderer;
    }

    public static OpenGLSceneResources Create(GL gl, GameWorld world) => new(
        new WorldRenderer(gl, world),
        new DecalRenderer(gl),
        new TracerRenderer(gl),
        new HudRenderer(gl),
        new WeaponViewmodelRenderer(gl),
        new RocketRenderer(gl),
        new MuzzleFlashRenderer(gl),
        new ScorchRenderer(gl),
        new ParticleRenderer(gl),
        new SkyRenderer(gl));

    public void Dispose()
    {
        ParticleRenderer.Dispose();
        ScorchRenderer.Dispose();
        MuzzleFlashRenderer.Dispose();
        RocketRenderer.Dispose();
        WeaponViewmodelRenderer.Dispose();
        HudRenderer.Dispose();
        TracerRenderer.Dispose();
        DecalRenderer.Dispose();
        WorldRenderer.Dispose();
        SkyRenderer.Dispose();
    }
}
