using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>2048² depth-only directional shadow map for the sun.
/// Each frame the light-space matrix is fitted to a square around the player's current
/// position with texel snapping to eliminate shimmer.</summary>
public sealed class ShadowMap : IDisposable
{
    public const int Size = 2048;
    public const float HalfExtent = 30f;   // metres covered around the player on each axis
    public const float Near = -40f;
    public const float Far = 40f;

    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    public uint Fbo { get; private set; }
    public uint DepthTex { get; private set; }
    public Matrix4x4 LightSpace { get; private set; } = Matrix4x4.Identity;

    public ShadowMap(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Shaders.ShadowDepthVert, Shaders.ShadowDepthFrag);

        Fbo = gl.GenFramebuffer();
        DepthTex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, DepthTex);
        unsafe
        {
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24,
                Size, Size, 0, PixelFormat.DepthComponent, PixelType.Float, (void*)0);
        }
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToBorder);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToBorder);
        Span<float> border = [1f, 1f, 1f, 1f];
        unsafe { fixed (float* p = border) gl.TexParameter(TextureTarget.Texture2D, GLEnum.TextureBorderColor, p); }
        // Hardware PCF: depth comparison against r component.
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)GLEnum.CompareRefToTexture);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)GLEnum.Lequal);

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
            TextureTarget.Texture2D, DepthTex, 0);
        gl.DrawBuffer(DrawBufferMode.None);
        gl.ReadBuffer(ReadBufferMode.None);
        var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"ShadowMap framebuffer incomplete: {status}");
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    /// <summary>Computes a stable light-space matrix centered on the player position. Snaps the
    /// orthographic origin to the nearest shadowmap texel so the shadow does not shimmer when
    /// the player moves a sub-texel distance.</summary>
    public Matrix4x4 BuildLightSpace(Vector3 playerPos, LightingEnvironment env)
    {
        var sunDir = Vector3.Normalize(env.SunDirection);
        // Light camera looks along sunDir, so the light's "from" position is opposite the sun.
        var lightPos = playerPos - sunDir * 20f;
        var up = MathF.Abs(sunDir.Y) > 0.95f ? Vector3.UnitX : Vector3.UnitY;
        var view = Matrix4x4.CreateLookAt(lightPos, lightPos + sunDir, up);

        // Texel snapping: project player center, round to nearest texel in light-space, undo.
        var center = Vector3.Transform(playerPos, view);
        float texel = (HalfExtent * 2f) / Size;
        center.X = MathF.Round(center.X / texel) * texel;
        center.Y = MathF.Round(center.Y / texel) * texel;
        var snappedView = view * Matrix4x4.CreateTranslation(
            new Vector3(
                Vector3.Transform(playerPos, view).X - center.X,
                Vector3.Transform(playerPos, view).Y - center.Y,
                0f) * -1f);

        var proj = Matrix4x4.CreateOrthographicOffCenter(
            -HalfExtent, HalfExtent, -HalfExtent, HalfExtent, Near, Far);
        return snappedView * proj;
    }

    /// <summary>Renders depth for all world brushes from the sun's perspective.
    /// Earlier versions used front-face culling + a strong polygon offset, which reduced acne
    /// but made contact shadows visibly detach from walls/crates/floor (peter-panning).
    /// For this low-poly boxy scene we prefer slightly more self-shadow risk over the
    /// "hovering geometry" look, so the pass now uses back-face culling and a gentler offset.</summary>
    public unsafe void RenderPass(IReadOnlyList<WorldBrush> brushes, Dictionary<Guid, GlMesh> brushMeshes,
        Matrix4x4 lightSpace)
    {
        LightSpace = lightSpace;

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        _gl.Viewport(0, 0, Size, Size);
        _gl.Clear(ClearBufferMask.DepthBufferBit);

        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.Enable(EnableCap.PolygonOffsetFill);
        _gl.PolygonOffset(0.75f, 1.5f);

        _shader.Use();
        UploadMatrix(_shader.U("uLightSpace"), lightSpace);

        foreach (var wb in brushes)
        {
            if (!brushMeshes.TryGetValue(wb.BrushId, out var mesh)) continue;
            UploadMatrix(_shader.U("uModel"), wb.Model);
            mesh.Bind();
            _gl.DrawElements(PrimitiveType.Triangles, (uint)mesh.IndexCount, DrawElementsType.UnsignedInt, (void*)0);
        }
        _gl.BindVertexArray(0);

        _gl.Disable(EnableCap.PolygonOffsetFill);
        _gl.CullFace(TriangleFace.Back);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
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

    public void Dispose()
    {
        if (Fbo != 0) { _gl.DeleteFramebuffer(Fbo); Fbo = 0; }
        if (DepthTex != 0) { _gl.DeleteTexture(DepthTex); DepthTex = 0; }
        _shader.Dispose();
    }
}
