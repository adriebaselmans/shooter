using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Draws bullet tracers as world-space line segments, fading with lifetime.</summary>
public sealed class TracerRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly DynamicPosBuffer _buffer;

    public TracerRenderer(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Shaders.TracerVert, Shaders.TracerFrag);
        _buffer = new DynamicPosBuffer(gl, componentsPerVertex: 3);
    }

    public unsafe void Draw(Matrix4x4 viewProj, TracerSystem tracers)
    {
        if (tracers.Active.Count == 0) return;

        // 2 verts per tracer.
        var data = new float[tracers.Active.Count * 2 * 3];
        int o = 0;
        foreach (var t in tracers.Active)
        {
            data[o++] = t.Start.X; data[o++] = t.Start.Y; data[o++] = t.Start.Z;
            data[o++] = t.End.X; data[o++] = t.End.Y; data[o++] = t.End.Z;
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

        // Average alpha based on fraction of remaining life across all active tracers.
        float alpha = 0f;
        foreach (var t in tracers.Active) alpha += t.Lifetime > 0 ? t.Ttl / t.Lifetime : 0f;
        alpha /= tracers.Active.Count;
        _gl.Uniform4(_shader.U("uColor"), 1.0f, 0.92f, 0.55f, MathF.Max(0.4f, alpha));

        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.LineWidth(2.5f);
        _gl.DepthFunc(DepthFunction.Lequal);

        _buffer.Upload(data);
        _buffer.Bind();
        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(tracers.Active.Count * 2));

        _gl.BindVertexArray(0);
        _gl.DepthFunc(DepthFunction.Less);
        _gl.Disable(EnableCap.Blend);
    }

    public void Dispose()
    {
        _shader.Dispose();
        _buffer.Dispose();
    }
}
