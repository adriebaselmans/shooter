using FluentAssertions;
using MapEditor.App.Infrastructure;
using MapEditor.App.Tools;
using MapEditor.Core.Entities;
using System.Numerics;

namespace MapEditor.App.Tests;

public sealed class BrushSurfaceHitTesterTests
{
    [Fact]
    public void HitTest_ReturnsNearestLogicalSurfaceForBox()
    {
        var brush = new Brush
        {
            Primitive = BrushPrimitive.Box,
            Transform = Transform.Identity with
            {
                Scale = new Vector3(128f, 128f, 128f)
            }
        };

        var ray = new ViewportRay(new Vector3(0f, 0f, -256f), Vector3.UnitZ);

        var hit = BrushSurfaceHitTester.HitTest(ray, [brush]);

        hit.Should().NotBeNull();
        hit!.Value.BrushId.Should().Be(brush.Id);
        hit.Value.SurfaceId.Should().Be(BrushSurfaceIds.Back);
    }

    [Fact]
    public void HitTest_RespectsBrushRotation()
    {
        var brush = new Brush
        {
            Primitive = BrushPrimitive.Box,
            Transform = Transform.Identity with
            {
                Scale = new Vector3(128f, 64f, 128f),
                EulerDegrees = new Vector3(0f, 90f, 0f)
            }
        };

        var ray = new ViewportRay(new Vector3(-256f, 0f, 0f), Vector3.UnitX);

        var hit = BrushSurfaceHitTester.HitTest(ray, [brush]);

        hit.Should().NotBeNull();
        hit!.Value.SurfaceId.Should().Be(BrushSurfaceIds.Back);
    }
}
