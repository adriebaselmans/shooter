using System.Numerics;
using MapEditor.Core.Geometry;

namespace MapEditor.Rendering.Buffers;

/// <summary>
/// Builds feature-edge line indices from triangle mesh data by removing
/// coplanar shared edges while preserving hard edges and boundaries.
/// </summary>
public static class MeshEdgeIndexBuilder
{
    public static uint[] BuildFeatureEdges(Mesh mesh, float coplanarDotThreshold = 0.999f)
    {
        if (mesh.Indices.Length < 3 || mesh.Vertices.Length < 6)
        {
            return [];
        }

        var edgeMap = new Dictionary<EdgeKey, EdgeRecord>();
        for (int i = 0; i <= mesh.Indices.Length - 3; i += 3)
        {
            uint i0 = mesh.Indices[i];
            uint i1 = mesh.Indices[i + 1];
            uint i2 = mesh.Indices[i + 2];

            var p0 = GetPosition(mesh, i0);
            var p1 = GetPosition(mesh, i1);
            var p2 = GetPosition(mesh, i2);
            var normal = ComputeTriangleNormal(p0, p1, p2);

            AddEdge(edgeMap, i0, i1, p0, p1, normal, coplanarDotThreshold);
            AddEdge(edgeMap, i1, i2, p1, p2, normal, coplanarDotThreshold);
            AddEdge(edgeMap, i2, i0, p2, p0, normal, coplanarDotThreshold);
        }

        var lineIndices = new List<uint>(edgeMap.Count * 2);
        foreach (var edge in edgeMap.Values)
        {
            if (edge.OccurrenceCount == 1 || edge.HasHardEdge)
            {
                lineIndices.Add(edge.StartIndex);
                lineIndices.Add(edge.EndIndex);
            }
        }

        return [.. lineIndices];
    }

    private static void AddEdge(
        Dictionary<EdgeKey, EdgeRecord> edgeMap,
        uint startIndex,
        uint endIndex,
        Vector3 start,
        Vector3 end,
        Vector3 triangleNormal,
        float coplanarDotThreshold)
    {
        var key = EdgeKey.Create(start, end);
        if (!edgeMap.TryGetValue(key, out var edge))
        {
            edgeMap[key] = new EdgeRecord(startIndex, endIndex, triangleNormal);
            return;
        }

        edge.OccurrenceCount++;
        if (!edge.HasHardEdge && triangleNormal != Vector3.Zero)
        {
            float dot = Vector3.Dot(edge.FirstNormal, triangleNormal);
            if (dot < coplanarDotThreshold)
            {
                edge.HasHardEdge = true;
            }
        }

        edgeMap[key] = edge;
    }

    private static Vector3 GetPosition(Mesh mesh, uint index)
    {
        int offset = checked((int)index) * 6;
        return new Vector3(
            mesh.Vertices[offset],
            mesh.Vertices[offset + 1],
            mesh.Vertices[offset + 2]);
    }

    private static Vector3 ComputeTriangleNormal(Vector3 p0, Vector3 p1, Vector3 p2)
    {
        var cross = Vector3.Cross(p1 - p0, p2 - p0);
        float lengthSquared = cross.LengthSquared();
        return lengthSquared <= float.Epsilon ? Vector3.Zero : cross / MathF.Sqrt(lengthSquared);
    }

    private readonly record struct QuantizedPoint(int X, int Y, int Z)
    {
        public static QuantizedPoint Create(Vector3 point) =>
            new(
                BitConverter.SingleToInt32Bits(point.X),
                BitConverter.SingleToInt32Bits(point.Y),
                BitConverter.SingleToInt32Bits(point.Z));
    }

    private readonly record struct EdgeKey(QuantizedPoint A, QuantizedPoint B)
    {
        public static EdgeKey Create(Vector3 a, Vector3 b)
        {
            var qa = QuantizedPoint.Create(a);
            var qb = QuantizedPoint.Create(b);
            return Compare(qa, qb) <= 0 ? new EdgeKey(qa, qb) : new EdgeKey(qb, qa);
        }

        private static int Compare(QuantizedPoint left, QuantizedPoint right)
        {
            int cmp = left.X.CompareTo(right.X);
            if (cmp != 0) return cmp;
            cmp = left.Y.CompareTo(right.Y);
            return cmp != 0 ? cmp : left.Z.CompareTo(right.Z);
        }
    }

    private struct EdgeRecord
    {
        public EdgeRecord(uint startIndex, uint endIndex, Vector3 firstNormal)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
            FirstNormal = firstNormal;
            OccurrenceCount = 1;
            HasHardEdge = false;
        }

        public uint StartIndex { get; }
        public uint EndIndex { get; }
        public Vector3 FirstNormal { get; }
        public int OccurrenceCount { get; set; }
        public bool HasHardEdge { get; set; }
    }
}
