using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

public sealed class ParticleRenderer : IDisposable
{
    private const int FloatsPerVert = 9; // pos3 uv2 color4
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly uint _vao;
    private readonly uint _vbo;
    private int _capacityFloats;

    public unsafe ParticleRenderer(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Shaders.ParticleVert, Shaders.ParticleFrag);
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
        gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)(5 * sizeof(float)));
        gl.BindVertexArray(0);
    }

    public unsafe void Draw(Matrix4x4 viewProj, Vector3 cameraRight, Vector3 cameraUp, ParticleSystem particles)
    {
        if (particles.Active.Count == 0) return;
        var data = new float[particles.Active.Count * 6 * FloatsPerVert];
        int o = 0;
        foreach (var p in particles.Active)
        {
            float life = p.Lifetime > 0f ? Math.Clamp(1f - p.Age / p.Lifetime, 0f, 1f) : 0f;
            float alpha = p.Color.W * life;
            float size = p.Size * (p.Kind == ParticleKind.Smoke || p.Kind == ParticleKind.Dust ? (1.0f + (1f - life) * 0.55f) : 1.0f);
            Vector3 right = cameraRight * size;
            Vector3 up = cameraUp * size;
            Vector3 v00 = p.Position - right - up;
            Vector3 v10 = p.Position + right - up;
            Vector3 v11 = p.Position + right + up;
            Vector3 v01 = p.Position - right + up;
            Vector4 c = new(p.Color.X, p.Color.Y, p.Color.Z, alpha);
            Write(v00, new Vector2(-1f, -1f), c); Write(v10, new Vector2(1f, -1f), c); Write(v11, new Vector2(1f, 1f), c);
            Write(v00, new Vector2(-1f, -1f), c); Write(v11, new Vector2(1f, 1f), c); Write(v01, new Vector2(-1f, 1f), c);
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

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        if (data.Length > _capacityFloats)
        {
            _capacityFloats = Math.Max(data.Length, _capacityFloats * 2);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_capacityFloats * sizeof(float)), null, BufferUsageARB.DynamicDraw);
        }
        fixed (float* pp = data)
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(data.Length * sizeof(float)), pp);

        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.DepthMask(false);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(particles.Active.Count * 6));
        _gl.BindVertexArray(0);

        _gl.DepthMask(true);
        _gl.DepthFunc(DepthFunction.Less);
        _gl.Disable(EnableCap.Blend);
        return;

        void Write(Vector3 pos, Vector2 uv, Vector4 color)
        {
            data[o++] = pos.X; data[o++] = pos.Y; data[o++] = pos.Z;
            data[o++] = uv.X; data[o++] = uv.Y;
            data[o++] = color.X; data[o++] = color.Y; data[o++] = color.Z; data[o++] = color.W;
        }
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _shader.Dispose();
    }
}
