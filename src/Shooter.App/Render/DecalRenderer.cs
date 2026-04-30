using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Renders bullet-hole decals as small flat quads aligned to surface normals.</summary>
public sealed class DecalRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly DynamicPosBuffer _buffer;

    public DecalRenderer(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Shaders.DecalVert, Shaders.DecalFrag);
        _buffer = new DynamicPosBuffer(gl, componentsPerVertex: 3);
    }

    public unsafe void Draw(Matrix4x4 viewProj, BulletHoleManager holes)
    {
        if (holes.Count == 0) return;
        // Build 6 verts per quad
        var data = new float[holes.Count * 6 * 3];
        int o = 0;
        float s = BulletHoleManager.QuadHalfSize;
        foreach (var h in holes.Holes)
        {
            // build basis around normal
            Vector3 n = h.Normal;
            Vector3 up = MathF.Abs(n.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 right = Vector3.Normalize(Vector3.Cross(n, up));
            up = Vector3.Cross(right, n);

            Vector3 p = h.Position;
            Vector3 v00 = p - right * s - up * s;
            Vector3 v10 = p + right * s - up * s;
            Vector3 v11 = p + right * s + up * s;
            Vector3 v01 = p - right * s + up * s;

            void W(Vector3 v) { data[o++] = v.X; data[o++] = v.Y; data[o++] = v.Z; }
            W(v00); W(v10); W(v11);
            W(v00); W(v11); W(v01);
        }

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
        _gl.DepthFunc(DepthFunction.Lequal);

        _buffer.Upload(data);
        _buffer.Bind();
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(holes.Count * 6));
        _gl.BindVertexArray(0);
        _gl.DepthFunc(DepthFunction.Less);
    }

    public void Dispose()
    {
        _shader.Dispose();
        _buffer.Dispose();
    }
}
