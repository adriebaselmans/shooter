using MapEditor.App.Tools;
using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;
using System.Numerics;

namespace MapEditor.App.Infrastructure;

internal readonly record struct BrushSurfaceHit(Guid BrushId, string SurfaceId, float Distance);

internal static class BrushSurfaceHitTester
{
    public static BrushSurfaceHit? HitTest(ViewportRay ray, IEnumerable<Brush> brushes)
    {
        BrushSurfaceHit? bestHit = null;

        foreach (var brush in brushes)
        {
            var mesh = MeshGenerator.GenerateMesh(brush);
            var model = BuildModelMatrix(brush.Transform);

            foreach (var surface in mesh.Surfaces)
            {
                int end = surface.IndexStart + surface.IndexCount;
                for (int i = surface.IndexStart; i <= end - 3; i += 3)
                {
                    var p0 = GetWorldVertex(mesh, mesh.Indices[i], model);
                    var p1 = GetWorldVertex(mesh, mesh.Indices[i + 1], model);
                    var p2 = GetWorldVertex(mesh, mesh.Indices[i + 2], model);
                    if (!TryIntersectTriangle(ray, p0, p1, p2, out var distance))
                    {
                        continue;
                    }

                    if (bestHit is null || distance < bestHit.Value.Distance)
                    {
                        bestHit = new BrushSurfaceHit(brush.Id, surface.SurfaceId, distance);
                    }
                }
            }
        }

        return bestHit;
    }

    private static Matrix4x4 BuildModelMatrix(Transform transform)
    {
        var scale = Matrix4x4.CreateScale(transform.Scale);
        var rotation = Matrix4x4.CreateFromYawPitchRoll(
            float.DegreesToRadians(transform.EulerDegrees.Y),
            float.DegreesToRadians(transform.EulerDegrees.X),
            float.DegreesToRadians(transform.EulerDegrees.Z));
        var translation = Matrix4x4.CreateTranslation(transform.Position);
        return scale * rotation * translation;
    }

    private static Vector3 GetWorldVertex(Mesh mesh, uint index, Matrix4x4 model)
    {
        int baseOffset = checked((int)index) * Mesh.FloatsPerVertex;
        var position = new Vector3(
            mesh.Vertices[baseOffset],
            mesh.Vertices[baseOffset + 1],
            mesh.Vertices[baseOffset + 2]);
        return Vector3.Transform(position, model);
    }

    private static bool TryIntersectTriangle(
        ViewportRay ray,
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        out float distance)
    {
        const float epsilon = 0.0001f;
        distance = 0f;

        var edge1 = p1 - p0;
        var edge2 = p2 - p0;
        var h = Vector3.Cross(ray.Direction, edge2);
        var a = Vector3.Dot(edge1, h);
        if (MathF.Abs(a) < epsilon)
        {
            return false;
        }

        float f = 1f / a;
        var s = ray.Origin - p0;
        float u = f * Vector3.Dot(s, h);
        if (u < 0f || u > 1f)
        {
            return false;
        }

        var q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(ray.Direction, q);
        if (v < 0f || u + v > 1f)
        {
            return false;
        }

        float t = f * Vector3.Dot(edge2, q);
        if (t <= epsilon)
        {
            return false;
        }

        distance = t;
        return true;
    }
}
