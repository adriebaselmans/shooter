using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Offscreen HDR multi-render-target used as the main scene framebuffer.
///
/// Attachments:
///   COLOR0 (RGBA16F) – HDR linear scene color
///   COLOR1 (RGBA16F) – view-space normal (xyz; w unused). Written by lit opaque passes;
///                      non-opaque passes write 0 here so blending preserves the existing
///                      normal under transparent overlays.
///   DEPTH  (DEPTH_COMPONENT24, sampleable) – depth texture so SSAO and auto-exposure
///                      passes can read it as a regular sampler2D.
/// </summary>
public sealed class HdrTarget : IDisposable
{
    private readonly GL _gl;
    public uint Fbo { get; private set; }
    public uint ColorTex { get; private set; }
    public uint NormalTex { get; private set; }
    public uint DepthTex { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public HdrTarget(GL gl)
    {
        _gl = gl;
    }

    /// <summary>Allocate or re-allocate the target. Safe to call every frame.</summary>
    public unsafe void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (width == Width && height == Height && Fbo != 0) return;

        Dispose();

        Fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);

        // COLOR0: HDR scene
        ColorTex = AllocColor(width, height, InternalFormat.Rgba16f);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, ColorTex, 0);

        // COLOR1: view-space normal
        NormalTex = AllocColor(width, height, InternalFormat.Rgba16f);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1,
            TextureTarget.Texture2D, NormalTex, 0);

        // DEPTH: sampleable depth texture (GL_NEAREST – we don't want filtered depth in SSAO)
        DepthTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, DepthTex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24,
            (uint)width, (uint)height, 0, PixelFormat.DepthComponent, PixelType.Float, (void*)0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
            TextureTarget.Texture2D, DepthTex, 0);

        Span<GLEnum> bufs = [GLEnum.ColorAttachment0, GLEnum.ColorAttachment1];
        fixed (GLEnum* p = bufs) _gl.DrawBuffers(2, p);

        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"HdrTarget framebuffer incomplete: {status}");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        Width = width;
        Height = height;
    }

    private uint AllocColor(int w, int h, InternalFormat fmt)
    {
        uint tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        unsafe
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, fmt,
                (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.HalfFloat, (void*)0);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        return tex;
    }

    public void Bind()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
    }

    public static void BindDefault(GL gl, int width, int height)
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.Viewport(0, 0, (uint)width, (uint)height);
    }

    public void Dispose()
    {
        if (Fbo != 0) { _gl.DeleteFramebuffer(Fbo); Fbo = 0; }
        if (ColorTex != 0) { _gl.DeleteTexture(ColorTex); ColorTex = 0; }
        if (NormalTex != 0) { _gl.DeleteTexture(NormalTex); NormalTex = 0; }
        if (DepthTex != 0) { _gl.DeleteTexture(DepthTex); DepthTex = 0; }
        Width = 0; Height = 0;
    }
}
