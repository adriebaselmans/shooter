using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Deferred opaque GBuffer with MSAA render targets and resolved 2D textures.
/// Layout:
///   G0 = RGBA8   (albedo.rgb, ao)
///   G1 = RG16F   (octa normal.xy)
///   G2 = RGBA8   (roughness, metallic, emissive, wetness)
///   D  = DEPTH24
/// </summary>
public sealed class GBufferTarget : IDisposable
{
    private readonly GL _gl;

    public uint MsFbo { get; private set; }
    private uint _msG0Tex;
    private uint _msG1Tex;
    private uint _msG2Tex;
    private uint _msDepthTex;

    public uint ResolveFbo { get; private set; }
    public uint G0Tex { get; private set; }
    public uint G1Tex { get; private set; }
    public uint G2Tex { get; private set; }
    public uint DepthTex { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public GBufferTarget(GL gl) => _gl = gl;

    public unsafe void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (width == Width && height == Height && MsFbo != 0) return;

        Dispose();
        Width = width;
        Height = height;

        MsFbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, MsFbo);

        _msG0Tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2DMultisample, _msG0Tex);
        _gl.TexImage2DMultisample(TextureTarget.Texture2DMultisample, 4, InternalFormat.Rgba8, (uint)width, (uint)height, true);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2DMultisample, _msG0Tex, 0);

        _msG1Tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2DMultisample, _msG1Tex);
        _gl.TexImage2DMultisample(TextureTarget.Texture2DMultisample, 4, InternalFormat.Rgba16f, (uint)width, (uint)height, true);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2DMultisample, _msG1Tex, 0);

        _msG2Tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2DMultisample, _msG2Tex);
        _gl.TexImage2DMultisample(TextureTarget.Texture2DMultisample, 4, InternalFormat.Rgba8, (uint)width, (uint)height, true);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2DMultisample, _msG2Tex, 0);

        _msDepthTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2DMultisample, _msDepthTex);
        _gl.TexImage2DMultisample(TextureTarget.Texture2DMultisample, 4, InternalFormat.DepthComponent24, (uint)width, (uint)height, true);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2DMultisample, _msDepthTex, 0);

        Span<GLEnum> msBufs = [GLEnum.ColorAttachment0, GLEnum.ColorAttachment1, GLEnum.ColorAttachment2];
        fixed (GLEnum* p = msBufs) _gl.DrawBuffers(3, p);

        var msStatus = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (msStatus != GLEnum.FramebufferComplete) throw new InvalidOperationException($"GBuffer MSAA framebuffer incomplete: {msStatus}");

        ResolveFbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, ResolveFbo);

        G0Tex = AllocColor(width, height, InternalFormat.Rgba8);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, G0Tex, 0);

        G1Tex = AllocColor(width, height, InternalFormat.Rgba16f);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, G1Tex, 0);

        G2Tex = AllocColor(width, height, InternalFormat.Rgba8);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, G2Tex, 0);

        DepthTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, DepthTex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24, (uint)width, (uint)height, 0, PixelFormat.DepthComponent, PixelType.Float, (void*)0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, DepthTex, 0);

        Span<GLEnum> resBufs = [GLEnum.ColorAttachment0, GLEnum.ColorAttachment1, GLEnum.ColorAttachment2];
        fixed (GLEnum* p = resBufs) _gl.DrawBuffers(3, p);

        var resStatus = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (resStatus != GLEnum.FramebufferComplete) throw new InvalidOperationException($"GBuffer resolve framebuffer incomplete: {resStatus}");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private uint AllocColor(int w, int h, InternalFormat fmt)
    {
        uint tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        unsafe
        {
            var pfmt = PixelFormat.Rgba;
            var ptype = fmt == InternalFormat.Rgba8 ? PixelType.UnsignedByte : PixelType.HalfFloat;
            _gl.TexImage2D(TextureTarget.Texture2D, 0, fmt, (uint)w, (uint)h, 0, pfmt, ptype, (void*)0);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        return tex;
    }

    public void Bind()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, MsFbo);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
    }

    public void Resolve()
    {
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, MsFbo);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, ResolveFbo);

        _gl.ReadBuffer(ReadBufferMode.ColorAttachment0);
        _gl.DrawBuffer(DrawBufferMode.ColorAttachment0);
        _gl.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        _gl.ReadBuffer(ReadBufferMode.ColorAttachment1);
        _gl.DrawBuffer(DrawBufferMode.ColorAttachment1);
        _gl.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        _gl.ReadBuffer(ReadBufferMode.ColorAttachment2);
        _gl.DrawBuffer(DrawBufferMode.ColorAttachment2);
        _gl.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        _gl.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
    }

    public void Dispose()
    {
        if (MsFbo != 0) { _gl.DeleteFramebuffer(MsFbo); MsFbo = 0; }
        if (_msG0Tex != 0) { _gl.DeleteTexture(_msG0Tex); _msG0Tex = 0; }
        if (_msG1Tex != 0) { _gl.DeleteTexture(_msG1Tex); _msG1Tex = 0; }
        if (_msG2Tex != 0) { _gl.DeleteTexture(_msG2Tex); _msG2Tex = 0; }
        if (_msDepthTex != 0) { _gl.DeleteTexture(_msDepthTex); _msDepthTex = 0; }

        if (ResolveFbo != 0) { _gl.DeleteFramebuffer(ResolveFbo); ResolveFbo = 0; }
        if (G0Tex != 0) { _gl.DeleteTexture(G0Tex); G0Tex = 0; }
        if (G1Tex != 0) { _gl.DeleteTexture(G1Tex); G1Tex = 0; }
        if (G2Tex != 0) { _gl.DeleteTexture(G2Tex); G2Tex = 0; }
        if (DepthTex != 0) { _gl.DeleteTexture(DepthTex); DepthTex = 0; }
        Width = 0;
        Height = 0;
    }
}
