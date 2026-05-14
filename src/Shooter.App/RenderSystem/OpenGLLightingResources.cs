using Shooter.Game;
using Shooter.Render;
using Silk.NET.OpenGL;

namespace Shooter.RenderSystem;

/// <summary>Owns lighting-related OpenGL resources shared across frame passes.</summary>
internal sealed class OpenGLLightingResources : IDisposable
{
    public IblProbe IblProbe { get; }
    public ShadowMap ShadowMap { get; }

    private OpenGLLightingResources(IblProbe iblProbe, ShadowMap shadowMap)
    {
        IblProbe = iblProbe;
        ShadowMap = shadowMap;
    }

    public static OpenGLLightingResources Create(GL gl, LightingEnvironment lighting)
    {
        var iblProbe = new IblProbe(gl);
        iblProbe.Build(lighting);
        return new OpenGLLightingResources(iblProbe, new ShadowMap(gl));
    }

    public void Dispose()
    {
        ShadowMap.Dispose();
        IblProbe.Dispose();
    }
}
