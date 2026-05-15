using Shooter.Render;
using Silk.NET.OpenGL;

namespace Shooter.RenderSystem;

/// <summary>Owns HDR targets and fullscreen post-processing passes.</summary>
internal sealed class OpenGLPostProcessResources : IDisposable
{
    public HdrTarget HdrTarget { get; }
    public Bloom Bloom { get; }
    public SsaoPass SsaoPass { get; }
    public AutoExposure AutoExposure { get; }
    public PostFx PostFx { get; }

    private OpenGLPostProcessResources(HdrTarget hdrTarget, Bloom bloom, SsaoPass ssaoPass, AutoExposure autoExposure, PostFx postFx)
    {
        HdrTarget = hdrTarget;
        Bloom = bloom;
        SsaoPass = ssaoPass;
        AutoExposure = autoExposure;
        PostFx = postFx;
    }

    public static OpenGLPostProcessResources Create(GL gl, int width, int height)
    {
        var post = new OpenGLPostProcessResources(
            new HdrTarget(gl),
            new Bloom(gl),
            new SsaoPass(gl),
            new AutoExposure(gl),
            new PostFx(gl));
        post.Resize(width, height);
        return post;
    }

    public void Resize(int width, int height)
    {
        HdrTarget.Resize(width, height);
        Bloom.Resize(width, height);
        SsaoPass.Resize(width, height);
    }

    public void Dispose()
    {
        PostFx.Dispose();
        AutoExposure.Dispose();
        SsaoPass.Dispose();
        Bloom.Dispose();
        HdrTarget.Dispose();
    }
}
