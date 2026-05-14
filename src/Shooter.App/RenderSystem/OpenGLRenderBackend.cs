using Shooter.Game;
using Silk.NET.Windowing;

namespace Shooter.RenderSystem;

/// <summary>Thin OpenGL backend adapter that delegates to resource/bootstrap and frame-render collaborators.</summary>
public sealed class OpenGLRenderBackend : IRenderBackend
{
    private readonly OpenGLFrameRenderer _frameRenderer = new();
    private OpenGLRenderResources? _resources;

    public string Name => "OpenGL";

    public void Initialize(IWindow window, GameWorld world, LightingEnvironment lighting)
    {
        _resources = OpenGLRenderResources.Create(window, world, lighting);
    }

    public void Resize(int width, int height)
    {
        _resources?.Resize(width, height);
    }

    public void Render(double dt, RenderFrameData frame)
    {
        if (_resources is null) return;
        _frameRenderer.Render(_resources, dt, frame);
    }

    public void Dispose()
    {
        _resources?.Dispose();
        _resources = null;
    }
}
