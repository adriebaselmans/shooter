using FluentAssertions;
using MapEditor.Core.Commands;
using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;
using MapEditor.Core;
using System.Numerics;

namespace MapEditor.Core.Tests;

public sealed class CommandHistoryTests
{
    [Fact]
    public void Execute_AddsCommandToUndoStack()
    {
        var svc = new SceneService();
        var brush = new Brush();
        svc.Execute(new CreateBrushCommand(svc.Scene, brush));

        svc.CanUndo.Should().BeTrue();
        svc.Scene.Brushes.Should().ContainSingle();
    }

    [Fact]
    public void Undo_ReversesBrushCreation()
    {
        var svc = new SceneService();
        var brush = new Brush();
        svc.Execute(new CreateBrushCommand(svc.Scene, brush));
        svc.Undo();

        svc.Scene.Brushes.Should().BeEmpty();
        svc.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void Redo_ReappliesBrushCreation()
    {
        var svc = new SceneService();
        var brush = new Brush();
        svc.Execute(new CreateBrushCommand(svc.Scene, brush));
        svc.Undo();
        svc.Redo();

        svc.Scene.Brushes.Should().ContainSingle();
    }

    [Fact]
    public void Execute_ClearsRedoStack()
    {
        var svc = new SceneService();
        var b1 = new Brush();
        var b2 = new Brush();
        svc.Execute(new CreateBrushCommand(svc.Scene, b1));
        svc.Undo();
        svc.Execute(new CreateBrushCommand(svc.Scene, b2));

        svc.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void HistoryCapAt50_OldestCommandDropped()
    {
        var svc = new SceneService();
        for (int i = 0; i < CommandHistory.Capacity + 5; i++)
        {
            svc.Execute(new CreateBrushCommand(svc.Scene, new Brush { Name = $"B{i}" }));
        }

        svc.Scene.Brushes.Should().HaveCount(CommandHistory.Capacity + 5);

        // Undo all — should only be able to undo Capacity times
        int undoCount = 0;
        while (svc.CanUndo) { svc.Undo(); undoCount++; }
        undoCount.Should().Be(CommandHistory.Capacity);
        svc.Scene.Brushes.Should().HaveCount(5);
        svc.Scene.Brushes.Select(b => b.Name).Should().ContainInOrder("B0", "B1", "B2", "B3", "B4");
    }

    [Fact]
    public void TransformBrushCommand_UndoRestoresPreviousTransform()
    {
        var svc = new SceneService();
        var brush = new Brush();
        svc.Execute(new CreateBrushCommand(svc.Scene, brush));

        var originalTransform = brush.Transform;
        var newTransform = originalTransform.WithPosition(new Vector3(100, 0, 0));
        svc.Execute(new TransformBrushCommand(svc.Scene, brush, newTransform));

        brush.Transform.Position.X.Should().Be(100);
        svc.Undo();
        brush.Transform.Should().Be(originalTransform);
    }

    [Fact]
    public void DeleteBrushCommand_UndoRestoresBrushAtOriginalIndex()
    {
        var svc = new SceneService();
        var b1 = new Brush { Name = "B1" };
        var b2 = new Brush { Name = "B2" };
        svc.Execute(new CreateBrushCommand(svc.Scene, b1));
        svc.Execute(new CreateBrushCommand(svc.Scene, b2));
        svc.Execute(new DeleteBrushCommand(svc.Scene, b1));

        svc.Scene.Brushes.Should().ContainSingle().Which.Name.Should().Be("B2");
        svc.Undo();
        svc.Scene.Brushes[0].Name.Should().Be("B1");
    }
}

public sealed class MeshGeneratorTests
{
    [Theory]
    [InlineData(BrushPrimitive.Box)]
    [InlineData(BrushPrimitive.Cylinder)]
    [InlineData(BrushPrimitive.Cone)]
    [InlineData(BrushPrimitive.Wedge)]
    public void GenerateMesh_ReturnsNonEmptyMesh(BrushPrimitive primitive)
    {
        var mesh = MeshGenerator.GenerateMesh(primitive);

        mesh.Vertices.Should().NotBeEmpty();
        mesh.Indices.Should().NotBeEmpty();
        mesh.VertexCount.Should().BePositive();
        mesh.TriangleCount.Should().BePositive();
    }

    [Fact]
    public void BoxMesh_Has24Vertices()
    {
        var mesh = MeshGenerator.GenerateMesh(BrushPrimitive.Box);
        mesh.VertexCount.Should().Be(24); // 6 faces × 4 vertices
    }

    [Fact]
    public void BoxMesh_Has12Triangles()
    {
        var mesh = MeshGenerator.GenerateMesh(BrushPrimitive.Box);
        mesh.TriangleCount.Should().Be(12); // 6 faces × 2 triangles
    }

    [Fact]
    public void AllMeshes_IndicesWithinVertexBounds()
    {
        foreach (BrushPrimitive p in Enum.GetValues<BrushPrimitive>())
        {
            var mesh = MeshGenerator.GenerateMesh(p);
            foreach (var idx in mesh.Indices)
                idx.Should().BeLessThan((uint)mesh.VertexCount,
                    $"{p} has an out-of-bounds index");
        }
    }

    [Fact]
    public void VertexDataStride_IsMultipleOf6()
    {
        foreach (BrushPrimitive p in Enum.GetValues<BrushPrimitive>())
        {
            var mesh = MeshGenerator.GenerateMesh(p);
            (mesh.Vertices.Length % 6).Should().Be(0, $"{p} vertex array length must be multiple of 6");
        }
    }
}

public sealed class SceneServiceTests
{
    [Fact]
    public void ReplaceScene_ClearsUndoHistory()
    {
        var svc = new SceneService();
        svc.Execute(new CreateBrushCommand(svc.Scene, new Brush()));

        var newScene = new Scene();
        svc.ReplaceScene(newScene);

        svc.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void SceneChanged_FiresOnBrushAdd()
    {
        var svc = new SceneService();
        int changeCount = 0;
        svc.SceneChanged += (_, _) => changeCount++;

        svc.Execute(new CreateBrushCommand(svc.Scene, new Brush()));

        changeCount.Should().Be(1);
    }
}
