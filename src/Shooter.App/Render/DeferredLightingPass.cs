using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Foundation fullscreen deferred-lighting pass consuming the resolved GBuffer.
/// This initial scaffold is intentionally conservative and non-authoritative; the runtime
/// still uses the existing forward opaque path until deferred parity is completed.</summary>
public sealed class DeferredLightingPass : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly uint _vao;
    private readonly uint _whiteTex;

    public DeferredLightingPass(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.DeferredLightingFrag);
        _vao = gl.GenVertexArray();
        _whiteTex = CreateWhiteTexture();
    }

    public void Draw(GBufferTarget gbuffer, uint ssaoTex, uint contactShadowTex, HdrTarget hdrTarget, LightingEnvironment env,
        ShadowMap shadow, IblProbe ibl, WorldRenderer worldRenderer, Vector3 cameraPos, Matrix4x4 view, Matrix4x4 viewProj)
    {
        hdrTarget.Bind();
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(false);

        _shader.Use();
        worldRenderer.BindLighting(_shader, env, shadow, ibl, cameraPos, view);
        Matrix4x4.Invert(viewProj, out var invViewProj);
        Matrix4x4.Invert(view, out var invView);
        UploadMatrix(_shader.U("uInvViewProj"), invViewProj);
        UploadMatrix(_shader.U("uInvView"), invView);
        _gl.Uniform1(_shader.U("uReceiveShadows"), env.ShadowsEnabled ? 1 : 0);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, gbuffer.G0Tex);
        _gl.Uniform1(_shader.U("uG0"), 0);

        _gl.ActiveTexture(TextureUnit.Texture11);
        _gl.BindTexture(TextureTarget.Texture2D, gbuffer.G1Tex);
        _gl.Uniform1(_shader.U("uG1"), 11);

        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, gbuffer.G2Tex);
        _gl.Uniform1(_shader.U("uG2"), 2);

        _gl.ActiveTexture(TextureUnit.Texture3);
        _gl.BindTexture(TextureTarget.Texture2D, gbuffer.DepthTex);
        _gl.Uniform1(_shader.U("uDepth"), 3);

        _gl.ActiveTexture(TextureUnit.Texture9);
        _gl.BindTexture(TextureTarget.Texture2D, ssaoTex != 0 ? ssaoTex : _whiteTex);
        _gl.Uniform1(_shader.U("uSsao"), 9);

        _gl.ActiveTexture(TextureUnit.Texture10);
        _gl.BindTexture(TextureTarget.Texture2D, contactShadowTex != 0 ? contactShadowTex : _whiteTex);
        _gl.Uniform1(_shader.U("uContactShadow"), 10);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
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

    private uint CreateWhiteTexture()
    {
        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        unsafe
        {
            byte value = 255;
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8, 1, 1, 0, PixelFormat.Red, PixelType.UnsignedByte, &value);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        return handle;
    }

    public void Dispose()
    {
        _gl.DeleteTexture(_whiteTex);
        _gl.DeleteVertexArray(_vao);
        _shader.Dispose();
    }
}
