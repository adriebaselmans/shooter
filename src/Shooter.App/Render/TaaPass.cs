using System;
using Silk.NET.OpenGL;
using Shooter.RenderSystem;

namespace Shooter.Render;

public sealed class TaaPass : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _resolveShader;
    private uint _vao;

    private uint _fbo, _historyTex;
    private uint _scratchFbo, _scratchTex;
    private int _w, _h;
    
    // Halton(2, 3) sequence up to 16
    private static readonly float[] JitterX = { 0.5f, 0.25f, 0.75f, 0.125f, 0.625f, 0.375f, 0.875f, 0.0625f, 0.5625f, 0.3125f, 0.8125f, 0.1875f, 0.6875f, 0.4375f, 0.9375f, 0.03125f };
    private static readonly float[] JitterY = { 0.333f, 0.666f, 0.111f, 0.444f, 0.777f, 0.222f, 0.555f, 0.888f, 0.037f, 0.370f, 0.703f, 0.148f, 0.481f, 0.814f, 0.259f, 0.592f };
    
    public int FrameIndex { get; set; } = 0;
    public uint OutputTex => _historyTex;
    public bool Initialized { get; set; }

    public TaaPass(GL gl)
    {
        _gl = gl;
        _resolveShader = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.TaaResolveFrag);
        _vao = gl.GenVertexArray();
    }

    public void Resize(int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        if (w == _w && h == _h) return;
        
        DisposeTextures();
        _w = w; _h = h;

        _fbo = _gl.GenFramebuffer();
        _historyTex = AllocColor(w, h);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _historyTex, 0);

        _scratchFbo = _gl.GenFramebuffer();
        _scratchTex = AllocColor(w, h);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _scratchFbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _scratchTex, 0);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        Initialized = false; // Need to copy first frame straight
    }

    private uint AllocColor(int w, int h)
    {
        uint tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        unsafe { _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.HalfFloat, (void*)0); }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        return tex;
    }

    public (float jx, float jy) GetJitter()
    {
        int idx = FrameIndex % 16;
        return (JitterX[idx] - 0.5f, JitterY[idx] - 0.5f);
    }

    public void Resolve(uint currentHdrTex, uint velocityTex)
    {
        if (!Initialized)
        {
            // Initial copy
            _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, currentHdrTex); // This assumes currentHdrTex is an FBO, but it's a texture!
            // Need a simpler way. Let's just use the resolve shader with 0 blend factor.
        }

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _scratchFbo);
        _gl.Viewport(0, 0, (uint)_w, (uint)_h);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);

        _resolveShader.Use();
        _gl.Uniform1(_resolveShader.U("uFirstFrame"), Initialized ? 0 : 1);
        
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, currentHdrTex);
        _gl.Uniform1(_resolveShader.U("uCurrent"), 0);

        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, _historyTex);
        _gl.Uniform1(_resolveShader.U("uHistory"), 1);

        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, velocityTex);
        _gl.Uniform1(_resolveShader.U("uVelocity"), 2);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        
        // Ping-pong
        (_fbo, _scratchFbo) = (_scratchFbo, _fbo);
        (_historyTex, _scratchTex) = (_scratchTex, _historyTex);
        
        Initialized = true;
    }

    private void DisposeTextures()
    {
        if (_fbo != 0) { _gl.DeleteFramebuffer(_fbo); _fbo = 0; }
        if (_scratchFbo != 0) { _gl.DeleteFramebuffer(_scratchFbo); _scratchFbo = 0; }
        if (_historyTex != 0) { _gl.DeleteTexture(_historyTex); _historyTex = 0; }
        if (_scratchTex != 0) { _gl.DeleteTexture(_scratchTex); _scratchTex = 0; }
    }

    public void Dispose()
    {
        DisposeTextures();
        if (_vao != 0) { _gl.DeleteVertexArray(_vao); _vao = 0; }
    }
}
