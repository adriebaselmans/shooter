using MapEditor.Core.Entities;
using System.Numerics;

namespace MapEditor.Core.Geometry;

public static class TransformMath
{
    public static Matrix4x4 BuildModelMatrix(Transform transform)
    {
        var scale = Matrix4x4.CreateScale(transform.Scale);
        var rotation = Matrix4x4.CreateFromYawPitchRoll(
            float.DegreesToRadians(transform.EulerDegrees.Y),
            float.DegreesToRadians(transform.EulerDegrees.X),
            float.DegreesToRadians(transform.EulerDegrees.Z));
        var translation = Matrix4x4.CreateTranslation(transform.Position);
        return scale * rotation * translation;
    }
}
