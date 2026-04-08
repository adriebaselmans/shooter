using FluentAssertions;
using MapEditor.App.Views;
using MapEditor.Rendering.Cameras;
using System.Numerics;
using System.Windows;

namespace MapEditor.App.Tests;

public sealed class OrthographicViewportProjectionTests
{
    [Fact]
    public void Project_TopViewMapsVerticalToPositiveZWhenDraggingUp()
    {
        var camera = new OrthographicCamera
        {
            Axis = ViewAxis.Top,
            Pan = 100f,
            PanY = 200f,
            Zoom = 50f
        };

        var projected = OrthographicViewportProjection.Project(
            new Point(100, 0),
            camera,
            200,
            100,
            ViewAxis.Top);

        projected.Should().Be(new Vector3(100f, 0f, 250f));
    }

    [Fact]
    public void Project_SideViewMapsHorizontalToPositiveZAndVerticalToPositiveY()
    {
        var camera = new OrthographicCamera
        {
            Axis = ViewAxis.Side,
            Pan = 100f,
            PanY = 200f,
            Zoom = 50f
        };

        var projected = OrthographicViewportProjection.Project(
            new Point(200, 0),
            camera,
            200,
            100,
            ViewAxis.Side);

        projected.Should().Be(new Vector3(0f, 250f, 200f));
    }
}
