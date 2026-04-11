using MapEditor.Core.Entities;
using System.Numerics;

namespace MapEditor.Core.Geometry;

public interface IBrushBooleanKernel
{
    bool HasIntersection(Brush first, Brush second);
    BrushBooleanKernelResult? Subtract(Brush target, Brush cutter);
    BrushBooleanKernelResult? Merge(IReadOnlyList<Brush> brushes);
}

public sealed class BrushBooleanKernelResult
{
    public required Transform Transform { get; init; }
    public required BrushGeometry Geometry { get; init; }
    public required IReadOnlyDictionary<string, SurfaceMapping> SurfaceMappings { get; init; }
}

public sealed class BspBrushBooleanKernel : IBrushBooleanKernel
{
    private const float VolumeEpsilon = 0.0001f;

    public bool HasIntersection(Brush first, Brush second)
    {
        if (!BrushBounds.TryGetWorldBounds(first, out var firstMin, out var firstMax) ||
            !BrushBounds.TryGetWorldBounds(second, out var secondMin, out var secondMax) ||
            !BoundsOverlap(firstMin, firstMax, secondMin, secondMax))
        {
            return false;
        }

        var firstSolid = BuildSolid(first, OperationSource.Target);
        var secondSolid = BuildSolid(second, OperationSource.Cutter);
        var intersection = firstSolid.Intersect(secondSolid);
        return ComputeVolume(intersection.Polygons) > VolumeEpsilon;
    }

    public BrushBooleanKernelResult? Subtract(Brush target, Brush cutter)
    {
        var targetSolid = BuildSolid(target, OperationSource.Target);
        var cutterSolid = BuildSolid(cutter, OperationSource.Cutter);
        var result = targetSolid.Subtract(cutterSolid);
        return BuildResult(result.Polygons, target.MaterialName, replaceCutterFacesWithBaseline: true);
    }

    public BrushBooleanKernelResult? Merge(IReadOnlyList<Brush> brushes)
    {
        ArgumentNullException.ThrowIfNull(brushes);
        if (brushes.Count < 2)
        {
            return null;
        }

        var aggregate = BuildSolid(brushes[0], OperationSource.Target);
        for (int i = 1; i < brushes.Count; i++)
        {
            aggregate = aggregate.Union(BuildSolid(brushes[i], OperationSource.Target));
        }

        var result = BuildResult(aggregate.Polygons, brushes[0].MaterialName, replaceCutterFacesWithBaseline: false);
        if (result is null || BrushGeometryTopology.CountConnectedShells(result.Geometry) != 1)
        {
            return null;
        }

        return result;
    }

    private static CsgSolid BuildSolid(Brush brush, OperationSource source)
    {
        var model = TransformMath.BuildModelMatrix(brush.Transform);
        var polygons = new List<CsgPolygon>();
        foreach (var face in BrushGeometryFactory.GetGeometry(brush).Faces)
        {
            var vertices = face.Vertices
                .Select(vertex => new CsgVertex(Vector3.Transform(vertex, model)))
                .ToArray();

            polygons.Add(new CsgPolygon(
                vertices,
                new PolygonAppearance(
                    brush.MaterialName,
                    brush.GetEffectiveSurfaceMapping(face.Id),
                    source)));
        }

        EnsureOutwardNormals(polygons);
        return new CsgSolid(polygons);
    }

    /// <summary>
    /// Ensures all polygon normals point outward (away from the solid's centroid).
    /// The BSP CSG algorithm requires consistent outward-facing normals to correctly
    /// partition space into inside/outside regions. Some primitive generators produce
    /// faces with inconsistent winding, which creates degenerate BSP trees that fail
    /// to clip geometry extending beyond the solid's bounds.
    /// This normalization assumes convex primitive input geometry built by
    /// <see cref="BuildSolid"/>; it is not suitable for arbitrary non-convex geometry
    /// where the centroid-based outward test may flip faces incorrectly.
    /// </summary>
    private static void EnsureOutwardNormals(List<CsgPolygon> polygons)
    {
        if (polygons.Count == 0) return;

        var centroid = Vector3.Zero;
        int vertexCount = 0;
        foreach (var polygon in polygons)
        {
            foreach (var vertex in polygon.Vertices)
            {
                centroid += vertex.Position;
                vertexCount++;
            }
        }
        if (vertexCount == 0) return;
        centroid /= vertexCount;

        foreach (var polygon in polygons)
        {
            if (polygon.Vertices.Count == 0) continue;

            var faceCenter = Vector3.Zero;
            foreach (var vertex in polygon.Vertices)
            {
                faceCenter += vertex.Position;
            }
            faceCenter /= polygon.Vertices.Count;

            var outward = faceCenter - centroid;
            if (outward.LengthSquared() > float.Epsilon &&
                Vector3.Dot(polygon.Plane.Normal, outward) < 0f)
            {
                polygon.Flip();
            }
        }
    }

    private static BrushBooleanKernelResult? BuildResult(
        IReadOnlyList<CsgPolygon> polygons,
        string baselineMaterialName,
        bool replaceCutterFacesWithBaseline)
    {
        if (polygons.Count == 0 || ComputeVolume(polygons) <= VolumeEpsilon)
        {
            return null;
        }

        var firstVertex = polygons[0].Vertices[0].Position;
        var min = firstVertex;
        var max = firstVertex;
        foreach (var vertex in polygons.SelectMany(polygon => polygon.Vertices.Select(item => item.Position)))
        {
            min = Vector3.Min(min, vertex);
            max = Vector3.Max(max, vertex);
        }

        var center = (min + max) * 0.5f;
        var faces = new List<BrushFace>(polygons.Count);
        var mappings = new Dictionary<string, SurfaceMapping>(polygons.Count, StringComparer.Ordinal);
        for (int i = 0; i < polygons.Count; i++)
        {
            var polygon = polygons[i];
            string faceId = $"face-{i + 1}";
            var vertices = polygon.Vertices
                .Select(vertex => vertex.Position - center)
                .ToArray();
            bool isCutterFace = polygon.Appearance.Source == OperationSource.Cutter;
            faces.Add(new BrushFace(faceId, vertices) { IsCutterFace = isCutterFace });
            mappings[faceId] = replaceCutterFacesWithBaseline && isCutterFace
                ? SurfaceMapping.Default(baselineMaterialName)
                : polygon.Appearance.Mapping;
        }

        return new BrushBooleanKernelResult
        {
            Transform = new Transform
            {
                Position = center,
                EulerDegrees = Vector3.Zero,
                Scale = Vector3.One
            },
            Geometry = new BrushGeometry(faces),
            SurfaceMappings = mappings
        };
    }

    private static bool BoundsOverlap(Vector3 firstMin, Vector3 firstMax, Vector3 secondMin, Vector3 secondMax)
    {
        return firstMax.X > secondMin.X &&
               firstMin.X < secondMax.X &&
               firstMax.Y > secondMin.Y &&
               firstMin.Y < secondMax.Y &&
               firstMax.Z > secondMin.Z &&
               firstMin.Z < secondMax.Z;
    }

    internal static float ComputeVolume(IReadOnlyList<CsgPolygon> polygons)
    {
        double volume = 0d;
        foreach (var polygon in polygons)
        {
            var origin = polygon.Vertices[0].Position;
            for (int i = 1; i < polygon.Vertices.Count - 1; i++)
            {
                var a = polygon.Vertices[i].Position;
                var b = polygon.Vertices[i + 1].Position;
                volume += Vector3.Dot(origin, Vector3.Cross(a, b)) / 6d;
            }
        }

        return (float)Math.Abs(volume);
    }

    internal enum OperationSource
    {
        Target,
        Cutter
    }

    internal readonly record struct PolygonAppearance(string MaterialName, SurfaceMapping Mapping, OperationSource Source);

    internal sealed class CsgSolid
    {
        public IReadOnlyList<CsgPolygon> Polygons { get; }

        public CsgSolid(IEnumerable<CsgPolygon> polygons)
        {
            Polygons = polygons.Select(polygon => polygon.Clone()).ToArray();
        }

        public CsgSolid Union(CsgSolid other)
        {
            var left = new CsgNode(Polygons);
            var right = new CsgNode(other.Polygons);
            left.ClipTo(right);
            right.ClipTo(left);
            right.Invert();
            right.ClipTo(left);
            right.Invert();
            left.Build(right.AllPolygons());
            return new CsgSolid(left.AllPolygons());
        }

        public CsgSolid Subtract(CsgSolid other)
        {
            var left = new CsgNode(Polygons);
            var right = new CsgNode(other.Polygons);
            left.Invert();
            left.ClipTo(right);
            right.ClipTo(left);
            right.Invert();
            right.ClipTo(left);
            right.Invert();
            left.Build(right.AllPolygons());
            left.Invert();
            return new CsgSolid(left.AllPolygons());
        }

        public CsgSolid Intersect(CsgSolid other)
        {
            var left = new CsgNode(Polygons);
            var right = new CsgNode(other.Polygons);
            left.Invert();
            right.ClipTo(left);
            right.Invert();
            left.ClipTo(right);
            right.ClipTo(left);
            left.Build(right.AllPolygons());
            left.Invert();
            return new CsgSolid(left.AllPolygons());
        }
    }

    internal sealed class CsgNode
    {
        private CsgPlane? _plane;
        private CsgNode? _front;
        private CsgNode? _back;
        private readonly List<CsgPolygon> _polygons = [];

        public CsgNode()
        {
        }

        public CsgNode(IEnumerable<CsgPolygon> polygons)
        {
            Build(polygons);
        }

        public CsgNode Clone()
        {
            var clone = new CsgNode
            {
                _plane = _plane?.Clone(),
                _front = _front?.Clone(),
                _back = _back?.Clone()
            };

            clone._polygons.AddRange(_polygons.Select(polygon => polygon.Clone()));
            return clone;
        }

        public void Invert()
        {
            foreach (var polygon in _polygons)
            {
                polygon.Flip();
            }

            _plane?.Flip();
            _front?.Invert();
            _back?.Invert();
            (_front, _back) = (_back, _front);
        }

        public IReadOnlyList<CsgPolygon> ClipPolygons(IReadOnlyList<CsgPolygon> polygons)
        {
            if (_plane is null)
            {
                return polygons.Select(polygon => polygon.Clone()).ToArray();
            }

            List<CsgPolygon> front = [];
            List<CsgPolygon> back = [];
            foreach (var polygon in polygons)
            {
                _plane.SplitPolygon(polygon, front, back, front, back);
            }

            if (_front is not null)
            {
                front = [.. _front.ClipPolygons(front)];
            }

            if (_back is not null)
            {
                back = [.. _back.ClipPolygons(back)];
            }
            else
            {
                back.Clear();
            }

            front.AddRange(back);
            return front;
        }

        public void ClipTo(CsgNode other)
        {
            var clipped = other.ClipPolygons(_polygons);
            _polygons.Clear();
            _polygons.AddRange(clipped);
            _front?.ClipTo(other);
            _back?.ClipTo(other);
        }

        public IReadOnlyList<CsgPolygon> AllPolygons()
        {
            var polygons = new List<CsgPolygon>(_polygons.Select(polygon => polygon.Clone()));
            if (_front is not null)
            {
                polygons.AddRange(_front.AllPolygons());
            }

            if (_back is not null)
            {
                polygons.AddRange(_back.AllPolygons());
            }

            return polygons;
        }

        public void Build(IEnumerable<CsgPolygon> polygons)
        {
            var polygonList = polygons.Select(polygon => polygon.Clone()).ToList();
            if (polygonList.Count == 0)
            {
                return;
            }

            _plane ??= CsgPlane.FromPolygon(polygonList[0]);

            List<CsgPolygon> front = [];
            List<CsgPolygon> back = [];
            foreach (var polygon in polygonList)
            {
                _plane.SplitPolygon(polygon, _polygons, _polygons, front, back);
            }

            if (front.Count > 0)
            {
                _front ??= new CsgNode();
                _front.Build(front);
            }

            if (back.Count > 0)
            {
                _back ??= new CsgNode();
                _back.Build(back);
            }
        }
    }

    internal sealed class CsgPolygon
    {
        public List<CsgVertex> Vertices { get; }
        public PolygonAppearance Appearance { get; }
        public CsgPlane Plane { get; }

        public CsgPolygon(IEnumerable<CsgVertex> vertices, PolygonAppearance appearance)
        {
            Vertices = vertices.Select(vertex => vertex.Clone()).ToList();
            Appearance = appearance;
            Plane = CsgPlane.FromVertices(Vertices);
        }

        public CsgPolygon Clone() => new(Vertices, Appearance);

        public void Flip()
        {
            Vertices.Reverse();
            foreach (var vertex in Vertices)
            {
                vertex.Flip();
            }

            Plane.Flip();
        }
    }

    internal sealed class CsgPlane
    {
        private const float Epsilon = 0.0001f;
        private const int Coplanar = 0;
        private const int Front = 1;
        private const int Back = 2;
        private const int Spanning = 3;

        public Vector3 Normal { get; private set; }
        public float W { get; private set; }

        private CsgPlane(Vector3 normal, float w)
        {
            Normal = normal;
            W = w;
        }

        public static CsgPlane FromPolygon(CsgPolygon polygon) => FromVertices(polygon.Vertices);

        public static CsgPlane FromVertices(IReadOnlyList<CsgVertex> vertices)
        {
            for (int i = 1; i < vertices.Count - 1; i++)
            {
                var normal = Vector3.Cross(
                    vertices[i].Position - vertices[0].Position,
                    vertices[i + 1].Position - vertices[0].Position);

                if (normal.LengthSquared() > Epsilon * Epsilon)
                {
                    normal = Vector3.Normalize(normal);
                    return new CsgPlane(normal, Vector3.Dot(normal, vertices[0].Position));
                }
            }

            return new CsgPlane(Vector3.UnitY, 0f);
        }

        public CsgPlane Clone() => new(Normal, W);

        public void Flip()
        {
            Normal = -Normal;
            W = -W;
        }

        public void SplitPolygon(
            CsgPolygon polygon,
            ICollection<CsgPolygon> coplanarFront,
            ICollection<CsgPolygon> coplanarBack,
            ICollection<CsgPolygon> front,
            ICollection<CsgPolygon> back)
        {
            int polygonType = 0;
            var types = new int[polygon.Vertices.Count];
            for (int i = 0; i < polygon.Vertices.Count; i++)
            {
                float distance = Vector3.Dot(Normal, polygon.Vertices[i].Position) - W;
                int type = distance < -Epsilon ? Back : distance > Epsilon ? Front : Coplanar;
                polygonType |= type;
                types[i] = type;
            }

            switch (polygonType)
            {
                case Coplanar:
                    if (Vector3.Dot(Normal, polygon.Plane.Normal) > 0f)
                    {
                        coplanarFront.Add(polygon.Clone());
                    }
                    else
                    {
                        coplanarBack.Add(polygon.Clone());
                    }
                    break;
                case Front:
                    front.Add(polygon.Clone());
                    break;
                case Back:
                    back.Add(polygon.Clone());
                    break;
                case Spanning:
                    List<CsgVertex> frontVertices = [];
                    List<CsgVertex> backVertices = [];
                    for (int i = 0; i < polygon.Vertices.Count; i++)
                    {
                        int next = (i + 1) % polygon.Vertices.Count;
                        int type = types[i];
                        int nextType = types[next];
                        var current = polygon.Vertices[i];
                        var following = polygon.Vertices[next];

                        if (type != Back)
                        {
                            frontVertices.Add(current.Clone());
                        }

                        if (type != Front)
                        {
                            backVertices.Add(current.Clone());
                        }

                        if ((type | nextType) != Spanning)
                        {
                            continue;
                        }

                        var edge = following.Position - current.Position;
                        float t = (W - Vector3.Dot(Normal, current.Position)) / Vector3.Dot(Normal, edge);
                        var splitVertex = current.Interpolate(following, t);
                        frontVertices.Add(splitVertex.Clone());
                        backVertices.Add(splitVertex);
                    }

                    if (frontVertices.Count >= 3)
                    {
                        front.Add(new CsgPolygon(frontVertices, polygon.Appearance));
                    }

                    if (backVertices.Count >= 3)
                    {
                        back.Add(new CsgPolygon(backVertices, polygon.Appearance));
                    }
                    break;
            }
        }
    }

    internal sealed class CsgVertex
    {
        public Vector3 Position { get; private set; }

        public CsgVertex(Vector3 position)
        {
            Position = position;
        }

        public CsgVertex Clone() => new(Position);

        public void Flip()
        {
        }

        public CsgVertex Interpolate(CsgVertex other, float t) =>
            new(Vector3.Lerp(Position, other.Position, t));
    }
}

public static class BrushGeometryTopology
{
    private const float QuantizeScale = 1000f;

    public static int CountConnectedShells(BrushGeometry geometry)
    {
        if (!geometry.HasFaces)
        {
            return 0;
        }

        var edgeOwners = new Dictionary<EdgeKey, List<int>>();
        for (int faceIndex = 0; faceIndex < geometry.Faces.Count; faceIndex++)
        {
            var vertices = geometry.Faces[faceIndex].Vertices;
            for (int i = 0; i < vertices.Count; i++)
            {
                var edge = new EdgeKey(vertices[i], vertices[(i + 1) % vertices.Count]);
                if (!edgeOwners.TryGetValue(edge, out var owners))
                {
                    owners = [];
                    edgeOwners[edge] = owners;
                }

                owners.Add(faceIndex);
            }
        }

        var adjacency = Enumerable.Range(0, geometry.Faces.Count)
            .ToDictionary(index => index, _ => new HashSet<int>());

        foreach (var owners in edgeOwners.Values.Where(owners => owners.Count > 1))
        {
            foreach (var owner in owners)
            {
                foreach (var other in owners)
                {
                    if (owner != other)
                    {
                        adjacency[owner].Add(other);
                    }
                }
            }
        }

        int components = 0;
        var visited = new HashSet<int>();
        for (int faceIndex = 0; faceIndex < geometry.Faces.Count; faceIndex++)
        {
            if (!visited.Add(faceIndex))
            {
                continue;
            }

            components++;
            var pending = new Queue<int>();
            pending.Enqueue(faceIndex);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var neighbour in adjacency[current])
                {
                    if (visited.Add(neighbour))
                    {
                        pending.Enqueue(neighbour);
                    }
                }
            }
        }

        return components;
    }

    private readonly record struct EdgeKey(VertexKey A, VertexKey B)
    {
        public EdgeKey(Vector3 first, Vector3 second)
            : this(
                VertexKey.From(first).CompareTo(VertexKey.From(second)) <= 0 ? VertexKey.From(first) : VertexKey.From(second),
                VertexKey.From(first).CompareTo(VertexKey.From(second)) <= 0 ? VertexKey.From(second) : VertexKey.From(first))
        {
        }
    }

    private readonly record struct VertexKey(int X, int Y, int Z) : IComparable<VertexKey>
    {
        public static VertexKey From(Vector3 value) => new(
            (int)MathF.Round(value.X * QuantizeScale),
            (int)MathF.Round(value.Y * QuantizeScale),
            (int)MathF.Round(value.Z * QuantizeScale));

        public int CompareTo(VertexKey other)
        {
            int x = X.CompareTo(other.X);
            if (x != 0)
            {
                return x;
            }

            int y = Y.CompareTo(other.Y);
            return y != 0 ? y : Z.CompareTo(other.Z);
        }
    }
}
