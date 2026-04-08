using MapEditor.Core.Entities;
using MapEditor.Rendering.Cameras;
using System.Numerics;

namespace MapEditor.Rendering.Infrastructure;

public enum ResizeHandleKind
{
    MinPrimaryMinSecondary,
    MaxPrimaryMinSecondary,
    MaxPrimaryMaxSecondary,
    MinPrimaryMaxSecondary
}

public readonly record struct ResizeHandleData(ResizeHandleKind Kind, Vector3 Position);

public static class ResizeHandleMath
{
    public static float GetHandleSize(float zoom, float gridSize) =>
        MathF.Max(gridSize * 0.35f, zoom * 0.04f);

    public static ResizeHandleData[] GetHandles(Transform transform, ViewAxis axis)
    {
        var plane = VisiblePlane.FromTransform(transform, axis);
        return
        [
            new ResizeHandleData(
                ResizeHandleKind.MinPrimaryMinSecondary,
                plane.CreatePoint(plane.MinPrimary, plane.MinSecondary)),
            new ResizeHandleData(
                ResizeHandleKind.MaxPrimaryMinSecondary,
                plane.CreatePoint(plane.MaxPrimary, plane.MinSecondary)),
            new ResizeHandleData(
                ResizeHandleKind.MaxPrimaryMaxSecondary,
                plane.CreatePoint(plane.MaxPrimary, plane.MaxSecondary)),
            new ResizeHandleData(
                ResizeHandleKind.MinPrimaryMaxSecondary,
                plane.CreatePoint(plane.MinPrimary, plane.MaxSecondary))
        ];
    }

    public static ResizeHandleKind? HitTestHandle(
        Transform transform,
        ViewAxis axis,
        Vector3 worldPoint,
        float hitRadius)
    {
        var handles = GetHandles(transform, axis);
        float nearestDistanceSquared = hitRadius * hitRadius;
        ResizeHandleKind? nearestHandle = null;

        foreach (var handle in handles)
        {
            var delta = ProjectToPlane(handle.Position - worldPoint, axis);
            float distanceSquared = delta.LengthSquared();
            if (distanceSquared > nearestDistanceSquared)
            {
                continue;
            }

            nearestDistanceSquared = distanceSquared;
            nearestHandle = handle.Kind;
        }

        return nearestHandle;
    }

    public static Transform ResizeFromHandle(
        Transform original,
        ViewAxis axis,
        ResizeHandleKind handleKind,
        Vector3 draggedWorldPoint,
        float minimumVisibleSize)
    {
        var plane = VisiblePlane.FromTransform(original, axis);
        var anchor = GetOppositeCorner(plane, handleKind);
        var dragged = plane.Project(draggedWorldPoint);

        float minPrimary = MathF.Min(anchor.Primary, dragged.Primary);
        float maxPrimary = MathF.Max(anchor.Primary, dragged.Primary);
        float minSecondary = MathF.Min(anchor.Secondary, dragged.Secondary);
        float maxSecondary = MathF.Max(anchor.Secondary, dragged.Secondary);

        EnsureMinimumSize(anchor.Primary, dragged.Primary, minimumVisibleSize, ref minPrimary, ref maxPrimary);
        EnsureMinimumSize(anchor.Secondary, dragged.Secondary, minimumVisibleSize, ref minSecondary, ref maxSecondary);

        return plane.CreateTransform(minPrimary, maxPrimary, minSecondary, maxSecondary);
    }

    public static Vector3[] BuildHandleOutlineVertices(Transform transform, ViewAxis axis, float handleSize)
    {
        var handles = GetHandles(transform, axis);
        var vertices = new List<Vector3>(handles.Length * 8);

        foreach (var handle in handles)
        {
            var center = handle.Position;
            var minPrimary = GetPrimary(center, axis) - handleSize;
            var maxPrimary = GetPrimary(center, axis) + handleSize;
            var minSecondary = GetSecondary(center, axis) - handleSize;
            var maxSecondary = GetSecondary(center, axis) + handleSize;

            AppendRectangle(
                vertices,
                axis,
                GetHidden(center, axis),
                minPrimary,
                maxPrimary,
                minSecondary,
                maxSecondary);
        }

        return [.. vertices];
    }

    private static void EnsureMinimumSize(
        float anchorValue,
        float draggedValue,
        float minimumVisibleSize,
        ref float minValue,
        ref float maxValue)
    {
        if ((maxValue - minValue) >= minimumVisibleSize)
        {
            return;
        }

        if (draggedValue >= anchorValue)
        {
            maxValue = anchorValue + minimumVisibleSize;
            minValue = anchorValue;
            return;
        }

        minValue = anchorValue - minimumVisibleSize;
        maxValue = anchorValue;
    }

    private static (float Primary, float Secondary) GetOppositeCorner(VisiblePlane plane, ResizeHandleKind handleKind) => handleKind switch
    {
        ResizeHandleKind.MinPrimaryMinSecondary => (plane.MaxPrimary, plane.MaxSecondary),
        ResizeHandleKind.MaxPrimaryMinSecondary => (plane.MinPrimary, plane.MaxSecondary),
        ResizeHandleKind.MaxPrimaryMaxSecondary => (plane.MinPrimary, plane.MinSecondary),
        _ => (plane.MaxPrimary, plane.MinSecondary)
    };

    private static Vector2 ProjectToPlane(Vector3 value, ViewAxis axis) => axis switch
    {
        ViewAxis.Top => new Vector2(value.X, value.Z),
        ViewAxis.Front => new Vector2(value.X, value.Y),
        _ => new Vector2(value.Z, value.Y)
    };

    private static float GetPrimary(Vector3 value, ViewAxis axis) => axis switch
    {
        ViewAxis.Side => value.Z,
        _ => value.X
    };

    private static float GetSecondary(Vector3 value, ViewAxis axis) => axis switch
    {
        ViewAxis.Top => value.Z,
        _ => value.Y
    };

    private static float GetHidden(Vector3 value, ViewAxis axis) => axis switch
    {
        ViewAxis.Top => value.Y,
        ViewAxis.Front => value.Z,
        _ => value.X
    };

    private static void AppendRectangle(
        ICollection<Vector3> vertices,
        ViewAxis axis,
        float hidden,
        float minPrimary,
        float maxPrimary,
        float minSecondary,
        float maxSecondary)
    {
        var p1 = CreatePoint(axis, hidden, minPrimary, minSecondary);
        var p2 = CreatePoint(axis, hidden, maxPrimary, minSecondary);
        var p3 = CreatePoint(axis, hidden, maxPrimary, maxSecondary);
        var p4 = CreatePoint(axis, hidden, minPrimary, maxSecondary);

        vertices.Add(p1);
        vertices.Add(p2);
        vertices.Add(p2);
        vertices.Add(p3);
        vertices.Add(p3);
        vertices.Add(p4);
        vertices.Add(p4);
        vertices.Add(p1);
    }

    private static Vector3 CreatePoint(ViewAxis axis, float hidden, float primary, float secondary) => axis switch
    {
        ViewAxis.Top => new Vector3(primary, hidden, secondary),
        ViewAxis.Front => new Vector3(primary, secondary, hidden),
        _ => new Vector3(hidden, secondary, primary)
    };

    private readonly record struct VisiblePlane(
        ViewAxis Axis,
        float MinPrimary,
        float MaxPrimary,
        float MinSecondary,
        float MaxSecondary,
        float HiddenCenter,
        float HiddenScale)
    {
        public static VisiblePlane FromTransform(Transform transform, ViewAxis axis)
        {
            var half = transform.Scale * 0.5f;
            return axis switch
            {
                ViewAxis.Top => new VisiblePlane(
                    axis,
                    transform.Position.X - half.X,
                    transform.Position.X + half.X,
                    transform.Position.Z - half.Z,
                    transform.Position.Z + half.Z,
                    transform.Position.Y,
                    transform.Scale.Y),
                ViewAxis.Front => new VisiblePlane(
                    axis,
                    transform.Position.X - half.X,
                    transform.Position.X + half.X,
                    transform.Position.Y - half.Y,
                    transform.Position.Y + half.Y,
                    transform.Position.Z,
                    transform.Scale.Z),
                _ => new VisiblePlane(
                    axis,
                    transform.Position.Z - half.Z,
                    transform.Position.Z + half.Z,
                    transform.Position.Y - half.Y,
                    transform.Position.Y + half.Y,
                    transform.Position.X,
                    transform.Scale.X)
            };
        }

        public (float Primary, float Secondary) Project(Vector3 worldPoint) => Axis switch
        {
            ViewAxis.Top => (worldPoint.X, worldPoint.Z),
            ViewAxis.Front => (worldPoint.X, worldPoint.Y),
            _ => (worldPoint.Z, worldPoint.Y)
        };

        public Vector3 CreatePoint(float primary, float secondary) =>
            ResizeHandleMath.CreatePoint(Axis, HiddenCenter, primary, secondary);

        public Transform CreateTransform(
            float minPrimary,
            float maxPrimary,
            float minSecondary,
            float maxSecondary) => Axis switch
        {
            ViewAxis.Top => new Transform
            {
                Position = new Vector3(
                    (minPrimary + maxPrimary) * 0.5f,
                    HiddenCenter,
                    (minSecondary + maxSecondary) * 0.5f),
                EulerDegrees = Vector3.Zero,
                Scale = new Vector3(
                    maxPrimary - minPrimary,
                    HiddenScale,
                    maxSecondary - minSecondary)
            },
            ViewAxis.Front => new Transform
            {
                Position = new Vector3(
                    (minPrimary + maxPrimary) * 0.5f,
                    (minSecondary + maxSecondary) * 0.5f,
                    HiddenCenter),
                EulerDegrees = Vector3.Zero,
                Scale = new Vector3(
                    maxPrimary - minPrimary,
                    maxSecondary - minSecondary,
                    HiddenScale)
            },
            _ => new Transform
            {
                Position = new Vector3(
                    HiddenCenter,
                    (minSecondary + maxSecondary) * 0.5f,
                    (minPrimary + maxPrimary) * 0.5f),
                EulerDegrees = Vector3.Zero,
                Scale = new Vector3(
                    HiddenScale,
                    maxSecondary - minSecondary,
                    maxPrimary - minPrimary)
            }
        };
    }
}
