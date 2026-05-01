using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Eye-adaptation auto-exposure. Each frame:
/// 1. Render log(luminance) of the HDR scene into a 256² R16F texture.
/// 2. glGenerateMipmap → the 1×1 mip is the geometric mean log-luminance.
/// 3. Read back the 1×1 mip via glGetTexImage (a single half-float).
/// 4. Smooth on CPU with framerate-independent exponential decay.
/// 5. Convert to exposure scalar via the photographic key value (0.18 / Lavg), clamped.
/// </summary>
public sealed class AutoExposure : IDisposable
{
    private const int LumSize = 256; // power-of-two so mipmap chain bottoms at 1×1

    private readonly GL _gl;
    private readonly ShaderProgram _lum;
    private uint _vao;
    private uint _fbo, _tex;
    private bool _hasSample;
    private float _smoothedLogLum;

    /// <summary>Latest smoothed exposure value, ready to feed into PostFx.</summary>
    public float CurrentExposure { get; private set; } = 1.0f;

    public AutoExposure(GL gl)
    {
        _gl = gl;
        _lum = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.LogLuminanceFrag);
        _vao = gl.GenVertexArray();
        AllocateLumTarget();
    }

    private unsafe void AllocateLumTarget()
    {
        _tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _tex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R16f,
            LumSize, LumSize, 0, PixelFormat.Red, PixelType.HalfFloat, (void*)0);
        // We need mipmaps: linear filtering with mipmap min filter so glGenerateMipmap works.
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, _tex, 0);
        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"AutoExposure FBO incomplete: {status}");
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    /// <summary>Sample the HDR scene, update <see cref="CurrentExposure"/>.</summary>
    public unsafe void Run(uint hdrColorTex, LightingEnvironment env, float dt)
    {
        if (!env.AutoExposureEnabled)
        {
            CurrentExposure = env.Exposure;
            return;
        }

        // 1. Render log-luminance into base mip.
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.Viewport(0, 0, LumSize, LumSize);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(false);
        _lum.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, hdrColorTex);
        _gl.Uniform1(_lum.U("uSrc"), 0);
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        _gl.BindVertexArray(0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        // 2. Build mip chain. Top-of-pyramid (1×1) holds the average.
        _gl.BindTexture(TextureTarget.Texture2D, _tex);
        _gl.GenerateMipmap(TextureTarget.Texture2D);

        // 3. Read the smallest mip (1 half-float = 2 bytes). CPU/GPU sync stall is tiny here.
        // Allocate a 4-byte buffer to cover any driver oversampling on the readback.
        Span<ushort> buf = stackalloc ushort[4];
        int mipCount = (int)MathF.Log2(LumSize); // 256 → 8 levels of reduction → 1×1 at level 8
        fixed (ushort* p = buf)
        {
            _gl.GetTexImage(TextureTarget.Texture2D, mipCount,
                PixelFormat.Red, PixelType.HalfFloat, p);
        }
        float frameLogLum = HalfToFloat(buf[0]);
        if (float.IsNaN(frameLogLum) || float.IsInfinity(frameLogLum))
            frameLogLum = MathF.Log(0.18f);

        // 4. Framerate-independent exponential decay; brighten faster than darken.
        if (!_hasSample) { _smoothedLogLum = frameLogLum; _hasSample = true; }
        else
        {
            float rate = (frameLogLum > _smoothedLogLum)
                ? env.ExposureAdaptRateBrighten
                : env.ExposureAdaptRateDarken;
            float t = 1f - MathF.Exp(-MathF.Max(0f, dt) * rate);
            _smoothedLogLum += (frameLogLum - _smoothedLogLum) * t;
        }

        // 5. Photographic key value → exposure, clamped.
        float lavg = MathF.Exp(_smoothedLogLum);
        float exp = env.ExposureKey / MathF.Max(lavg, 1e-4f);
        CurrentExposure = Math.Clamp(exp, env.MinExposure, env.MaxExposure);

        _gl.DepthMask(true);
        _gl.Enable(EnableCap.DepthTest);
    }

    /// <summary>IEEE 754 half → float. Stand-alone so we don't depend on System.Half conversion
    /// being bit-identical with the GL driver's half encoding (it is, but this is explicit).</summary>
    private static float HalfToFloat(ushort h)
    {
        return (float)BitConverter.UInt16BitsToHalf(h);
    }

    public void Dispose()
    {
        if (_fbo != 0) { _gl.DeleteFramebuffer(_fbo); _fbo = 0; }
        if (_tex != 0) { _gl.DeleteTexture(_tex); _tex = 0; }
        if (_vao != 0) { _gl.DeleteVertexArray(_vao); _vao = 0; }
        _lum.Dispose();
    }
}
