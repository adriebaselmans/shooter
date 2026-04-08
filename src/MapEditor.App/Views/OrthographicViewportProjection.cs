using MapEditor.Rendering.Cameras;
using System.Numerics;
using System.Windows;

namespace MapEditor.App.Views;

internal static class OrthographicViewportProjection
{
    public static Vector3 Project(Point position, OrthographicCamera camera, int pixelWidth, int pixelHeight, ViewAxis axis)
    {
        float aspect = (float)pixelWidth / pixelHeight;
        float halfHeight = camera.Zoom;
        float halfWidth = halfHeight * aspect;
        float xRatio = (float)(position.X / Math.Max(1d, pixelWidth));
        float yRatio = (float)(position.Y / Math.Max(1d, pixelHeight));

        return axis switch
        {
            ViewAxis.Top => new Vector3(
                camera.Pan + ((xRatio - 0.5f) * 2f * halfWidth),
                0f,
                camera.PanY + ((0.5f - yRatio) * 2f * halfHeight)),
            ViewAxis.Front => new Vector3(
                camera.Pan + ((xRatio - 0.5f) * 2f * halfWidth),
                camera.PanY + ((0.5f - yRatio) * 2f * halfHeight),
                0f),
            _ => new Vector3(
                0f,
                camera.PanY + ((0.5f - yRatio) * 2f * halfHeight),
                camera.Pan + ((xRatio - 0.5f) * 2f * halfWidth))
        };
    }
}
