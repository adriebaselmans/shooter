using MapEditor.Core.Entities;
using System.Numerics;

namespace MapEditor.Core.Geometry;

public static class BrushGeometryFactory
{
    private const int RadialSegments = 16;

    public static BrushGeometry CreatePrimitiveGeometry(BrushPrimitive primitive) => primitive switch
    {
        BrushPrimitive.Box => CreateBox(),
        BrushPrimitive.Cylinder => CreateCylinder(),
        BrushPrimitive.Cone => CreateCone(),
        BrushPrimitive.Wedge => CreateWedge(),
        _ => throw new ArgumentOutOfRangeException(nameof(primitive))
    };

    public static BrushGeometry GetGeometry(Brush brush) =>
        brush.Geometry?.Clone() ?? CreatePrimitiveGeometry(brush.Primitive);

    public static BrushGeometry CreateWorldGeometry(Brush brush)
    {
        var localGeometry = GetGeometry(brush);
        return BrushGeometryTransforms.Transform(localGeometry, TransformMath.BuildModelMatrix(brush.Transform));
    }

    private static BrushGeometry CreateBox()
    {
        return new BrushGeometry(
        [
            new BrushFace(BrushSurfaceIds.Top,    [new(-0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f)]),
            new BrushFace(BrushSurfaceIds.Bottom, [new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f)]),
            new BrushFace(BrushSurfaceIds.Front,  [new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f)]),
            new BrushFace(BrushSurfaceIds.Back,   [new(0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, -0.5f)]),
            new BrushFace(BrushSurfaceIds.Right,  [new(0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, 0.5f)]),
            new BrushFace(BrushSurfaceIds.Left,   [new(-0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, -0.5f)])
        ]);
    }

    private static BrushGeometry CreateCylinder()
    {
        List<Vector3> top = [];
        List<Vector3> bottom = [];
        List<BrushFace> faces =
        [
            new BrushFace(BrushSurfaceIds.Top, BuildCap(0.5f, reverseWinding: false)),
            new BrushFace(BrushSurfaceIds.Bottom, BuildCap(-0.5f, reverseWinding: true))
        ];

        for (int i = 0; i < RadialSegments; i++)
        {
            float angle = MathF.PI * 2f * i / RadialSegments;
            top.Add(new Vector3(MathF.Cos(angle) * 0.5f, 0.5f, MathF.Sin(angle) * 0.5f));
            bottom.Add(new Vector3(MathF.Cos(angle) * 0.5f, -0.5f, MathF.Sin(angle) * 0.5f));
        }

        for (int i = 0; i < RadialSegments; i++)
        {
            int next = (i + 1) % RadialSegments;
            faces.Add(new BrushFace($"{BrushSurfaceIds.Side}-{i + 1}",
            [
                bottom[i],
                bottom[next],
                top[next],
                top[i]
            ]));
        }

        return new BrushGeometry(faces);
    }

    private static BrushGeometry CreateCone()
    {
        var apex = new Vector3(0f, 0.5f, 0f);
        List<BrushFace> faces =
        [
            new BrushFace(BrushSurfaceIds.Base, BuildCap(-0.5f, reverseWinding: true))
        ];

        for (int i = 0; i < RadialSegments; i++)
        {
            float angle0 = MathF.PI * 2f * i / RadialSegments;
            float angle1 = MathF.PI * 2f * (i + 1) / RadialSegments;
            var p0 = new Vector3(MathF.Cos(angle0) * 0.5f, -0.5f, MathF.Sin(angle0) * 0.5f);
            var p1 = new Vector3(MathF.Cos(angle1) * 0.5f, -0.5f, MathF.Sin(angle1) * 0.5f);
            faces.Add(new BrushFace($"{BrushSurfaceIds.Side}-{i + 1}", [apex, p0, p1]));
        }

        return new BrushGeometry(faces);
    }

    private static BrushGeometry CreateWedge()
    {
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

        return new BrushGeometry(
        [
            new BrushFace(BrushSurfaceIds.Front, [front[0], front[1], front[2]]),
            new BrushFace(BrushSurfaceIds.Back, [back[1], back[0], back[2]]),
            new BrushFace(BrushSurfaceIds.Bottom, [front[0], front[1], back[1], back[0]]),
            new BrushFace(BrushSurfaceIds.Left, [front[0], front[2], back[2], back[0]]),
            new BrushFace(BrushSurfaceIds.Right, [front[1], back[1], back[2], front[2]])
        ]);
    }

    private static IReadOnlyList<Vector3> BuildCap(float y, bool reverseWinding)
    {
        var vertices = new List<Vector3>(RadialSegments);
        for (int i = 0; i < RadialSegments; i++)
        {
            float angle = MathF.PI * 2f * i / RadialSegments;
            vertices.Add(new Vector3(MathF.Cos(angle) * 0.5f, y, MathF.Sin(angle) * 0.5f));
        }

        if (reverseWinding)
        {
            vertices.Reverse();
        }

        return vertices;
    }
}
