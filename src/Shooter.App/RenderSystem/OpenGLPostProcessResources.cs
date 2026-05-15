using Shooter.Render;
using Silk.NET.OpenGL;

namespace Shooter.RenderSystem;

/// <summary>Owns HDR targets and fullscreen post-processing passes.</summary>
internal sealed class OpenGLPostProcessResources : IDisposable
{
    private readonly GL _gl;

    public uint SafeFloatTex { get; }
    public GBufferTarget GBufferTarget { get; }
    public HdrTarget HdrTarget { get; }
    public DeferredLightingPass DeferredLightingPass { get; }
    public ContactShadowPass ContactShadowPass { get; }
    public SsrPass SsrPass { get; }
    public Bloom Bloom { get; }
    public SsaoPass SsaoPass { get; }
    public AutoExposure AutoExposure { get; }
    public PostFx PostFx { get; }

    private OpenGLPostProcessResources(GL gl, uint safeFloatTex, GBufferTarget gBufferTarget, HdrTarget hdrTarget, DeferredLightingPass deferredLightingPass, ContactShadowPass contactShadowPass, SsrPass ssrPass, Bloom bloom, SsaoPass ssaoPass, AutoExposure autoExposure, PostFx postFx)
    {
        _gl = gl;
        SafeFloatTex = safeFloatTex;
        GBufferTarget = gBufferTarget;
        HdrTarget = hdrTarget;
        DeferredLightingPass = deferredLightingPass;
        ContactShadowPass = contactShadowPass;
        SsrPass = ssrPass;
        Bloom = bloom;
        SsaoPass = ssaoPass;
        AutoExposure = autoExposure;
        PostFx = postFx;
    }

    public static OpenGLPostProcessResources Create(GL gl, int width, int height)
    {
        var post = new OpenGLPostProcessResources(
            gl,
            CreateSafeFloatTex(gl),
            new GBufferTarget(gl),
            new HdrTarget(gl),
            new DeferredLightingPass(gl),
            new ContactShadowPass(gl),
            new SsrPass(gl),
            new Bloom(gl),
            new SsaoPass(gl),
            new AutoExposure(gl),
            new PostFx(gl));
        post.Resize(width, height);
        return post;
    }

    public void Resize(int width, int height)
    {
        GBufferTarget.Resize(width, height);
        HdrTarget.Resize(width, height);
        ContactShadowPass.Resize(width, height);
        SsrPass.Resize(width, height);
        Bloom.Resize(width, height);
        SsaoPass.Resize(width, height);
    }

    private static unsafe uint CreateSafeFloatTex(GL gl)
    {
        uint tex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, tex);
        float[] px = [1f, 1f, 1f, 1f];
        fixed (float* p = px)
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, 1, 1, 0, PixelFormat.Rgba, PixelType.Float, p);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        return tex;
    }

    public void Dispose()
    {
        PostFx.Dispose();
        AutoExposure.Dispose();
        SsaoPass.Dispose();
        Bloom.Dispose();
        SsrPass.Dispose();
        ContactShadowPass.Dispose();
        DeferredLightingPass.Dispose();
        HdrTarget.Dispose();
        GBufferTarget.Dispose();
        if (SafeFloatTex != 0)
            _gl.DeleteTexture(SafeFloatTex);
    }
}
