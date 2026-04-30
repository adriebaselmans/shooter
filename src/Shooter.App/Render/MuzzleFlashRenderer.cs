using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Renders a short-lived view-space muzzle flash anchored at <see cref="WeaponViewmodelRenderer.MuzzleViewOffset"/>.
/// Drawn after the viewmodel pass with additive blending so it always reads as a glow on top
/// of the weapon, regardless of background. View-space anchoring means it inherits the
/// viewmodel's recoil-driven kick automatically.</summary>
public sealed class MuzzleFlashRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly uint _vao;
    private readonly uint _vbo;

    public MuzzleFlashRenderer(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Shaders.MuzzleFlashVert, Shaders.MuzzleFlashFrag);

        // Unit quad in [-1,1].
        Span<float> quad = [-1f, -1f, 1f, -1f, -1f, 1f, 1f, 1f];
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            fixed (float* p = quad)
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quad.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        }
        gl.BindVertexArray(0);
    }

    /// <summary>Draws the flash if it is currently active. Must be called *after* the viewmodel
    /// has drawn so the flash sits on top of the gun. Uses additive blending and disables depth
    /// writes so it composites cleanly without polluting the depth buffer.</summary>
    public void Draw(int fbWidth, int fbHeight, MuzzleFlash flash)
    {
        if (!flash.IsActive) return;

        float aspect = fbHeight > 0 ? (float)fbWidth / fbHeight : 16f / 9f;
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(WeaponViewmodelRenderer.FovYRadians, aspect, 0.01f, 10f);

        // Anchor: barrel tip in view space. The trigger picked this per-weapon so the flash
        // lines up with the visible nozzle of whichever gun is held.
        var anchor = flash.ViewOffset;
        // Size grows with intensity so the flash "punches" outward then collapses.
        float t = flash.Intensity;          // 1 -> 0
        float pulse = 0.55f + 0.45f * t;    // peak biggest at trigger
        float halfSize = 0.18f * flash.SeedScale * flash.WeaponScale * pulse;

        _shader.Use();
        UploadMatrix(_shader.U("uProj"), proj);
        _gl.Uniform3(_shader.U("uViewCenter"), anchor.X, anchor.Y, anchor.Z);
        _gl.Uniform1(_shader.U("uIntensity"), t);

        _gl.Disable(EnableCap.DepthTest);
        _gl.DepthMask(false);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One); // additive

        _gl.BindVertexArray(_vao);
        // Draw two crossed quads, each with random rotation, for a sparkly star look.
        DrawOne(halfSize, halfSize, flash.SeedAngle);
        DrawOne(halfSize * 0.65f, halfSize * 0.65f, flash.SeedAngle + MathF.PI * 0.5f);
        _gl.BindVertexArray(0);

        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.DepthTest);
    }

    private void DrawOne(float hx, float hy, float rotation)
    {
        _gl.Uniform2(_shader.U("uHalfSize"), hx, hy);
        _gl.Uniform1(_shader.U("uRotation"), rotation);
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
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
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _shader.Dispose();
    }
}
