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
    public uint MsFbo { get; private set; }
    private uint _msColorTex;
    private uint _msNormalTex;
    private uint _msDepthTex;

    public uint ResolveFbo { get; private set; }
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
        if (width == Width && height == Height && MsFbo != 0) return;

        Dispose();
        Width = width;
        Height = height;

        // 1. Create Multisampled FBO (where the actual rendering happens)
        MsFbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, MsFbo);

        _msColorTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2DMultisample, _msColorTex);
        _gl.TexImage2DMultisample(TextureTarget.Texture2DMultisample, 4, InternalFormat.Rgba16f, (uint)width, (uint)height, true);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2DMultisample, _msColorTex, 0);

        _msNormalTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2DMultisample, _msNormalTex);
        _gl.TexImage2DMultisample(TextureTarget.Texture2DMultisample, 4, InternalFormat.Rgba16f, (uint)width, (uint)height, true);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2DMultisample, _msNormalTex, 0);

        _msDepthTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2DMultisample, _msDepthTex);
        _gl.TexImage2DMultisample(TextureTarget.Texture2DMultisample, 4, InternalFormat.DepthComponent24, (uint)width, (uint)height, true);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2DMultisample, _msDepthTex, 0);

        Span<GLEnum> msBufs = [GLEnum.ColorAttachment0, GLEnum.ColorAttachment1];
        fixed (GLEnum* p = msBufs) _gl.DrawBuffers(2, p);

        var msStatus = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (msStatus != GLEnum.FramebufferComplete) throw new InvalidOperationException($"MSAA Framebuffer incomplete: {msStatus}");

        // 2. Create Resolve FBO (standard 2D textures for post-processing)
        ResolveFbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, ResolveFbo);

        ColorTex = AllocColor(width, height, InternalFormat.Rgba16f);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, ColorTex, 0);

        NormalTex = AllocColor(width, height, InternalFormat.Rgba16f);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, NormalTex, 0);

        DepthTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, DepthTex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24, (uint)width, (uint)height, 0, PixelFormat.DepthComponent, PixelType.Float, (void*)0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, DepthTex, 0);

        Span<GLEnum> resBufs = [GLEnum.ColorAttachment0, GLEnum.ColorAttachment1];
        fixed (GLEnum* p = resBufs) _gl.DrawBuffers(2, p);

        var resStatus = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (resStatus != GLEnum.FramebufferComplete) throw new InvalidOperationException($"Resolve Framebuffer incomplete: {resStatus}");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private uint AllocColor(int w, int h, InternalFormat fmt)
    {
        uint tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        unsafe
        {
            var pfmt = (int)fmt == 0x822F ? (PixelFormat)0x8227 /* GL_RG */ : PixelFormat.Rgba;
            _gl.TexImage2D(TextureTarget.Texture2D, 0, fmt,
                (uint)w, (uint)h, 0, pfmt, PixelType.HalfFloat, (void*)0);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        return tex;
    }

    public void Bind()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, MsFbo);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
    }

    /// <summary>Blits the multisampled attachments into the standard 2D texture attachments for post-processing.</summary>
    public void Resolve()
    {
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, MsFbo);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, ResolveFbo);

        // Resolve Color
        _gl.ReadBuffer(ReadBufferMode.ColorAttachment0);
        _gl.DrawBuffer(DrawBufferMode.ColorAttachment0);
        _gl.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        // Resolve Normal
        _gl.ReadBuffer(ReadBufferMode.ColorAttachment1);
        _gl.DrawBuffer(DrawBufferMode.ColorAttachment1);
        _gl.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        // Resolve Depth
        _gl.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
    }

    public static void BindDefault(GL gl, int width, int height)
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.Viewport(0, 0, (uint)width, (uint)height);
    }

    public void Dispose()
    {
        if (MsFbo != 0) { _gl.DeleteFramebuffer(MsFbo); MsFbo = 0; }
        if (_msColorTex != 0) { _gl.DeleteTexture(_msColorTex); _msColorTex = 0; }
        if (_msNormalTex != 0) { _gl.DeleteTexture(_msNormalTex); _msNormalTex = 0; }
        if (_msDepthTex != 0) { _gl.DeleteTexture(_msDepthTex); _msDepthTex = 0; }

        if (ResolveFbo != 0) { _gl.DeleteFramebuffer(ResolveFbo); ResolveFbo = 0; }
        if (ColorTex != 0) { _gl.DeleteTexture(ColorTex); ColorTex = 0; }
        if (NormalTex != 0) { _gl.DeleteTexture(NormalTex); NormalTex = 0; }
        if (DepthTex != 0) { _gl.DeleteTexture(DepthTex); DepthTex = 0; }
        Width = 0; Height = 0;
    }
}
