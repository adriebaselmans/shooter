using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Image-based ambient probe. Builds two cubemaps from the analytic sky:
/// (1) a small HDR sky cubemap, (2) a tiny diffuse-irradiance cubemap convolved from (1).
/// Lit shaders sample the irradiance cube as their ambient term.</summary>
public sealed class IblProbe : IDisposable
{
    private const int SkySize = 64;
    private const int IrradianceSize = 16;

    private readonly GL _gl;
    private readonly ShaderProgram _skyFaceShader;
    private readonly ShaderProgram _convolveShader;
    private readonly uint _quadVao;
    private readonly uint _quadVbo;
    private readonly uint _captureFbo;

    public uint SkyCube { get; private set; }
    public uint IrradianceCube { get; private set; }

    public IblProbe(GL gl)
    {
        _gl = gl;
        _skyFaceShader = new ShaderProgram(gl, Shaders.SkyFaceVert, Shaders.SkyFaceFrag);
        _convolveShader = new ShaderProgram(gl, Shaders.IrradianceConvolveVert, Shaders.IrradianceConvolveFrag);

        // Fullscreen quad in clip space [-1,1].
        Span<float> quad = [-1f, -1f, 3f, -1f, -1f, 3f];
        _quadVao = gl.GenVertexArray();
        _quadVbo = gl.GenBuffer();
        gl.BindVertexArray(_quadVao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _quadVbo);
        unsafe
        {
            fixed (float* p = quad)
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quad.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        }
        gl.BindVertexArray(0);

        _captureFbo = gl.GenFramebuffer();

        SkyCube = AllocateCubemap(SkySize);
        IrradianceCube = AllocateCubemap(IrradianceSize);
    }

    private uint AllocateCubemap(int size)
    {
        uint cube = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.TextureCubeMap, cube);
        for (int face = 0; face < 6; face++)
        {
            unsafe
            {
                _gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + face, 0,
                    InternalFormat.Rgba16f, (uint)size, (uint)size, 0,
                    PixelFormat.Rgba, PixelType.HalfFloat, (void*)0);
            }
        }
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)GLEnum.ClampToEdge);
        return cube;
    }

    /// <summary>One-shot rebuild of the sky and irradiance cubemaps from the supplied lighting
    /// environment. Restores the bound framebuffer to default before returning.</summary>
    public void Build(LightingEnvironment env)
    {
        // Cubemap face-direction bases in OpenGL convention (matches GL_TEXTURE_CUBE_MAP_*).
        // Each tuple is (forward, right, up). Forward points along the +face axis from origin.
        var faces = new (Vector3 fwd, Vector3 right, Vector3 up)[]
        {
            (new( 1, 0, 0), new( 0, 0,-1), new( 0,-1, 0)), // +X
            (new(-1, 0, 0), new( 0, 0, 1), new( 0,-1, 0)), // -X
            (new( 0, 1, 0), new( 1, 0, 0), new( 0, 0, 1)), // +Y
            (new( 0,-1, 0), new( 1, 0, 0), new( 0, 0,-1)), // -Y
            (new( 0, 0, 1), new( 1, 0, 0), new( 0,-1, 0)), // +Z
            (new( 0, 0,-1), new(-1, 0, 0), new( 0,-1, 0)), // -Z
        };

        // --- Pass 1: render analytic sky into 6 faces of SkyCube ---
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _captureFbo);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(false);

        _skyFaceShader.Use();
        var s = env.ToSun;
        _gl.Uniform3(_skyFaceShader.U("uToSun"), s.X, s.Y, s.Z);
        _gl.Uniform1(_skyFaceShader.U("uTurbidity"), env.Turbidity);
        _gl.Uniform3(_skyFaceShader.U("uGroundAlbedo"), env.GroundAlbedo.X, env.GroundAlbedo.Y, env.GroundAlbedo.Z);

        _gl.Viewport(0, 0, SkySize, SkySize);
        _gl.BindVertexArray(_quadVao);
        for (int i = 0; i < 6; i++)
        {
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.TextureCubeMapPositiveX + i, SkyCube, 0);
            var f = faces[i];
            _gl.Uniform3(_skyFaceShader.U("uFaceForward"), f.fwd.X, f.fwd.Y, f.fwd.Z);
            _gl.Uniform3(_skyFaceShader.U("uFaceRight"), f.right.X, f.right.Y, f.right.Z);
            _gl.Uniform3(_skyFaceShader.U("uFaceUp"), f.up.X, f.up.Y, f.up.Z);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        // --- Pass 2: convolve sky into IrradianceCube ---
        _convolveShader.Use();
        _gl.Uniform1(_convolveShader.U("uSky"), 0);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.TextureCubeMap, SkyCube);

        _gl.Viewport(0, 0, IrradianceSize, IrradianceSize);
        for (int i = 0; i < 6; i++)
        {
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.TextureCubeMapPositiveX + i, IrradianceCube, 0);
            var f = faces[i];
            _gl.Uniform3(_convolveShader.U("uFaceForward"), f.fwd.X, f.fwd.Y, f.fwd.Z);
            _gl.Uniform3(_convolveShader.U("uFaceRight"), f.right.X, f.right.Y, f.right.Z);
            _gl.Uniform3(_convolveShader.U("uFaceUp"), f.up.X, f.up.Y, f.up.Z);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        _gl.BindVertexArray(0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.DepthTest);
    }

    public void Dispose()
    {
        if (SkyCube != 0) _gl.DeleteTexture(SkyCube);
        if (IrradianceCube != 0) _gl.DeleteTexture(IrradianceCube);
        _gl.DeleteFramebuffer(_captureFbo);
        _gl.DeleteBuffer(_quadVbo);
        _gl.DeleteVertexArray(_quadVao);
        _skyFaceShader.Dispose();
        _convolveShader.Dispose();
        SkyCube = 0;
        IrradianceCube = 0;
    }
}
