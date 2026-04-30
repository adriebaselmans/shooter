using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Renders <see cref="ScorchManager"/> entries as soft, noise-perturbed dark smudges
/// stuck to surface normals. Used for rocket detonations.</summary>
public sealed class ScorchRenderer : IDisposable
{
    private const int FloatsPerVert = 6; // pos3 + uv2 + seed1
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly uint _vao;
    private readonly uint _vbo;
    private int _capacityFloats;

    public unsafe ScorchRenderer(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Shaders.ScorchVert, Shaders.ScorchFrag);
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        uint stride = (uint)(FloatsPerVert * sizeof(float));
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, (void*)(5 * sizeof(float)));
        gl.BindVertexArray(0);
    }

    public unsafe void Draw(Matrix4x4 viewProj, ScorchManager scorches)
    {
        if (scorches.Count == 0) return;

        // 6 verts per quad.
        var data = new float[scorches.Count * 6 * FloatsPerVert];
        int o = 0;
        foreach (var s in scorches.Scorches)
        {
            // Build a tangent basis around the surface normal.
            Vector3 n = s.Normal;
            Vector3 up = MathF.Abs(n.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 right = Vector3.Normalize(Vector3.Cross(n, up));
            up = Vector3.Cross(right, n);

            float h = s.HalfSize;
            Vector3 p = s.Position;
            Vector3 v00 = p - right * h - up * h;
            Vector3 v10 = p + right * h - up * h;
            Vector3 v11 = p + right * h + up * h;
            Vector3 v01 = p - right * h + up * h;

            void W(Vector3 v, float u, float vv, float seed)
            {
                data[o++] = v.X; data[o++] = v.Y; data[o++] = v.Z;
                data[o++] = u; data[o++] = vv;
                data[o++] = seed;
            }
            W(v00, -1f, -1f, s.Seed); W(v10, 1f, -1f, s.Seed); W(v11, 1f, 1f, s.Seed);
            W(v00, -1f, -1f, s.Seed); W(v11, 1f, 1f, s.Seed); W(v01, -1f, 1f, s.Seed);
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        if (data.Length > _capacityFloats)
        {
            _capacityFloats = Math.Max(data.Length, _capacityFloats * 2);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_capacityFloats * sizeof(float)), null, BufferUsageARB.DynamicDraw);
        }
        fixed (float* pp = data)
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(data.Length * sizeof(float)), pp);

        _shader.Use();
        Span<float> mat =
        [
            viewProj.M11, viewProj.M12, viewProj.M13, viewProj.M14,
            viewProj.M21, viewProj.M22, viewProj.M23, viewProj.M24,
            viewProj.M31, viewProj.M32, viewProj.M33, viewProj.M34,
            viewProj.M41, viewProj.M42, viewProj.M43, viewProj.M44,
        ];
        fixed (float* p = mat) _gl.UniformMatrix4(_shader.U("uViewProj"), 1, false, p);

        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.DepthMask(false);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(scorches.Count * 6));
        _gl.BindVertexArray(0);

        _gl.DepthMask(true);
        _gl.DepthFunc(DepthFunction.Less);
        _gl.Disable(EnableCap.Blend);
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _shader.Dispose();
    }
}
