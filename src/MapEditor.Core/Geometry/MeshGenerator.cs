using MapEditor.Core.Entities;
using System.Numerics;

namespace MapEditor.Core.Geometry;

/// <summary>
/// Generates display meshes for BSP brush primitives.
/// All meshes use interleaved vertex format: [x,y,z, nx,ny,nz, u,v] per vertex.
/// Generated geometry is in local space (centred at origin); the caller applies the transform.
/// </summary>
public static class MeshGenerator
{
    private const int CylinderSegments = 16;
    private const int ConeSegments = 16;
    private const float TextureWorldSize = 64f;

    /// <summary>Generates a unit mesh for the given primitive in local space (unit size: 1x1x1).</summary>
    public static Mesh GenerateMesh(BrushPrimitive primitive) => GenerateMesh(new Brush { Primitive = primitive });

    /// <summary>Generates a brush mesh using its primitive and current surface mappings.</summary>
    public static Mesh GenerateMesh(Brush brush)
    {
        if (brush.HasExplicitGeometry)
        {
            return GenerateExplicitGeometry(brush);
        }

        return brush.Primitive switch
        {
            BrushPrimitive.Box => GenerateBox(brush),
            BrushPrimitive.Cylinder => GenerateCylinder(brush),
            BrushPrimitive.Cone => GenerateCone(brush),
            BrushPrimitive.Wedge => GenerateWedge(brush),
            _ => throw new ArgumentOutOfRangeException(nameof(brush))
        };
    }

    private static Mesh GenerateExplicitGeometry(Brush brush)
    {
        var builder = new MeshBuilder();
        var geometry = BrushGeometryFactory.GetGeometry(brush);
        foreach (var face in geometry.Faces)
        {
            AddExplicitFace(builder, brush, face);
        }

        return builder.ToMesh();
    }

    private static Mesh GenerateBox(Brush brush)
    {
        var builder = new MeshBuilder();
        var size = brush.Transform.Scale;

        AddQuad(
            builder,
            BrushSurfaceIds.Top,
            brush,
            Vector3.UnitY,
            Vector3.UnitX,
            Vector3.UnitZ,
            size.X,
            size.Z,
            [new(-0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f)]);

        AddQuad(
            builder,
            BrushSurfaceIds.Bottom,
            brush,
            -Vector3.UnitY,
            Vector3.UnitX,
            -Vector3.UnitZ,
            size.X,
            size.Z,
            [new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f)]);

        AddQuad(
            builder,
            BrushSurfaceIds.Front,
            brush,
            Vector3.UnitZ,
            Vector3.UnitX,
            Vector3.UnitY,
            size.X,
            size.Y,
            [new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f)]);

        AddQuad(
            builder,
            BrushSurfaceIds.Back,
            brush,
            -Vector3.UnitZ,
            -Vector3.UnitX,
            Vector3.UnitY,
            size.X,
            size.Y,
            [new(0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, -0.5f)]);

        AddQuad(
            builder,
            BrushSurfaceIds.Right,
            brush,
            Vector3.UnitX,
            -Vector3.UnitZ,
            Vector3.UnitY,
            size.Z,
            size.Y,
            [new(0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, 0.5f)]);

        AddQuad(
            builder,
            BrushSurfaceIds.Left,
            brush,
            -Vector3.UnitX,
            Vector3.UnitZ,
            Vector3.UnitY,
            size.Z,
            size.Y,
            [new(-0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, -0.5f)]);

        return builder.ToMesh();
    }

    private static Mesh GenerateCylinder(Brush brush)
    {
        var builder = new MeshBuilder();
        var size = brush.Transform.Scale;
        float radiusX = size.X * 0.5f;
        float radiusZ = size.Z * 0.5f;
        float height = size.Y;
        float circumference = MathF.PI * (3f * (radiusX + radiusZ) - MathF.Sqrt((3f * radiusX + radiusZ) * (radiusX + 3f * radiusZ)));

        builder.AddSurface(BrushSurfaceIds.Side, () =>
        {
            for (int i = 0; i < CylinderSegments; i++)
            {
                float a0 = MathF.PI * 2 * i / CylinderSegments;
                float a1 = MathF.PI * 2 * (i + 1) / CylinderSegments;
                var p00 = new Vector3(MathF.Cos(a0) * 0.5f, -0.5f, MathF.Sin(a0) * 0.5f);
                var p10 = new Vector3(MathF.Cos(a1) * 0.5f, -0.5f, MathF.Sin(a1) * 0.5f);
                var p01 = new Vector3(MathF.Cos(a0) * 0.5f, 0.5f, MathF.Sin(a0) * 0.5f);
                var p11 = new Vector3(MathF.Cos(a1) * 0.5f, 0.5f, MathF.Sin(a1) * 0.5f);
                var n0 = Vector3.Normalize(new Vector3(MathF.Cos(a0), 0f, MathF.Sin(a0)));
                var n1 = Vector3.Normalize(new Vector3(MathF.Cos(a1), 0f, MathF.Sin(a1)));

                var mapping = brush.GetEffectiveSurfaceMapping(BrushSurfaceIds.Side);
                var uv00 = ApplyMapping(new Vector2(GetAngularU(a0, mapping.TextureLocked, circumference), GetVerticalV(p00.Y, mapping.TextureLocked, height)), mapping);
                var uv10 = ApplyMapping(new Vector2(GetAngularU(a1, mapping.TextureLocked, circumference), GetVerticalV(p10.Y, mapping.TextureLocked, height)), mapping);
                var uv01 = ApplyMapping(new Vector2(GetAngularU(a0, mapping.TextureLocked, circumference), GetVerticalV(p01.Y, mapping.TextureLocked, height)), mapping);
                var uv11 = ApplyMapping(new Vector2(GetAngularU(a1, mapping.TextureLocked, circumference), GetVerticalV(p11.Y, mapping.TextureLocked, height)), mapping);

                builder.AddQuadVertices(p00, n0, uv00, p10, n1, uv10, p11, n1, uv11, p01, n0, uv01);
            }
        });

        AddCircleCap(builder, brush, BrushSurfaceIds.Top, 0.5f, Vector3.UnitY, radiusX, radiusZ, flipWinding: false);
        AddCircleCap(builder, brush, BrushSurfaceIds.Bottom, -0.5f, -Vector3.UnitY, radiusX, radiusZ, flipWinding: true);
        return builder.ToMesh();
    }

    private static Mesh GenerateCone(Brush brush)
    {
        var builder = new MeshBuilder();
        var size = brush.Transform.Scale;
        float radiusX = size.X * 0.5f;
        float radiusZ = size.Z * 0.5f;
        float height = size.Y;
        float circumference = MathF.PI * (radiusX + radiusZ);
        float slantHeight = MathF.Sqrt(MathF.Max(1f, radiusX * radiusX + radiusZ * radiusZ) + height * height);
        var apex = new Vector3(0f, 0.5f, 0f);

        builder.AddSurface(BrushSurfaceIds.Side, () =>
        {
            var mapping = brush.GetEffectiveSurfaceMapping(BrushSurfaceIds.Side);
            for (int i = 0; i < ConeSegments; i++)
            {
                float a0 = MathF.PI * 2 * i / ConeSegments;
                float a1 = MathF.PI * 2 * (i + 1) / ConeSegments;
                var b0 = new Vector3(MathF.Cos(a0) * 0.5f, -0.5f, MathF.Sin(a0) * 0.5f);
                var b1 = new Vector3(MathF.Cos(a1) * 0.5f, -0.5f, MathF.Sin(a1) * 0.5f);
                var faceNormal = Vector3.Normalize(Vector3.Cross(b1 - apex, b0 - apex));

                var uvApex = ApplyMapping(new Vector2((GetAngularU(a0, mapping.TextureLocked, circumference) + GetAngularU(a1, mapping.TextureLocked, circumference)) * 0.5f, 0f), mapping);
                var uv0 = ApplyMapping(new Vector2(GetAngularU(a0, mapping.TextureLocked, circumference), mapping.TextureLocked ? slantHeight / TextureWorldSize : 1f), mapping);
                var uv1 = ApplyMapping(new Vector2(GetAngularU(a1, mapping.TextureLocked, circumference), mapping.TextureLocked ? slantHeight / TextureWorldSize : 1f), mapping);

                builder.AddTriangleVertices(apex, faceNormal, uvApex, b0, faceNormal, uv0, b1, faceNormal, uv1);
            }
        });

        AddCircleCap(builder, brush, BrushSurfaceIds.Base, -0.5f, -Vector3.UnitY, radiusX, radiusZ, flipWinding: true);
        return builder.ToMesh();
    }

    private static Mesh GenerateWedge(Brush brush)
    {
        var builder = new MeshBuilder();
        var size = brush.Transform.Scale;

        Vector3[] front =
        [
            new(-0.5f, -0.5f, 0.5f),
            new(0.5f, -0.5f, 0.5f),
            new(0.0f, 0.5f, 0.5f)
        ];
        Vector3[] back =
        [
            new(-0.5f, -0.5f, -0.5f),
            new(0.5f, -0.5f, -0.5f),
            new(0.0f, 0.5f, -0.5f)
        ];

        AddTriangle(
            builder,
            BrushSurfaceIds.Front,
            brush,
            Vector3.UnitZ,
            Vector3.UnitX,
            Vector3.UnitY,
            size.X,
            size.Y,
            front[0], front[1], front[2]);

        AddTriangle(
            builder,
            BrushSurfaceIds.Back,
            brush,
            -Vector3.UnitZ,
            -Vector3.UnitX,
            Vector3.UnitY,
            size.X,
            size.Y,
            back[1], back[0], back[2]);

        AddQuad(
            builder,
            BrushSurfaceIds.Bottom,
            brush,
            -Vector3.UnitY,
            Vector3.UnitX,
            -Vector3.UnitZ,
            size.X,
            size.Z,
            [front[0], front[1], back[1], back[0]]);

        var leftNormal = Vector3.Normalize(Vector3.Cross(back[2] - front[0], front[2] - front[0]));
        AddQuad(
            builder,
            BrushSurfaceIds.Left,
            brush,
            leftNormal,
            Vector3.Normalize(front[2] - front[0]),
            Vector3.UnitZ,
            size.Y,
            size.Z,
            [front[0], front[2], back[2], back[0]]);

        var rightNormal = Vector3.Normalize(Vector3.Cross(front[2] - front[1], back[2] - front[1]));
        AddQuad(
            builder,
            BrushSurfaceIds.Right,
            brush,
            rightNormal,
            Vector3.Normalize(back[2] - front[1]),
            -Vector3.UnitZ,
            size.Y,
            size.Z,
            [front[1], back[1], back[2], front[2]]);

        return builder.ToMesh();
    }

    private static void AddCircleCap(
        MeshBuilder builder,
        Brush brush,
        string surfaceId,
        float y,
        Vector3 normal,
        float radiusX,
        float radiusZ,
        bool flipWinding)
    {
        builder.AddSurface(surfaceId, () =>
        {
            var mapping = brush.GetEffectiveSurfaceMapping(surfaceId);
            uint centerIndex = builder.GetNextVertexIndex();
            var centerUv = ApplyMapping(Vector2.Zero, mapping);
            builder.AddVertex(new Vector3(0f, y, 0f), normal, centerUv);

            for (int i = 0; i < CylinderSegments; i++)
            {
                float a = MathF.PI * 2 * i / CylinderSegments;
                var position = new Vector3(MathF.Cos(a) * 0.5f, y, MathF.Sin(a) * 0.5f);
                var uv = ApplyMapping(
                    mapping.TextureLocked
                        ? new Vector2((position.X * radiusX) / TextureWorldSize, (position.Z * radiusZ) / TextureWorldSize)
                        : new Vector2(position.X + 0.5f, position.Z + 0.5f),
                    mapping);
                builder.AddVertex(position, normal, uv);
            }

            for (int i = 0; i < CylinderSegments; i++)
            {
                uint a = centerIndex + 1 + (uint)i;
                uint b = centerIndex + 1 + (uint)((i + 1) % CylinderSegments);
                if (flipWinding)
                {
                    builder.AddTriangleIndices(centerIndex, b, a);
                }
                else
                {
                    builder.AddTriangleIndices(centerIndex, a, b);
                }
            }
        });
    }

    private static void AddQuad(
        MeshBuilder builder,
        string surfaceId,
        Brush brush,
        Vector3 normal,
        Vector3 axisU,
        Vector3 axisV,
        float dimensionU,
        float dimensionV,
        IReadOnlyList<Vector3> corners)
    {
        var mapping = brush.GetEffectiveSurfaceMapping(surfaceId);
        builder.AddSurface(surfaceId, () =>
        {
            for (int i = 0; i < corners.Count; i++)
            {
                var uv = BuildPlanarUv(corners[i], axisU, axisV, dimensionU, dimensionV, mapping);
                builder.AddVertex(corners[i], normal, uv);
            }

            uint baseIndex = builder.GetNextVertexIndex() - 4;
            builder.AddQuadIndices(baseIndex);
        });
    }

    private static void AddTriangle(
        MeshBuilder builder,
        string surfaceId,
        Brush brush,
        Vector3 normal,
        Vector3 axisU,
        Vector3 axisV,
        float dimensionU,
        float dimensionV,
        Vector3 p0,
        Vector3 p1,
        Vector3 p2)
    {
        var mapping = brush.GetEffectiveSurfaceMapping(surfaceId);
        builder.AddSurface(surfaceId, () =>
        {
            builder.AddVertex(p0, normal, BuildPlanarUv(p0, axisU, axisV, dimensionU, dimensionV, mapping));
            builder.AddVertex(p1, normal, BuildPlanarUv(p1, axisU, axisV, dimensionU, dimensionV, mapping));
            builder.AddVertex(p2, normal, BuildPlanarUv(p2, axisU, axisV, dimensionU, dimensionV, mapping));
            uint baseIndex = builder.GetNextVertexIndex() - 3;
            builder.AddTriangleIndices(baseIndex, baseIndex + 1, baseIndex + 2);
        });
    }

    private static void AddExplicitFace(MeshBuilder builder, Brush brush, BrushFace face)
    {
        var vertices = face.Vertices.ToArray();
        if (vertices.Length < 3)
        {
            return;
        }

        var normal = face.GetNormal();
        var axisU = GetFaceAxisU(normal, vertices);
        var axisV = Vector3.Normalize(Vector3.Cross(normal, axisU));
        var mapping = brush.GetEffectiveSurfaceMapping(face.Id);
        var scaledVertices = vertices
            .Select(vertex => Vector3.Multiply(vertex, brush.Transform.Scale))
            .ToArray();
        GetProjectionBounds(scaledVertices, axisU, axisV, out var minU, out var maxU, out var minV, out var maxV);

        builder.AddSurface(face.Id, () =>
        {
            uint baseIndex = builder.GetNextVertexIndex();
            foreach (var (vertex, scaledVertex) in vertices.Zip(scaledVertices))
            {
                var uv = BuildExplicitFaceUv(
                    scaledVertex,
                    axisU,
                    axisV,
                    minU,
                    maxU,
                    minV,
                    maxV,
                    mapping);
                builder.AddVertex(vertex, normal, uv);
            }

            for (uint i = 1; i < vertices.Length - 1; i++)
            {
                builder.AddTriangleIndices(baseIndex, baseIndex + i, baseIndex + i + 1);
            }
        }, face.IsCutterFace);
    }

    private static Vector2 BuildPlanarUv(
        Vector3 localPosition,
        Vector3 axisU,
        Vector3 axisV,
        float dimensionU,
        float dimensionV,
        SurfaceMapping mapping)
    {
        float uCoord = Vector3.Dot(localPosition, axisU);
        float vCoord = Vector3.Dot(localPosition, axisV);

        Vector2 baseUv = mapping.TextureLocked
            ? new Vector2(
                (uCoord + 0.5f) * dimensionU / TextureWorldSize,
                (vCoord + 0.5f) * dimensionV / TextureWorldSize)
            : new Vector2(uCoord + 0.5f, vCoord + 0.5f);

        return ApplyMapping(baseUv, mapping);
    }

    private static Vector2 BuildExplicitFaceUv(
        Vector3 scaledPosition,
        Vector3 axisU,
        Vector3 axisV,
        float minU,
        float maxU,
        float minV,
        float maxV,
        SurfaceMapping mapping)
    {
        float uCoord = Vector3.Dot(scaledPosition, axisU);
        float vCoord = Vector3.Dot(scaledPosition, axisV);

        Vector2 baseUv = mapping.TextureLocked
            ? new Vector2(uCoord / TextureWorldSize, vCoord / TextureWorldSize)
            : new Vector2(
                NormalizeToUnit(uCoord, minU, maxU),
                NormalizeToUnit(vCoord, minV, maxV));

        return ApplyMapping(baseUv, mapping);
    }

    private static void GetProjectionBounds(
        IReadOnlyList<Vector3> vertices,
        Vector3 axisU,
        Vector3 axisV,
        out float minU,
        out float maxU,
        out float minV,
        out float maxV)
    {
        minU = maxU = Vector3.Dot(vertices[0], axisU);
        minV = maxV = Vector3.Dot(vertices[0], axisV);
        foreach (var vertex in vertices.Skip(1))
        {
            float u = Vector3.Dot(vertex, axisU);
            float v = Vector3.Dot(vertex, axisV);
            minU = MathF.Min(minU, u);
            maxU = MathF.Max(maxU, u);
            minV = MathF.Min(minV, v);
            maxV = MathF.Max(maxV, v);
        }
    }

    private static Vector3 GetFaceAxisU(Vector3 normal, IReadOnlyList<Vector3> vertices)
    {
        for (int i = 1; i < vertices.Count; i++)
        {
            var candidate = vertices[i] - vertices[0];
            if (candidate.LengthSquared() > 0.000001f)
            {
                return Vector3.Normalize(candidate);
            }
        }

        var fallback = MathF.Abs(Vector3.Dot(normal, Vector3.UnitY)) < 0.99f
            ? Vector3.UnitY
            : Vector3.UnitX;
        return Vector3.Normalize(Vector3.Cross(fallback, normal));
    }

    private static float NormalizeToUnit(float value, float min, float max)
    {
        float range = max - min;
        if (MathF.Abs(range) <= 0.0001f)
        {
            return 0f;
        }

        return (value - min) / range;
    }

    private static Vector2 ApplyMapping(Vector2 baseUv, SurfaceMapping mapping)
    {
        var scale = new Vector2(
            Math.Abs(mapping.Scale.X) < 0.0001f ? 1f : mapping.Scale.X,
            Math.Abs(mapping.Scale.Y) < 0.0001f ? 1f : mapping.Scale.Y);

        var scaled = new Vector2(baseUv.X * scale.X, baseUv.Y * scale.Y);
        float radians = float.DegreesToRadians(mapping.RotationDegrees);
        float sin = MathF.Sin(radians);
        float cos = MathF.Cos(radians);
        var rotated = new Vector2(
            scaled.X * cos - scaled.Y * sin,
            scaled.X * sin + scaled.Y * cos);

        return rotated + mapping.Offset;
    }

    private static float GetAngularU(float angle, bool textureLocked, float circumference)
    {
        float normalized = angle / (MathF.PI * 2f);
        return textureLocked ? normalized * (circumference / TextureWorldSize) : normalized;
    }

    private static float GetVerticalV(float y, bool textureLocked, float height) =>
        textureLocked ? (y + 0.5f) * height / TextureWorldSize : y + 0.5f;

    private sealed class MeshBuilder
    {
        private readonly List<float> _vertices = [];
        private readonly List<uint> _indices = [];
        private readonly List<MeshSurfaceRange> _surfaces = [];

        public uint GetNextVertexIndex() => (uint)(_vertices.Count / Mesh.FloatsPerVertex);

        public void AddSurface(string surfaceId, Action emit, bool isCutterFace = false)
        {
            int start = _indices.Count;
            emit();
            _surfaces.Add(new MeshSurfaceRange(surfaceId, start, _indices.Count - start, isCutterFace));
        }

        public void AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            _vertices.Add(position.X);
            _vertices.Add(position.Y);
            _vertices.Add(position.Z);
            _vertices.Add(normal.X);
            _vertices.Add(normal.Y);
            _vertices.Add(normal.Z);
            _vertices.Add(uv.X);
            _vertices.Add(uv.Y);
        }

        public void AddTriangleIndices(uint i0, uint i1, uint i2)
        {
            _indices.Add(i0);
            _indices.Add(i1);
            _indices.Add(i2);
        }

        public void AddQuadIndices(uint baseIndex)
        {
            _indices.Add(baseIndex);
            _indices.Add(baseIndex + 1);
            _indices.Add(baseIndex + 2);
            _indices.Add(baseIndex);
            _indices.Add(baseIndex + 2);
            _indices.Add(baseIndex + 3);
        }

        public void AddTriangleVertices(
            Vector3 p0, Vector3 n0, Vector2 uv0,
            Vector3 p1, Vector3 n1, Vector2 uv1,
            Vector3 p2, Vector3 n2, Vector2 uv2)
        {
            uint baseIndex = GetNextVertexIndex();
            AddVertex(p0, n0, uv0);
            AddVertex(p1, n1, uv1);
            AddVertex(p2, n2, uv2);
            AddTriangleIndices(baseIndex, baseIndex + 1, baseIndex + 2);
        }

        public void AddQuadVertices(
            Vector3 p0, Vector3 n0, Vector2 uv0,
            Vector3 p1, Vector3 n1, Vector2 uv1,
            Vector3 p2, Vector3 n2, Vector2 uv2,
            Vector3 p3, Vector3 n3, Vector2 uv3)
        {
            uint baseIndex = GetNextVertexIndex();
            AddVertex(p0, n0, uv0);
            AddVertex(p1, n1, uv1);
            AddVertex(p2, n2, uv2);
            AddVertex(p3, n3, uv3);
            AddQuadIndices(baseIndex);
        }

        public Mesh ToMesh() => new([.. _vertices], [.. _indices], [.. _surfaces]);
    }
}
