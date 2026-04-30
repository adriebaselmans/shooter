using System.Numerics;
using MapEditor.Core.Geometry;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>VAO/VBO/EBO wrapping a static <see cref="Mesh"/>.</summary>
public sealed class GlMesh : IDisposable
{
    private readonly GL _gl;
    public uint Vao { get; }
    public uint Vbo { get; }
    public uint Ebo { get; }
    public int IndexCount { get; }

    public unsafe GlMesh(GL gl, Mesh mesh)
    {
        _gl = gl;
        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        Ebo = gl.GenBuffer();
        gl.BindVertexArray(Vao);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        fixed (float* p = mesh.Vertices)
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(mesh.Vertices.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
        fixed (uint* p = mesh.Indices)
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(mesh.Indices.Length * sizeof(uint)), p, BufferUsageARB.StaticDraw);

        uint stride = (uint)(Mesh.FloatsPerVertex * sizeof(float));
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));

        gl.BindVertexArray(0);
        IndexCount = mesh.Indices.Length;
    }

    public void Bind() => _gl.BindVertexArray(Vao);

    public void Dispose()
    {
        _gl.DeleteBuffer(Vbo);
        _gl.DeleteBuffer(Ebo);
        _gl.DeleteVertexArray(Vao);
    }
}

/// <summary>Dynamic position-only buffer (used for decals / hud).</summary>
public sealed class DynamicPosBuffer : IDisposable
{
    private readonly GL _gl;
    public uint Vao { get; }
    public uint Vbo { get; }
    private int _capacityFloats;
    private readonly int _componentsPerVertex;

    public unsafe DynamicPosBuffer(GL gl, int componentsPerVertex)
    {
        _gl = gl;
        _componentsPerVertex = componentsPerVertex;
        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        gl.BindVertexArray(Vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, (int)componentsPerVertex, VertexAttribPointerType.Float, false,
            (uint)(componentsPerVertex * sizeof(float)), (void*)0);
        gl.BindVertexArray(0);
    }

    public unsafe void Upload(ReadOnlySpan<float> data)
    {
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        if (data.Length > _capacityFloats)
        {
            _capacityFloats = Math.Max(data.Length, _capacityFloats * 2);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_capacityFloats * sizeof(float)), null, BufferUsageARB.DynamicDraw);
        }
        fixed (float* p = data)
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(data.Length * sizeof(float)), p);
    }

    public int VertexCountFor(int floatLen) => floatLen / _componentsPerVertex;

    public void Bind() => _gl.BindVertexArray(Vao);

    public void Dispose()
    {
        _gl.DeleteBuffer(Vbo);
        _gl.DeleteVertexArray(Vao);
    }
}
