using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Final post pass: HDR + bloom × strength → SSAO modulate → ACES → gamma → default FB.</summary>
public sealed class PostFx : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private uint _vao;


    public PostFx(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.PostFxFrag);
        _vao = gl.GenVertexArray();
    }

    public void Draw(uint hdrTex, uint bloomTex, uint aoTex, LightingEnvironment env,
        float aoStrength, float exposure, int targetWidth, int targetHeight)
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)targetWidth, (uint)targetHeight);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(false);

        _shader.Use();
        _gl.Uniform1(_shader.U("uExposure"), exposure);
        _gl.Uniform1(_shader.U("uBloomStrength"), env.BloomStrength);
        _gl.Uniform1(_shader.U("uAoStrength"), aoStrength);
        _gl.Uniform1(_shader.U("uContrast"), env.GradeContrast);
        _gl.Uniform1(_shader.U("uSaturation"), env.GradeSaturation);
        _gl.Uniform1(_shader.U("uShadowCool"), env.GradeShadowCool);
        _gl.Uniform1(_shader.U("uHighlightWarm"), env.GradeHighlightWarm);
        _gl.Uniform1(_shader.U("uVignetteStrength"), env.VignetteStrength);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, hdrTex);
        _gl.Uniform1(_shader.U("uHdr"), 0);

        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, bloomTex);
        _gl.Uniform1(_shader.U("uBloom"), 1);

        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, aoTex);
        _gl.Uniform1(_shader.U("uAo"), 2);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        _gl.BindVertexArray(0);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.DepthTest);
    }

    public void Dispose()
    {
        if (_vao != 0) { _gl.DeleteVertexArray(_vao); _vao = 0; }
        _shader.Dispose();
    }
}
