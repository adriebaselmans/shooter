using Silk.NET.OpenGL;
using System.Numerics;

namespace MapEditor.Rendering.Buffers;

/// <summary>
/// Uploads transient world-space line vertices for immediate-style editor overlays.
/// </summary>
public sealed class DynamicLineBuffer : IDisposable
{
    private readonly GL _gl;
    private readonly uint _vao;
    private readonly uint _vbo;
    private int _vertexCount;
    private bool _disposed;

    public DynamicLineBuffer(GL gl)
    {
        _gl = gl;
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        _gl.EnableVertexAttribArray(0);
        _gl.BindVertexArray(0);
    }

    public unsafe void SetVertices(ReadOnlySpan<Vector3> vertices)
    {
        _vertexCount = vertices.Length;

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        if (vertices.Length == 0)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, 0, null, BufferUsageARB.DynamicDraw);
            _gl.BindVertexArray(0);
            return;
        }

        fixed (Vector3* ptr = vertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(Vector3)),
                ptr,
                BufferUsageARB.DynamicDraw);
        }

        _gl.BindVertexArray(0);
    }

    public void Draw()
    {
        if (_vertexCount <= 0)
        {
            return;
        }

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_vertexCount);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _disposed = true;
    }
}
