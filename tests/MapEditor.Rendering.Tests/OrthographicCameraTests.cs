using FluentAssertions;
using MapEditor.Rendering.Cameras;
using System.Numerics;

namespace MapEditor.Rendering.Tests;

[Collection(GlRenderCollectionDefinition.Name)]
public sealed class OrthographicCameraTests
{
    [Fact]
    public void TopView_UsesXAsHorizontalAxisAndZAsVerticalAxis()
    {
        var camera = new OrthographicCamera
        {
            Axis = ViewAxis.Top,
            Pan = 100f,
            PanY = 200f,
            Zoom = 50f
        };

        var projection = camera.GetProjectionMatrix(1f);
        var view = camera.GetViewMatrix();

        var lowerX = ToNdcX(new Vector3(80f, 0f, 200f), view, projection);
        var higherX = ToNdcX(new Vector3(120f, 0f, 200f), view, projection);
        var lowerZ = ToNdcY(new Vector3(100f, 0f, 180f), view, projection);
        var higherZ = ToNdcY(new Vector3(100f, 0f, 220f), view, projection);

        higherX.Should().BeGreaterThan(lowerX);
        higherZ.Should().BeGreaterThan(lowerZ);
    }

    [Fact]
    public void SideView_UsesZAsHorizontalAxisAndYAsVerticalAxis()
    {
        var camera = new OrthographicCamera
        {
            Axis = ViewAxis.Side,
            Pan = 100f,
            PanY = 200f,
            Zoom = 50f
        };

        var projection = camera.GetProjectionMatrix(1f);
        var view = camera.GetViewMatrix();

        var lowerZ = ToNdcX(new Vector3(0f, 200f, 80f), view, projection);
        var higherZ = ToNdcX(new Vector3(0f, 200f, 120f), view, projection);
        var lowerY = ToNdcY(new Vector3(0f, 180f, 100f), view, projection);
        var higherY = ToNdcY(new Vector3(0f, 220f, 100f), view, projection);

        higherZ.Should().BeGreaterThan(lowerZ);
        higherY.Should().BeGreaterThan(lowerY);
    }

    private static float ToNdcX(Vector3 point, Matrix4x4 view, Matrix4x4 projection)
    {
        var clip = Vector4.Transform(Vector4.Transform(new Vector4(point, 1f), view), projection);
        return clip.X / clip.W;
    }

    private static float ToNdcY(Vector3 point, Matrix4x4 view, Matrix4x4 projection)
    {
        var clip = Vector4.Transform(Vector4.Transform(new Vector4(point, 1f), view), projection);
        return clip.Y / clip.W;
    }
}
