using Silk.NET.OpenGL;
using System.Numerics;
using MapEditor.Core.Entities;

namespace MapEditor.Rendering.Buffers;

/// <summary>
/// Manages GPU buffers (VAO/VBO/EBO) for a single brush mesh.
/// Dirty flag prevents unnecessary GPU uploads.
/// </summary>
public sealed class BrushMeshBuffer : IDisposable
{
    private readonly GL _gl;
    private uint _vao, _vbo, _ebo, _lineEbo;
    private int  _indexCount;
    private int _lineIndexCount;
    private bool _disposed;

    public bool IsDirty { get; set; } = true;
    public Core.Geometry.Mesh? Mesh { get; private set; }

    public BrushMeshBuffer(GL gl)
    {
        _gl = gl;
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        _ebo = gl.GenBuffer();
        _lineEbo = gl.GenBuffer();
    }

    public void SetMesh(Core.Geometry.Mesh mesh)
    {
        Mesh = mesh;
        IsDirty = true;
    }

    public unsafe void UploadIfDirty()
    {
        if (!IsDirty || Mesh is null) return;

        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* ptr = Mesh.Vertices)
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(Mesh.Vertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* ptr = Mesh.Indices)
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(Mesh.Indices.Length * sizeof(uint)), ptr, BufferUsageARB.StaticDraw);

        var lineIndices = MeshEdgeIndexBuilder.BuildFeatureEdges(Mesh);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _lineEbo);
        fixed (uint* ptr = lineIndices)
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(lineIndices.Length * sizeof(uint)), ptr, BufferUsageARB.StaticDraw);

        // Position: location 0, vec3
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
        _gl.EnableVertexAttribArray(0);

        // Normal: location 1, vec3 (offset 3 floats)
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BindVertexArray(0);

        _indexCount = Mesh.Indices.Length;
        _lineIndexCount = lineIndices.Length;
        IsDirty = false;
    }

    public unsafe void DrawSolid()
    {
        UploadIfDirty();
        if (_indexCount == 0) return;
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.DrawElements(PrimitiveType.Triangles, (uint)_indexCount, DrawElementsType.UnsignedInt, (void*)0);
        _gl.BindVertexArray(0);
    }

    public void DrawWireframe()
    {
        DrawEdges();
    }

    public unsafe void DrawEdges()
    {
        UploadIfDirty();
        if (_lineIndexCount == 0) return;
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _lineEbo);
        _gl.DrawElements(PrimitiveType.Lines, (uint)_lineIndexCount, DrawElementsType.UnsignedInt, (void*)0);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteBuffer(_lineEbo);
        _disposed = true;
    }
}
