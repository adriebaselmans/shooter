using System.Numerics;
using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;

namespace Shooter.Game;

/// <summary>Builds runtime WorldBrush instances from editor brushes.</summary>
internal static class WorldBrushFactory
{
    public static WorldBrush Build(Brush brush)
    {
        var mesh = MeshGenerator.GenerateMesh(brush);
        var model = TransformMath.BuildModelMatrix(brush.Transform);
        Matrix4x4.Invert(model, out var inverseModel);
        var normalMatrix = Matrix4x4.Transpose(inverseModel);

        var triangles = ExtractWorldTriangles(mesh, model);
        var (boundsMin, boundsMax) = ComputeBounds(triangles);
        string? texturePath = WorldMaterialResolver.ResolveTexturePath(brush.MaterialName);
        var visuals = WorldMaterialResolver.InferSurfaceVisuals(brush.MaterialName, texturePath);
        var authored = brush.MaterialProperties;
        bool useAuthored = authored.Kind != BrushMaterialKind.Standard || authored != BrushMaterialProperties.Default;

        return new WorldBrush
        {
            BrushId = brush.Id,
            Mesh = mesh,
            Model = model,
            NormalMatrix = normalMatrix,
            BoundsMin = boundsMin - new Vector3(0.01f),
            BoundsMax = boundsMax + new Vector3(0.01f),
            Triangles = triangles,
            MaterialName = brush.MaterialName,
            TexturePath = texturePath,
            TintColor = WorldMaterialResolver.ResolveTintColor(brush.MaterialName, texturePath),
            MaterialKind = useAuthored ? authored.Kind : BrushMaterialKind.Standard,
            Roughness = useAuthored ? authored.Roughness : visuals.Roughness,
            Metallic = useAuthored ? authored.Metallic : visuals.Metallic,
            SpecularStrength = useAuthored ? authored.SpecularStrength : visuals.SpecularStrength,
            DetailNormalStrength = useAuthored ? authored.NormalStrength : visuals.DetailNormalStrength,
            EmissiveStrength = useAuthored ? authored.EmissiveStrength : 0.0f,
            Opacity = useAuthored ? authored.Opacity : 1.0f,
            FlowSpeed = useAuthored ? authored.FlowSpeed : Vector2.Zero,
            DistortionStrength = useAuthored ? authored.DistortionStrength : 0.0f,
            FresnelStrength = useAuthored ? authored.FresnelStrength : 0.0f,
            PulseStrength = useAuthored ? authored.PulseStrength : 0.0f,
        };
    }

    private static IReadOnlyList<WorldTriangle> ExtractWorldTriangles(Mesh mesh, Matrix4x4 model)
    {
        var vertices = mesh.Vertices;
        var indices = mesh.Indices;
        var triangles = new WorldTriangle[indices.Length / 3];
        for (int t = 0; t < triangles.Length; t++)
        {
            int i0 = (int)indices[t * 3 + 0] * Mesh.FloatsPerVertex;
            int i1 = (int)indices[t * 3 + 1] * Mesh.FloatsPerVertex;
            int i2 = (int)indices[t * 3 + 2] * Mesh.FloatsPerVertex;
            var v0 = Vector3.Transform(new Vector3(vertices[i0], vertices[i0 + 1], vertices[i0 + 2]), model);
            var v1 = Vector3.Transform(new Vector3(vertices[i1], vertices[i1 + 1], vertices[i1 + 2]), model);
            var v2 = Vector3.Transform(new Vector3(vertices[i2], vertices[i2 + 1], vertices[i2 + 2]), model);
            var normal = Vector3.Cross(v1 - v0, v2 - v0);
            float length = normal.Length();
            normal = length > 1e-6f ? normal / length : Vector3.UnitY;
            triangles[t] = new WorldTriangle(v0, v1, v2, normal);
        }
        return triangles;
    }

    private static (Vector3 Min, Vector3 Max) ComputeBounds(IReadOnlyList<WorldTriangle> triangles)
    {
        if (triangles.Count == 0) return (Vector3.Zero, Vector3.Zero);
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var triangle in triangles)
        {
            min = Vector3.Min(min, Vector3.Min(triangle.V0, Vector3.Min(triangle.V1, triangle.V2)));
            max = Vector3.Max(max, Vector3.Max(triangle.V0, Vector3.Max(triangle.V1, triangle.V2)));
        }
        return (min, max);
    }
}
