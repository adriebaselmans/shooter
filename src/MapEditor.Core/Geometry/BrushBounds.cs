using MapEditor.Core.Entities;
using System.Numerics;

namespace MapEditor.Core.Geometry;

public static class BrushBounds
{
    public static bool TryGetWorldBounds(Brush brush, out Vector3 min, out Vector3 max)
    {
        ArgumentNullException.ThrowIfNull(brush);

        var geometry = BrushGeometryFactory.CreateWorldGeometry(brush);
        if (!geometry.HasFaces)
        {
            min = max = Vector3.Zero;
            return false;
        }

        (min, max) = geometry.GetBounds();
        return true;
    }
}
