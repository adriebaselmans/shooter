namespace MapEditor.Core.Geometry;

/// <summary>Indexed range for a logical surface inside a mesh index buffer.</summary>
public readonly record struct MeshSurfaceRange(string SurfaceId, int IndexStart, int IndexCount, bool IsCutterFace = false);

/// <summary>
/// A renderable mesh with interleaved position, normal, and UV vertex data.
/// </summary>
public sealed class Mesh
{
    public const int FloatsPerVertex = 8;

    /// <summary>Interleaved position + normal + UV data: [x,y,z,nx,ny,nz,u,v, ...].</summary>
    public float[] Vertices { get; }

    /// <summary>Triangle indices into the vertex array (3 indices per triangle).</summary>
    public uint[] Indices { get; }

    /// <summary>Logical surface ranges inside the shared index buffer.</summary>
    public IReadOnlyList<MeshSurfaceRange> Surfaces { get; }

    public int VertexCount => Vertices.Length / FloatsPerVertex;
    public int TriangleCount => Indices.Length / 3;

    public Mesh(float[] vertices, uint[] indices, IReadOnlyList<MeshSurfaceRange>? surfaces = null)
    {
        Vertices = vertices;
        Indices = indices;
        Surfaces = surfaces ?? [];
    }
}
