using System.Numerics;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Half-resolution screen-space reflections with specular-environment fallback.
/// First implementation focuses on stable hybrid composition rather than exhaustive quality.</summary>
public sealed class SsrPass : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _trace;
    private readonly ShaderProgram _temporal;
    private readonly ShaderProgram _composite;
    private readonly uint _vao;
    private uint _traceFbo;
    private uint _traceTex;
    private readonly uint[] _historyFbo = new uint[2];
    private readonly uint[] _historyTex = new uint[2];
    private int _historyWriteIndex;
    private bool _hasHistory;
    private int _w;
    private int _h;

    public SsrPass(GL gl)
    {
        _gl = gl;
        _trace = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.SsrTraceFrag);
        _temporal = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.SsrTemporalFrag);
        _composite = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.SsrCompositeFrag);
        _vao = gl.GenVertexArray();
    }

    public void Resize(int width, int height)
    {
        width = Math.Max(1, width / 2);
        height = Math.Max(1, height / 2);
        if (width == _w && height == _h && _traceFbo != 0) return;
        DisposeChain();
        _w = width;
        _h = height;

        _traceTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _traceTex);
        unsafe
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, (uint)_w, (uint)_h, 0, PixelFormat.Rgba, PixelType.HalfFloat, (void*)0);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _traceFbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _traceFbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _traceTex, 0);
        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"SSR framebuffer incomplete: {status}");

        for (int i = 0; i < 2; i++)
        {
            _historyTex[i] = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _historyTex[i]);
            unsafe
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f, (uint)_w, (uint)_h, 0, PixelFormat.Rgba, PixelType.HalfFloat, (void*)0);
            }
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            _historyFbo[i] = _gl.GenFramebuffer();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _historyFbo[i]);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _historyTex[i], 0);
            status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete)
                throw new InvalidOperationException($"SSR history framebuffer incomplete: {status}");
        }

        _historyWriteIndex = 0;
        _hasHistory = false;
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Run(uint sceneColorTex, GBufferTarget gbuffer, HdrTarget hdrTarget, IblProbe ibl, Matrix4x4 view, Matrix4x4 proj, Matrix4x4 prevViewProj)
    {
        if (_traceFbo == 0) return;

        Matrix4x4.Invert(proj, out var invProj);
        Matrix4x4.Invert(view, out var invView);

        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(false);
        _gl.BindVertexArray(_vao);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _traceFbo);
        _gl.Viewport(0, 0, (uint)_w, (uint)_h);
        _trace.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, sceneColorTex);
        _gl.Uniform1(_trace.U("uSceneColor"), 0);
        _gl.ActiveTexture(TextureUnit.Texture3);
        _gl.BindTexture(TextureTarget.Texture2D, gbuffer.DepthTex);
        _gl.Uniform1(_trace.U("uDepth"), 3);
        _gl.ActiveTexture(TextureUnit.Texture11);
        _gl.BindTexture(TextureTarget.Texture2D, gbuffer.G1Tex);
        _gl.Uniform1(_trace.U("uNormal"), 11);
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, gbuffer.G2Tex);
        _gl.Uniform1(_trace.U("uMaterial"), 2);
        _gl.ActiveTexture(TextureUnit.Texture15);
        _gl.BindTexture(TextureTarget.TextureCubeMap, ibl.SpecularCube);
        _gl.Uniform1(_trace.U("uSpecularEnv"), 15);
        UploadMatrix(_trace.U("uProj"), proj);
        UploadMatrix(_trace.U("uInvProj"), invProj);
        UploadMatrix(_trace.U("uInvView"), invView);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);

        int readIndex = 1 - _historyWriteIndex;
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _historyFbo[_historyWriteIndex]);
        _gl.Viewport(0, 0, (uint)_w, (uint)_h);
        _temporal.Use();
        _gl.ActiveTexture(TextureUnit.Texture13);
        _gl.BindTexture(TextureTarget.Texture2D, _traceTex);
        _gl.Uniform1(_temporal.U("uCurrentSsr"), 13);
        _gl.ActiveTexture(TextureUnit.Texture14);
        _gl.BindTexture(TextureTarget.Texture2D, _historyTex[readIndex]);
        _gl.Uniform1(_temporal.U("uHistorySsr"), 14);
        _gl.ActiveTexture(TextureUnit.Texture3);
        _gl.BindTexture(TextureTarget.Texture2D, gbuffer.DepthTex);
        _gl.Uniform1(_temporal.U("uDepth"), 3);
        Matrix4x4 currentViewProj = view * proj;
        Matrix4x4.Invert(currentViewProj, out var invViewProj);
        UploadMatrix(_temporal.U("uInvViewProj"), invViewProj);
        UploadMatrix(_temporal.U("uPrevViewProj"), prevViewProj);
        _gl.Uniform1(_temporal.U("uUseHistory"), _hasHistory ? 1 : 0);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);

        hdrTarget.Bind();
        _composite.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, sceneColorTex);
        _gl.Uniform1(_composite.U("uScene"), 0);
        _gl.ActiveTexture(TextureUnit.Texture13);
        _gl.BindTexture(TextureTarget.Texture2D, _historyTex[_historyWriteIndex]);
        _gl.Uniform1(_composite.U("uSsr"), 13);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);

        _historyWriteIndex = readIndex;
        _hasHistory = true;

        _gl.BindVertexArray(0);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.DepthTest);
    }

    private unsafe void UploadMatrix(int loc, Matrix4x4 m)
    {
        Span<float> data =
        [
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44,
        ];
        fixed (float* p = data)
            _gl.UniformMatrix4(loc, 1, false, p);
    }

    private void DisposeChain()
    {
        if (_traceFbo != 0) { _gl.DeleteFramebuffer(_traceFbo); _traceFbo = 0; }
        if (_traceTex != 0) { _gl.DeleteTexture(_traceTex); _traceTex = 0; }
        for (int i = 0; i < 2; i++)
        {
            if (_historyFbo[i] != 0) { _gl.DeleteFramebuffer(_historyFbo[i]); _historyFbo[i] = 0; }
            if (_historyTex[i] != 0) { _gl.DeleteTexture(_historyTex[i]); _historyTex[i] = 0; }
        }
        _hasHistory = false;
    }

    public void Dispose()
    {
        DisposeChain();
        _gl.DeleteVertexArray(_vao);
        _trace.Dispose();
        _temporal.Dispose();
        _composite.Dispose();
    }
}
