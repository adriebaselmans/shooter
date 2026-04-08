using System.Numerics;
using MapEditor.Core.Entities;

namespace MapEditor.Core.Geometry;

/// <summary>
/// Generates display meshes for BSP brush primitives.
/// All meshes use interleaved vertex format: [x,y,z, nx,ny,nz] per vertex.
/// Generated geometry is in local space (centred at origin); the caller applies the transform.
/// </summary>
public static class MeshGenerator
{
    private const int CylinderSegments = 16;
    private const int ConeSegments = 16;

    /// <summary>Generates a unit mesh for the given primitive in local space (unit size: 1x1x1).</summary>
    public static Mesh GenerateMesh(BrushPrimitive primitive) => primitive switch
    {
        BrushPrimitive.Box      => GenerateBox(),
        BrushPrimitive.Cylinder => GenerateCylinder(),
        BrushPrimitive.Cone     => GenerateCone(),
        BrushPrimitive.Wedge    => GenerateWedge(),
        _ => throw new ArgumentOutOfRangeException(nameof(primitive))
    };

    // ── Box ──────────────────────────────────────────────────────────────────
    private static Mesh GenerateBox()
    {
        // 6 faces × 4 vertices = 24 vertices, 6 × 2 triangles = 12 triangles
        var verts = new List<float>();
        var idxs  = new List<uint>();

        // Each face: normal, and 4 corner positions
        (Vector3 normal, Vector3[] corners)[] faces =
        [
            (Vector3.UnitY,  [new(-0.5f, 0.5f,-0.5f), new( 0.5f, 0.5f,-0.5f), new( 0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f)]),  // top
            (-Vector3.UnitY, [new(-0.5f,-0.5f, 0.5f), new( 0.5f,-0.5f, 0.5f), new( 0.5f,-0.5f,-0.5f), new(-0.5f,-0.5f,-0.5f)]),  // bottom
            (Vector3.UnitZ,  [new(-0.5f,-0.5f, 0.5f), new( 0.5f,-0.5f, 0.5f), new( 0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f)]),  // front
            (-Vector3.UnitZ, [new( 0.5f,-0.5f,-0.5f), new(-0.5f,-0.5f,-0.5f), new(-0.5f, 0.5f,-0.5f), new( 0.5f, 0.5f,-0.5f)]),  // back
            (Vector3.UnitX,  [new( 0.5f,-0.5f, 0.5f), new( 0.5f,-0.5f,-0.5f), new( 0.5f, 0.5f,-0.5f), new( 0.5f, 0.5f, 0.5f)]),  // right
            (-Vector3.UnitX, [new(-0.5f,-0.5f,-0.5f), new(-0.5f,-0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f,-0.5f)])   // left
        ];

        foreach (var (normal, corners) in faces)
        {
            uint baseIdx = (uint)(verts.Count / 6);
            foreach (var c in corners) AppendVertex(verts, c, normal);
            AppendQuad(idxs, baseIdx);
        }

        return new Mesh([.. verts], [.. idxs]);
    }

    // ── Cylinder ─────────────────────────────────────────────────────────────
    private static Mesh GenerateCylinder()
    {
        var verts = new List<float>();
        var idxs  = new List<uint>();
        int n = CylinderSegments;

        // Side faces
        for (int i = 0; i < n; i++)
        {
            float a0 = MathF.PI * 2 * i       / n;
            float a1 = MathF.PI * 2 * (i + 1) / n;
            var p00 = new Vector3(MathF.Cos(a0) * 0.5f, -0.5f, MathF.Sin(a0) * 0.5f);
            var p10 = new Vector3(MathF.Cos(a1) * 0.5f, -0.5f, MathF.Sin(a1) * 0.5f);
            var p01 = new Vector3(MathF.Cos(a0) * 0.5f,  0.5f, MathF.Sin(a0) * 0.5f);
            var p11 = new Vector3(MathF.Cos(a1) * 0.5f,  0.5f, MathF.Sin(a1) * 0.5f);
            var n0  = Vector3.Normalize(new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0)));
            var n1  = Vector3.Normalize(new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1)));

            uint b = (uint)(verts.Count / 6);
            AppendVertex(verts, p00, n0); AppendVertex(verts, p10, n1);
            AppendVertex(verts, p11, n1); AppendVertex(verts, p01, n0);
            AppendQuad(idxs, b);
        }

        // Top and bottom caps
        AppendCircleCap(verts, idxs, y: 0.5f, Vector3.UnitY, n, flipWinding: false);
        AppendCircleCap(verts, idxs, y: -0.5f, -Vector3.UnitY, n, flipWinding: true);

        return new Mesh([.. verts], [.. idxs]);
    }

    // ── Cone ─────────────────────────────────────────────────────────────────
    private static Mesh GenerateCone()
    {
        var verts = new List<float>();
        var idxs  = new List<uint>();
        int n = ConeSegments;
        var apex = new Vector3(0, 0.5f, 0);

        for (int i = 0; i < n; i++)
        {
            float a0 = MathF.PI * 2 * i       / n;
            float a1 = MathF.PI * 2 * (i + 1) / n;
            var b0 = new Vector3(MathF.Cos(a0) * 0.5f, -0.5f, MathF.Sin(a0) * 0.5f);
            var b1 = new Vector3(MathF.Cos(a1) * 0.5f, -0.5f, MathF.Sin(a1) * 0.5f);

            var edge0 = b0 - apex;
            var edge1 = b1 - apex;
            var faceNormal = Vector3.Normalize(Vector3.Cross(edge1, edge0));

            uint b = (uint)(verts.Count / 6);
            AppendVertex(verts, apex, faceNormal);
            AppendVertex(verts, b0,   faceNormal);
            AppendVertex(verts, b1,   faceNormal);
            idxs.AddRange([b, b + 1, b + 2]);
        }

        AppendCircleCap(verts, idxs, y: -0.5f, -Vector3.UnitY, n, flipWinding: true);

        return new Mesh([.. verts], [.. idxs]);
    }

    // ── Wedge (triangular prism) ──────────────────────────────────────────────
    private static Mesh GenerateWedge()
    {
        var verts = new List<float>();
        var idxs  = new List<uint>();

        // 6 vertices of a triangular prism: front/back triangle + connecting quads
        // Triangle cross-section in XY, extruded along Z
        Vector3[] front =
        [
            new(-0.5f, -0.5f,  0.5f),
            new( 0.5f, -0.5f,  0.5f),
            new( 0.0f,  0.5f,  0.5f)
        ];
        Vector3[] back =
        [
            new(-0.5f, -0.5f, -0.5f),
            new( 0.5f, -0.5f, -0.5f),
            new( 0.0f,  0.5f, -0.5f)
        ];

        // Front face
        uint b = (uint)(verts.Count / 6);
        foreach (var v in front) AppendVertex(verts, v, Vector3.UnitZ);
        idxs.AddRange([b, b + 1, b + 2]);

        // Back face
        b = (uint)(verts.Count / 6);
        foreach (var v in back) AppendVertex(verts, v, -Vector3.UnitZ);
        idxs.AddRange([b, b + 2, b + 1]);

        // Bottom quad
        b = (uint)(verts.Count / 6);
        AppendVertex(verts, front[0], -Vector3.UnitY);
        AppendVertex(verts, front[1], -Vector3.UnitY);
        AppendVertex(verts, back[1],  -Vector3.UnitY);
        AppendVertex(verts, back[0],  -Vector3.UnitY);
        AppendQuad(idxs, b);

        // Left slope quad
        var leftNormal = Vector3.Normalize(Vector3.Cross(back[2] - front[0], front[2] - front[0]));
        b = (uint)(verts.Count / 6);
        AppendVertex(verts, front[0], leftNormal);
        AppendVertex(verts, front[2], leftNormal);
        AppendVertex(verts, back[2],  leftNormal);
        AppendVertex(verts, back[0],  leftNormal);
        AppendQuad(idxs, b);

        // Right slope quad
        var rightNormal = Vector3.Normalize(Vector3.Cross(front[2] - front[1], back[2] - front[1]));
        b = (uint)(verts.Count / 6);
        AppendVertex(verts, front[1], rightNormal);
        AppendVertex(verts, back[1],  rightNormal);
        AppendVertex(verts, back[2],  rightNormal);
        AppendVertex(verts, front[2], rightNormal);
        AppendQuad(idxs, b);

        return new Mesh([.. verts], [.. idxs]);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void AppendVertex(List<float> verts, Vector3 pos, Vector3 normal)
    {
        verts.Add(pos.X);    verts.Add(pos.Y);    verts.Add(pos.Z);
        verts.Add(normal.X); verts.Add(normal.Y); verts.Add(normal.Z);
    }

    private static void AppendQuad(List<uint> idxs, uint b)
    {
        idxs.AddRange([b, b + 1, b + 2, b, b + 2, b + 3]);
    }

    private static void AppendCircleCap(
        List<float> verts, List<uint> idxs,
        float y, Vector3 normal, int segments, bool flipWinding)
    {
        uint centerIdx = (uint)(verts.Count / 6);
        AppendVertex(verts, new Vector3(0, y, 0), normal);

        for (int i = 0; i < segments; i++)
        {
            float a = MathF.PI * 2 * i / segments;
            AppendVertex(verts, new Vector3(MathF.Cos(a) * 0.5f, y, MathF.Sin(a) * 0.5f), normal);
        }

        for (int i = 0; i < segments; i++)
        {
            uint a = centerIdx + 1 + (uint)i;
            uint b = centerIdx + 1 + (uint)((i + 1) % segments);
            if (flipWinding)
                idxs.AddRange([centerIdx, b, a]);
            else
                idxs.AddRange([centerIdx, a, b]);
        }
    }
}
