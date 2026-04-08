using FluentAssertions;
using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;
using MapEditor.Rendering.Buffers;

namespace MapEditor.Rendering.Tests;

public sealed class MeshEdgeIndexBuilderTests
{
    [Fact]
    public void BuildFeatureEdges_RemovesSharedDiagonalFromTriangulatedQuad()
    {
        var mesh = new Mesh(
        [
            -0.5f, 0f, -0.5f, 0f, 1f, 0f,
             0.5f, 0f, -0.5f, 0f, 1f, 0f,
             0.5f, 0f,  0.5f, 0f, 1f, 0f,
            -0.5f, 0f,  0.5f, 0f, 1f, 0f
        ],
        [
            0u, 1u, 2u,
            0u, 2u, 3u
        ]);

        var edges = MeshEdgeIndexBuilder.BuildFeatureEdges(mesh);

        edges.Should().HaveCount(8);
        edges.Chunk(2).Should().NotContain(pair =>
            (pair[0] == 0u && pair[1] == 2u) ||
            (pair[0] == 2u && pair[1] == 0u));
    }

    [Fact]
    public void BuildFeatureEdges_ForBoxMesh_KeepsOnlyPhysicalEdges()
    {
        var mesh = MeshGenerator.GenerateMesh(BrushPrimitive.Box);

        var edges = MeshEdgeIndexBuilder.BuildFeatureEdges(mesh);

        edges.Should().HaveCount(24);
    }
}
