using Silk.NET.OpenGL;
using System.Numerics;
using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;

namespace MapEditor.Rendering.Buffers;

/// <summary>
/// Manages GPU buffers (VAO/VBO/EBO) for a single brush mesh.
/// Dirty flag prevents unnecessary GPU uploads.
/// </summary>
public sealed class BrushMeshBuffer : IDisposable
{
    private readonly GL _gl;
    private uint _vao, _vbo, _ebo, _lineEbo, _cutterLineEbo;
    private int  _indexCount;
    private int _lineIndexCount;
    private int _cutterLineIndexCount;
    private bool _disposed;

    public bool IsDirty { get; set; } = true;
    public Core.Geometry.Mesh? Mesh { get; private set; }
    public string? MeshSignature { get; set; }

    public BrushMeshBuffer(GL gl)
    {
        _gl = gl;
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();
        _ebo = gl.GenBuffer();
        _lineEbo = gl.GenBuffer();
        _cutterLineEbo = gl.GenBuffer();
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

        var splitEdges = MeshEdgeIndexBuilder.BuildSplitFeatureEdges(Mesh);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _lineEbo);
        fixed (uint* ptr = splitEdges.TargetEdges)
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(splitEdges.TargetEdges.Length * sizeof(uint)), ptr, BufferUsageARB.StaticDraw);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _cutterLineEbo);
        fixed (uint* ptr = splitEdges.CutterEdges)
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(splitEdges.CutterEdges.Length * sizeof(uint)), ptr, BufferUsageARB.StaticDraw);

        int stride = Core.Geometry.Mesh.FloatsPerVertex * sizeof(float);

        // Position: location 0, vec3
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)stride, 0);
        _gl.EnableVertexAttribArray(0);

        // Normal: location 1, vec3 (offset 3 floats)
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, (uint)stride, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        // UV: location 2, vec2 (offset 6 floats)
        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, (uint)stride, (void*)(6 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BindVertexArray(0);

        _indexCount = Mesh.Indices.Length;
        _lineIndexCount = splitEdges.TargetEdges.Length;
        _cutterLineIndexCount = splitEdges.CutterEdges.Length;
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

    public unsafe void DrawSurface(MeshSurfaceRange surface)
    {
        UploadIfDirty();
        if (surface.IndexCount <= 0)
        {
            return;
        }

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.DrawElements(
            PrimitiveType.Triangles,
            (uint)surface.IndexCount,
            DrawElementsType.UnsignedInt,
            (void*)(surface.IndexStart * sizeof(uint)));
        _gl.BindVertexArray(0);
    }

    public void DrawWireframe()
    {
        DrawTargetEdges();
        DrawCutterEdges();
    }

    public unsafe void DrawTargetEdges()
    {
        UploadIfDirty();
        if (_lineIndexCount == 0) return;
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _lineEbo);
        _gl.DrawElements(PrimitiveType.Lines, (uint)_lineIndexCount, DrawElementsType.UnsignedInt, (void*)0);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BindVertexArray(0);
    }

    public unsafe void DrawCutterEdges()
    {
        UploadIfDirty();
        if (_cutterLineIndexCount == 0) return;
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _cutterLineEbo);
        _gl.DrawElements(PrimitiveType.Lines, (uint)_cutterLineIndexCount, DrawElementsType.UnsignedInt, (void*)0);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BindVertexArray(0);
    }

    public bool HasCutterEdges => _cutterLineIndexCount > 0;

    public void Dispose()
    {
        if (_disposed) return;
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteBuffer(_lineEbo);
        _gl.DeleteBuffer(_cutterLineEbo);
        _disposed = true;
    }
}
