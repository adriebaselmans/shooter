using System.Numerics;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Cheap screen-space contact shadow visibility for deferred opaque lighting.
/// Produces a single-channel visibility texture where 1 = fully lit, 0 = fully occluded.</summary>
public sealed class ContactShadowPass : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly uint _vao;
    private uint _fbo;
    private uint _tex;
    private int _w;
    private int _h;

    public uint VisibilityTex => _tex;

    public ContactShadowPass(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.ContactShadowFrag);
        _vao = gl.GenVertexArray();
    }

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (width == _w && height == _h && _fbo != 0) return;
        DisposeChain();
        _w = width;
        _h = height;

        _tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _tex);
        unsafe
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R16f, (uint)width, (uint)height, 0, PixelFormat.Red, PixelType.HalfFloat, (void*)0);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _tex, 0);
        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"ContactShadow FBO incomplete: {status}");
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public unsafe void Run(uint depthTex, uint normalTex, Matrix4x4 proj, Vector3 lightDirView, float maxDistance = 1.2f, float thickness = 0.10f, float bias = 0.03f)
    {
        if (_fbo == 0) return;

        Matrix4x4.Invert(proj, out var invProj);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.Viewport(0, 0, (uint)_w, (uint)_h);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(false);

        _shader.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, depthTex);
        _gl.Uniform1(_shader.U("uDepth"), 0);
        _gl.ActiveTexture(TextureUnit.Texture11);
        _gl.BindTexture(TextureTarget.Texture2D, normalTex);
        _gl.Uniform1(_shader.U("uNormal"), 11);
        UploadMatrix(_shader.U("uProj"), proj);
        UploadMatrix(_shader.U("uInvProj"), invProj);
        _gl.Uniform3(_shader.U("uLightDirView"), lightDirView.X, lightDirView.Y, lightDirView.Z);
        _gl.Uniform1(_shader.U("uMaxDistance"), maxDistance);
        _gl.Uniform1(_shader.U("uThickness"), thickness);
        _gl.Uniform1(_shader.U("uBias"), bias);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        _gl.BindVertexArray(0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
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
        if (_fbo != 0) { _gl.DeleteFramebuffer(_fbo); _fbo = 0; }
        if (_tex != 0) { _gl.DeleteTexture(_tex); _tex = 0; }
    }

    public void Dispose()
    {
        DisposeChain();
        _gl.DeleteVertexArray(_vao);
        _shader.Dispose();
    }
}
