namespace MapEditor.Core.Geometry;

/// <summary>
/// A renderable mesh: interleaved float vertex data (X,Y,Z, NX,NY,NZ per vertex)
/// and an index list for triangle faces.
/// </summary>
public sealed class Mesh
{
    /// <summary>Interleaved position + normal data: [x,y,z,nx,ny,nz, ...].</summary>
    public float[] Vertices { get; }

    /// <summary>Triangle indices into the vertex array (3 indices per triangle).</summary>
    public uint[] Indices { get; }

    public int VertexCount => Vertices.Length / 6;
    public int TriangleCount => Indices.Length / 3;

    public Mesh(float[] vertices, uint[] indices)
    {
        Vertices = vertices;
        Indices = indices;
    }
}
