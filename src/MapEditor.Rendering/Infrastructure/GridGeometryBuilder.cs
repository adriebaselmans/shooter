using MapEditor.Rendering.Cameras;
using System.Numerics;

namespace MapEditor.Rendering.Infrastructure;

/// <summary>
/// Builds world-space editor grid line geometry for orthographic and perspective viewports.
/// </summary>
public static class GridGeometryBuilder
{
    private const int MajorLineFactor = 8;
    private const float MinimumVisibleGridPixels = 12f;
    private const float PerspectiveSpacingFactor = 0.04f;

    public static GridGeometry BuildOrthographic(
        ViewAxis axis,
        float baseSpacing,
        float aspectRatio,
        float pan,
        float panY,
        float zoom)
    {
        float minorSpacing = SelectOrthographicMinorSpacing(baseSpacing, zoom);
        float majorSpacing = minorSpacing * MajorLineFactor;

        float halfHeight = zoom;
        float halfWidth = zoom * aspectRatio;

        return axis switch
        {
            ViewAxis.Top => BuildPlaneGrid(
                minorSpacing,
                majorSpacing,
                pan - halfWidth,
                pan + halfWidth,
                panY - halfHeight,
                panY + halfHeight,
                CreateTopPrimaryLine,
                CreateTopSecondaryLine),
            ViewAxis.Front => BuildPlaneGrid(
                minorSpacing,
                majorSpacing,
                pan - halfWidth,
                pan + halfWidth,
                panY - halfHeight,
                panY + halfHeight,
                CreateFrontPrimaryLine,
                CreateFrontSecondaryLine),
            _ => BuildPlaneGrid(
                minorSpacing,
                majorSpacing,
                pan - halfWidth,
                pan + halfWidth,
                panY - halfHeight,
                panY + halfHeight,
                CreateSidePrimaryLine,
                CreateSideSecondaryLine)
        };
    }

    public static GridGeometry BuildPerspectiveFloor(float baseSpacing, PerspectiveCamera camera)
    {
        float minorSpacing = SelectPerspectiveMinorSpacing(baseSpacing, camera.Distance);
        float majorSpacing = minorSpacing * MajorLineFactor;
        float halfExtent = AlignUp(MathF.Max(majorSpacing * 4f, camera.Distance * 1.5f), minorSpacing);
        float centerX = MathF.Round(camera.Target.X / minorSpacing) * minorSpacing;
        float centerZ = MathF.Round(camera.Target.Z / minorSpacing) * minorSpacing;

        return BuildPlaneGrid(
            minorSpacing,
            majorSpacing,
            centerX - halfExtent,
            centerX + halfExtent,
            centerZ - halfExtent,
            centerZ + halfExtent,
            CreateTopPrimaryLine,
            CreateTopSecondaryLine);
    }

    private static GridGeometry BuildPlaneGrid(
        float minorSpacing,
        float majorSpacing,
        float primaryMin,
        float primaryMax,
        float secondaryMin,
        float secondaryMax,
        Func<float, float, float, (Vector3 Start, Vector3 End)> createPrimaryLine,
        Func<float, float, float, (Vector3 Start, Vector3 End)> createSecondaryLine)
    {
        var minor = new List<Vector3>();
        var major = new List<Vector3>();
        var primaryAxis = new List<Vector3>();
        var secondaryAxis = new List<Vector3>();

        int majorInterval = Math.Max(1, (int)MathF.Round(majorSpacing / minorSpacing));

        AppendAxisAlignedLines(
            minor,
            major,
            primaryAxis,
            minorSpacing,
            majorInterval,
            primaryMin,
            primaryMax,
            secondaryMin,
            secondaryMax,
            createPrimaryLine);

        AppendAxisAlignedLines(
            minor,
            major,
            secondaryAxis,
            minorSpacing,
            majorInterval,
            secondaryMin,
            secondaryMax,
            primaryMin,
            primaryMax,
            createSecondaryLine);

        return new GridGeometry(
            minorSpacing,
            majorSpacing,
            [.. minor],
            [.. major],
            [.. primaryAxis],
            [.. secondaryAxis]);
    }

    private static void AppendAxisAlignedLines(
        List<Vector3> minor,
        List<Vector3> major,
        List<Vector3> axis,
        float spacing,
        int majorInterval,
        float lineMin,
        float lineMax,
        float segmentMin,
        float segmentMax,
        Func<float, float, float, (Vector3 Start, Vector3 End)> createLine)
    {
        int startIndex = (int)MathF.Floor(lineMin / spacing);
        int endIndex = (int)MathF.Ceiling(lineMax / spacing);

        for (int index = startIndex; index <= endIndex; index++)
        {
            float coordinate = index * spacing;
            var (start, end) = createLine(coordinate, segmentMin, segmentMax);

            if (index == 0)
            {
                axis.Add(start);
                axis.Add(end);
            }
            else if (index % majorInterval == 0)
            {
                major.Add(start);
                major.Add(end);
            }
            else
            {
                minor.Add(start);
                minor.Add(end);
            }
        }
    }

    private static float SelectOrthographicMinorSpacing(float baseSpacing, float zoom)
    {
        float worldPerPixel = (zoom * 2f) / 1080f;
        float spacing = MathF.Max(baseSpacing, 1f);

        while ((spacing / worldPerPixel) < MinimumVisibleGridPixels)
        {
            spacing *= 2f;
        }

        return spacing;
    }

    private static float SelectPerspectiveMinorSpacing(float baseSpacing, float cameraDistance)
    {
        float minimumSpacing = MathF.Max(baseSpacing, cameraDistance * PerspectiveSpacingFactor);
        float spacing = MathF.Max(baseSpacing, 1f);

        while (spacing < minimumSpacing)
        {
            spacing *= 2f;
        }

        return spacing;
    }

    private static float AlignUp(float value, float spacing) =>
        MathF.Ceiling(value / spacing) * spacing;

    private static (Vector3 Start, Vector3 End) CreateTopPrimaryLine(float x, float zMin, float zMax) =>
        (new Vector3(x, 0f, zMin), new Vector3(x, 0f, zMax));

    private static (Vector3 Start, Vector3 End) CreateTopSecondaryLine(float z, float xMin, float xMax) =>
        (new Vector3(xMin, 0f, z), new Vector3(xMax, 0f, z));

    private static (Vector3 Start, Vector3 End) CreateFrontPrimaryLine(float x, float yMin, float yMax) =>
        (new Vector3(x, yMin, 0f), new Vector3(x, yMax, 0f));

    private static (Vector3 Start, Vector3 End) CreateFrontSecondaryLine(float y, float xMin, float xMax) =>
        (new Vector3(xMin, y, 0f), new Vector3(xMax, y, 0f));

    private static (Vector3 Start, Vector3 End) CreateSidePrimaryLine(float z, float yMin, float yMax) =>
        (new Vector3(0f, yMin, z), new Vector3(0f, yMax, z));

    private static (Vector3 Start, Vector3 End) CreateSideSecondaryLine(float y, float zMin, float zMax) =>
        (new Vector3(0f, y, zMin), new Vector3(0f, y, zMax));
}

/// <summary>
/// Immutable line batches for a viewport grid.
/// </summary>
public sealed class GridGeometry(
    float minorSpacing,
    float majorSpacing,
    Vector3[] minorVertices,
    Vector3[] majorVertices,
    Vector3[] primaryAxisVertices,
    Vector3[] secondaryAxisVertices)
{
    public float MinorSpacing { get; } = minorSpacing;
    public float MajorSpacing { get; } = majorSpacing;
    public Vector3[] MinorVertices { get; } = minorVertices;
    public Vector3[] MajorVertices { get; } = majorVertices;
    public Vector3[] PrimaryAxisVertices { get; } = primaryAxisVertices;
    public Vector3[] SecondaryAxisVertices { get; } = secondaryAxisVertices;
}
