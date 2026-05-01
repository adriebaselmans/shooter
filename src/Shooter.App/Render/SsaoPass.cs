using System.Numerics;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Screen-space ambient occlusion. Runs after opaque passes have filled depth and
/// the view-space normal attachment of <see cref="HdrTarget"/>. Output is a single-channel
/// AO buffer that <see cref="PostFx"/> samples to modulate the tone-mapped color.</summary>
public sealed class SsaoPass : IDisposable
{
    private const int KernelSize = 16;
    private const int NoiseSize = 4;

    private readonly GL _gl;
    private readonly ShaderProgram _ssao;
    private readonly ShaderProgram _blur;
    private uint _vao;

    private uint _aoFbo, _aoTex;
    private uint _blurFbo, _blurTex;
    private int _w, _h;

    private uint _noiseTex;
    private readonly float[] _kernel = new float[KernelSize * 3];

    /// <summary>Final blurred AO texture (R16F). 1.0 = fully lit, 0.0 = fully occluded.</summary>
    public uint AoTex => _blurTex;

    public SsaoPass(GL gl)
    {
        _gl = gl;
        _ssao = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.SsaoFrag);
        _blur = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.SsaoBlurFrag);
        _vao = gl.GenVertexArray();

        BuildKernel();
        BuildNoise();
    }

    private void BuildKernel()
    {
        // Cosine-distributed hemisphere samples in tangent space (z-up). Closer samples
        // get more weight by squaring the radius bias.
        var rng = new Random(0xACE);
        for (int i = 0; i < KernelSize; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1);
            float y = (float)(rng.NextDouble() * 2 - 1);
            float z = (float)rng.NextDouble(); // z >= 0 → upper hemisphere
            var v = Vector3.Normalize(new Vector3(x, y, z));
            v *= (float)rng.NextDouble();
            // Bias samples closer to the origin (more contact-shadow-like).
            float scale = i / (float)KernelSize;
            scale = 0.1f + scale * scale * 0.9f;
            v *= scale;
            _kernel[i * 3 + 0] = v.X;
            _kernel[i * 3 + 1] = v.Y;
            _kernel[i * 3 + 2] = v.Z;
        }
    }

    private unsafe void BuildNoise()
    {
        // 4×4 random rotations around the surface normal. Stored as RGB16F.
        var rng = new Random(0xBEE);
        var data = new float[NoiseSize * NoiseSize * 3];
        for (int i = 0; i < NoiseSize * NoiseSize; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1);
            float y = (float)(rng.NextDouble() * 2 - 1);
            data[i * 3 + 0] = x;
            data[i * 3 + 1] = y;
            data[i * 3 + 2] = 0f;
        }
        _noiseTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _noiseTex);
        fixed (float* p = data)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgb16f,
                NoiseSize, NoiseSize, 0, PixelFormat.Rgb, PixelType.Float, p);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
    }

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (width == _w && height == _h && _aoFbo != 0) return;
        DisposeChain();
        _w = width; _h = height;
        (_aoFbo, _aoTex) = AllocAo(width, height);
        (_blurFbo, _blurTex) = AllocAo(width, height);
    }

    private (uint fbo, uint tex) AllocAo(int w, int h)
    {
        uint tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        unsafe
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R16f,
                (uint)w, (uint)h, 0, PixelFormat.Red, PixelType.HalfFloat, (void*)0);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        uint fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, tex, 0);
        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"SSAO FBO incomplete: {status}");
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return (fbo, tex);
    }

    /// <summary>Run SSAO + bilateral-edge-aware blur. Produces <see cref="AoTex"/>.</summary>
    public unsafe void Run(uint depthTex, uint normalTex, Matrix4x4 proj,
        float radius, float bias)
    {
        if (_aoFbo == 0) return;

        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(false);
        _gl.BindVertexArray(_vao);

        Matrix4x4.Invert(proj, out var invProj);

        // 1. SSAO
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _aoFbo);
        _gl.Viewport(0, 0, (uint)_w, (uint)_h);
        _ssao.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, depthTex);
        _gl.Uniform1(_ssao.U("uDepth"), 0);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, normalTex);
        _gl.Uniform1(_ssao.U("uNormal"), 1);
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, _noiseTex);
        _gl.Uniform1(_ssao.U("uNoise"), 2);
        _gl.Uniform2(_ssao.U("uNoiseScale"), _w / (float)NoiseSize, _h / (float)NoiseSize);
        _gl.Uniform1(_ssao.U("uRadius"), radius);
        _gl.Uniform1(_ssao.U("uBias"), bias);
        UploadMatrix(_ssao.U("uProj"), proj);
        UploadMatrix(_ssao.U("uInvProj"), invProj);
        fixed (float* p = _kernel) _gl.Uniform3(_ssao.U("uSamples"), KernelSize, p);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);

        // 2. Edge-aware blur (uses depth to reject neighbours across silhouettes).
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFbo);
        _gl.Viewport(0, 0, (uint)_w, (uint)_h);
        _blur.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _aoTex);
        _gl.Uniform1(_blur.U("uAo"), 0);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, depthTex);
        _gl.Uniform1(_blur.U("uDepth"), 1);
        _gl.Uniform2(_blur.U("uTexel"), 1f / _w, 1f / _h);
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
        if (_aoFbo != 0) { _gl.DeleteFramebuffer(_aoFbo); _aoFbo = 0; }
        if (_aoTex != 0) { _gl.DeleteTexture(_aoTex); _aoTex = 0; }
        if (_blurFbo != 0) { _gl.DeleteFramebuffer(_blurFbo); _blurFbo = 0; }
        if (_blurTex != 0) { _gl.DeleteTexture(_blurTex); _blurTex = 0; }
    }

    public void Dispose()
    {
        DisposeChain();
        if (_noiseTex != 0) { _gl.DeleteTexture(_noiseTex); _noiseTex = 0; }
        if (_vao != 0) { _gl.DeleteVertexArray(_vao); _vao = 0; }
        _ssao.Dispose();
        _blur.Dispose();
    }
}
