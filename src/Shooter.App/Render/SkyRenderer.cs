using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Renders the procedural HDR sky as a unit cube around the camera, sitting at the
/// far plane (depth = 1) so any opaque geometry drawn before depth-test occludes it.</summary>
public sealed class SkyRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;

    public SkyRenderer(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Shaders.SkyVert, Shaders.SkyFrag);

        // Unit cube, positions only.
        Span<float> verts =
        [
            -1, -1, -1,  1, -1, -1,  1,  1, -1, -1,  1, -1,
            -1, -1,  1,  1, -1,  1,  1,  1,  1, -1,  1,  1,
        ];
        Span<uint> idx =
        [
            0,1,2, 0,2,3,
            4,6,5, 4,7,6,
            0,4,5, 0,5,1,
            2,6,7, 2,7,3,
            0,3,7, 0,7,4,
            1,5,6, 1,6,2,
        ];
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        _ebo = gl.GenBuffer();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            fixed (float* p = verts)
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(verts.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            fixed (uint* p = idx)
                gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(idx.Length * sizeof(uint)), p, BufferUsageARB.StaticDraw);
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        }
        gl.BindVertexArray(0);
    }

    /// <summary>Draws the sky into the currently bound HDR framebuffer.</summary>
    public unsafe void Draw(Matrix4x4 viewNoTrans, Matrix4x4 proj, LightingEnvironment env)
    {
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.DepthMask(false);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);

        _shader.Use();
        UploadMatrix(_shader.U("uViewNoTrans"), viewNoTrans);
        UploadMatrix(_shader.U("uProj"), proj);
        var s = env.ToSun;
        _gl.Uniform3(_shader.U("uToSun"), s.X, s.Y, s.Z);
        _gl.Uniform1(_shader.U("uTurbidity"), env.Turbidity);
        _gl.Uniform3(_shader.U("uGroundAlbedo"), env.GroundAlbedo.X, env.GroundAlbedo.Y, env.GroundAlbedo.Z);

        _gl.BindVertexArray(_vao);
        _gl.DrawElements(PrimitiveType.Triangles, 36, DrawElementsType.UnsignedInt, (void*)0);
        _gl.BindVertexArray(0);

        _gl.DepthMask(true);
        _gl.DepthFunc(DepthFunction.Less);
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
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteVertexArray(_vao);
        _shader.Dispose();
    }
}
