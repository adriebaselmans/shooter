using FluentAssertions;
using MapEditor.Core.Entities;
using MapEditor.Rendering.Cameras;
using MapEditor.Rendering.Infrastructure;
using System.Numerics;

namespace MapEditor.App.Tests;

public sealed class ResizeHandleMathTests
{
    [Fact]
    public void GetHandles_SideViewUsesZAsHorizontalAxisAndYAsVerticalAxis()
    {
        var transform = new Transform
        {
            Position = new Vector3(16f, 100f, 200f),
            EulerDegrees = Vector3.Zero,
            Scale = new Vector3(32f, 80f, 40f)
        };

        var handles = ResizeHandleMath.GetHandles(transform, ViewAxis.Side);

        handles[0].Position.Should().Be(new Vector3(16f, 60f, 180f));
        handles[2].Position.Should().Be(new Vector3(16f, 140f, 220f));
    }
}
