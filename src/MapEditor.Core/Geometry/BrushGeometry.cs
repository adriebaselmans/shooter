using System.Collections.ObjectModel;
using System.Numerics;

namespace MapEditor.Core.Geometry;

public sealed class BrushFace
{
    public string Id { get; }
    public IReadOnlyList<Vector3> Vertices { get; }

    /// <summary>
    /// Runtime-only flag indicating this face originated from the cutter in a CSG subtract.
    /// Not serialized — used solely for display differentiation.
    /// </summary>
    public bool IsCutterFace { get; init; }

    public BrushFace(string id, IEnumerable<Vector3> vertices)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var faceVertices = vertices.ToArray();
        if (faceVertices.Length < 3)
        {
            throw new ArgumentException("A face requires at least three vertices.", nameof(vertices));
        }

        Id = id;
        Vertices = new ReadOnlyCollection<Vector3>(faceVertices);
    }

    public Vector3 GetNormal()
    {
        var origin = Vertices[0];
        for (int i = 1; i < Vertices.Count - 1; i++)
        {
            var edgeA = Vertices[i] - origin;
            var edgeB = Vertices[i + 1] - origin;
            var normal = Vector3.Cross(edgeA, edgeB);
            if (normal.LengthSquared() > 0.000001f)
            {
                return Vector3.Normalize(normal);
            }
        }

        return Vector3.UnitY;
    }

    public BrushFace Clone() => new(Id, Vertices) { IsCutterFace = IsCutterFace };
}

public sealed class BrushGeometry
{
    public static readonly BrushGeometry Empty = new([]);

    public IReadOnlyList<BrushFace> Faces { get; }
    public bool HasFaces => Faces.Count > 0;
    public int FaceCount => Faces.Count;

    public BrushGeometry(IEnumerable<BrushFace> faces)
    {
        var geometryFaces = faces.Select(face => face.Clone()).ToArray();
        Faces = new ReadOnlyCollection<BrushFace>(geometryFaces);
    }

    public BrushGeometry Clone() => new(Faces);

    public IReadOnlyList<string> GetFaceIds() => Faces.Select(face => face.Id).ToArray();

    public (Vector3 Min, Vector3 Max) GetBounds()
    {
        if (!HasFaces)
        {
            return (Vector3.Zero, Vector3.Zero);
        }

        var firstVertex = Faces[0].Vertices[0];
        var min = firstVertex;
        var max = firstVertex;

        foreach (var vertex in Faces.SelectMany(face => face.Vertices))
        {
            min = Vector3.Min(min, vertex);
            max = Vector3.Max(max, vertex);
        }

        return (min, max);
    }
}

public static class BrushGeometryTransforms
{
    public static BrushGeometry Transform(BrushGeometry geometry, Matrix4x4 matrix) =>
        new(geometry.Faces.Select(face => new BrushFace(
            face.Id,
            face.Vertices.Select(vertex => Vector3.Transform(vertex, matrix)))
        { IsCutterFace = face.IsCutterFace }));

    public static BrushGeometry Translate(BrushGeometry geometry, Vector3 offset) =>
        new(geometry.Faces.Select(face => new BrushFace(
            face.Id,
            face.Vertices.Select(vertex => vertex + offset))
        { IsCutterFace = face.IsCutterFace }));
}
