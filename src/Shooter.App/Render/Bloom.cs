using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>HDR bloom: threshold the HDR buffer, downsample 5 times, upsample with tent filter,
/// produce a bloom texture in <see cref="OutputTex"/> at the size of the first mip.</summary>
public sealed class Bloom : IDisposable
{
    private const int MipCount = 5;

    private readonly GL _gl;
    private readonly ShaderProgram _threshold;
    private readonly ShaderProgram _down;
    private readonly ShaderProgram _up;

    private uint _vao; // empty VAO for fullscreen-triangle draws

    private readonly uint[] _fbo = new uint[MipCount];
    private readonly uint[] _tex = new uint[MipCount];
    private readonly int[] _w = new int[MipCount];
    private readonly int[] _h = new int[MipCount];

    /// <summary>Final bloom output (mip 0 size).</summary>
    public uint OutputTex => _tex[0];
    public int OutputWidth => _w[0];
    public int OutputHeight => _h[0];

    private int _curW, _curH;

    public Bloom(GL gl)
    {
        _gl = gl;
        _threshold = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.BloomThresholdFrag);
        _down = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.BloomDownFrag);
        _up = new ShaderProgram(gl, Shaders.FullscreenVert, Shaders.BloomUpFrag);

        _vao = gl.GenVertexArray();
    }

    /// <summary>Allocate or reallocate the mip chain to match the given input size.</summary>
    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (width == _curW && height == _curH && _fbo[0] != 0) return;

        DisposeChain();
        _curW = width;
        _curH = height;

        for (int i = 0; i < MipCount; i++)
        {
            int w = Math.Max(1, width >> (i + 1));   // mip 0 is half-res of input
            int h = Math.Max(1, height >> (i + 1));
            _w[i] = w;
            _h[i] = h;
            _tex[i] = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _tex[i]);
            unsafe
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f,
                    (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.HalfFloat, (void*)0);
            }
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

            _fbo[i] = _gl.GenFramebuffer();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo[i]);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, _tex[i], 0);
            var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete)
                throw new InvalidOperationException($"Bloom mip {i} framebuffer incomplete: {status}");
        }
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    /// <summary>Run the bloom passes against <paramref name="hdrColorTex"/>.
    /// Leaves the GL state with default framebuffer unbound.</summary>
    public void Run(uint hdrColorTex)
    {
        if (_fbo[0] == 0) return;

        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(false);

        _gl.BindVertexArray(_vao);

        // 1. Threshold: HDR → mip 0
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo[0]);
        _gl.Viewport(0, 0, (uint)_w[0], (uint)_h[0]);
        _threshold.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, hdrColorTex);
        _gl.Uniform1(_threshold.U("uSrc"), 0);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);

        // 2. Downsample mip 0 → mip 1 .. mip N-1
        _down.Use();
        _gl.Uniform1(_down.U("uSrc"), 0);
        for (int i = 1; i < MipCount; i++)
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo[i]);
            _gl.Viewport(0, 0, (uint)_w[i], (uint)_h[i]);
            _gl.BindTexture(TextureTarget.Texture2D, _tex[i - 1]);
            _gl.Uniform2(_down.U("uTexel"), 1f / _w[i - 1], 1f / _h[i - 1]);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        // 3. Upsample additively: mip N-1 → mip N-2, ..., → mip 0
        _up.Use();
        _gl.Uniform1(_up.U("uSrc"), 0);
        _gl.Uniform1(_up.U("uRadius"), 1.0f);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.One, BlendingFactor.One); // additive
        for (int i = MipCount - 1; i >= 1; i--)
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo[i - 1]);
            _gl.Viewport(0, 0, (uint)_w[i - 1], (uint)_h[i - 1]);
            _gl.BindTexture(TextureTarget.Texture2D, _tex[i]);
            _gl.Uniform2(_up.U("uTexel"), 1f / _w[i], 1f / _h[i]);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }
        _gl.Disable(EnableCap.Blend);

        _gl.BindVertexArray(0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.DepthTest);
    }

    private void DisposeChain()
    {
        for (int i = 0; i < MipCount; i++)
        {
            if (_fbo[i] != 0) { _gl.DeleteFramebuffer(_fbo[i]); _fbo[i] = 0; }
            if (_tex[i] != 0) { _gl.DeleteTexture(_tex[i]); _tex[i] = 0; }
        }
    }

    public void Dispose()
    {
        DisposeChain();
        if (_vao != 0) { _gl.DeleteVertexArray(_vao); _vao = 0; }
        _threshold.Dispose();
        _down.Dispose();
        _up.Dispose();
    }
}
