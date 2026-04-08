using FluentAssertions;
using MapEditor.Rendering.Cameras;
using MapEditor.Rendering.Infrastructure;
using System.Numerics;

namespace MapEditor.Rendering.Tests;

public sealed class GridGeometryBuilderTests
{
    [Fact]
    public void BuildOrthographic_UsesWorldOriginAxesForTopView()
    {
        var geometry = GridGeometryBuilder.BuildOrthographic(
            ViewAxis.Top,
            baseSpacing: 32f,
            aspectRatio: 1f,
            pan: 0f,
            panY: 0f,
            zoom: 64f);

        geometry.MinorSpacing.Should().Be(32f);
        geometry.MajorVertices.Should().BeEmpty();
        geometry.PrimaryAxisVertices.Should().Equal(
            new Vector3(0f, 0f, -64f),
            new Vector3(0f, 0f, 64f));
        geometry.SecondaryAxisVertices.Should().Equal(
            new Vector3(-64f, 0f, 0f),
            new Vector3(64f, 0f, 0f));
    }

    [Fact]
    public void BuildOrthographic_IncreasesMinorSpacingWhenZoomedFarOut()
    {
        var geometry = GridGeometryBuilder.BuildOrthographic(
            ViewAxis.Front,
            baseSpacing: 32f,
            aspectRatio: 1.6f,
            pan: 0f,
            panY: 0f,
            zoom: 5000f);

        geometry.MinorSpacing.Should().BeGreaterThan(32f);
        geometry.MajorSpacing.Should().Be(geometry.MinorSpacing * 8f);
    }

    [Fact]
    public void BuildPerspectiveFloor_SnapsGridToCameraTargetAndKeepsWorldAxes()
    {
        var camera = new PerspectiveCamera
        {
            Distance = 4000f,
            Target = new Vector3(70f, 0f, 130f)
        };

        var geometry = GridGeometryBuilder.BuildPerspectiveFloor(32f, camera);

        geometry.MinorSpacing.Should().Be(256f);
        geometry.PrimaryAxisVertices.Should().Contain(vertex => vertex.X == 0f);
        geometry.SecondaryAxisVertices.Should().Contain(vertex => vertex.Z == 0f);
        geometry.MinorVertices.Should().OnlyContain(vertex =>
            IsAligned(vertex.X, geometry.MinorSpacing) &&
            IsAligned(vertex.Z, geometry.MinorSpacing));
    }

    private static bool IsAligned(float value, float spacing)
    {
        float snapped = MathF.Round(value / spacing) * spacing;
        return MathF.Abs(value - snapped) < 0.001f;
    }
}
