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
    public void SetBrushOperationCommand_UndoRestoresPreviousOperation()
    {
        var svc = new SceneService();
        var brush = CreateBoxBrush();
        svc.Execute(new CreateBrushCommand(svc.Scene, brush));

        svc.Execute(new SetBrushOperationCommand(svc.Scene, brush, BrushOperation.Subtractive));
        brush.Operation.Should().Be(BrushOperation.Subtractive);

        svc.Undo();

        brush.Operation.Should().Be(BrushOperation.Additive);
    }

    [Fact]
    public void SubtractIntersectingBrushesCommand_ReplacesTargetsWithExplicitGeometryAndConsumesCutter()
    {
        var svc = new SceneService();
        var target = CreateBoxBrush(position: Vector3.Zero, scale: new Vector3(4f, 4f, 4f), name: "Target");
        var cutter = CreateBoxBrush(position: new Vector3(0.5f, 0f, 0f), scale: new Vector3(2f, 2f, 2f), name: "Cutter");
        cutter.Transform = cutter.Transform with { EulerDegrees = new Vector3(0f, 30f, 0f) };

        svc.Execute(new CreateBrushCommand(svc.Scene, target));
        svc.Execute(new CreateBrushCommand(svc.Scene, cutter));

        var command = new SubtractIntersectingBrushesCommand(svc.Scene, cutter);
        svc.Execute(command);

        svc.Scene.Brushes.Should().ContainSingle();
        svc.Scene.Brushes[0].Should().NotBeSameAs(target);
        svc.Scene.Brushes[0].HasExplicitGeometry.Should().BeTrue();
        command.ReplacementBrushes.Should().ContainSingle();

        svc.Undo();

        svc.Scene.Brushes.Should().ContainInOrder(target, cutter);
    }

    [Fact]
    public void SubtractIntersectingBrushesCommand_AssignsTargetBaselineToCreatedFaces()
    {
        var svc = new SceneService();
        var target = CreateBoxBrush(position: Vector3.Zero, scale: new Vector3(4f, 4f, 4f), name: "Target");
        target.MaterialName = "stone";
        target.SetSurfaceMapping(BrushSurfaceIds.Front, new SurfaceMapping("brick", Vector2.Zero, Vector2.One, 0f, true));
        var cutter = CreateBoxBrush(position: Vector3.Zero, scale: new Vector3(2f, 2f, 2f), name: "Cutter");
        cutter.MaterialName = "metal";

        svc.Execute(new CreateBrushCommand(svc.Scene, target));
        svc.Execute(new CreateBrushCommand(svc.Scene, cutter));

        svc.Execute(new SubtractIntersectingBrushesCommand(svc.Scene, cutter));

        svc.Scene.Brushes.Should().ContainSingle();
        var result = svc.Scene.Brushes[0];
        result.SurfaceMappings.Values.Should().NotContain(mapping => mapping.TextureKey == "metal");
        result.SurfaceMappings.Values.Should().Contain(mapping => mapping.TextureKey == "stone");
    }

    [Fact]
    public void MergeSelectedBrushesCommand_CreatesExplicitMergedBrushAndUndoRestoresSources()
    {
        var svc = new SceneService();
        var first = CreateBoxBrush(position: new Vector3(-1f, 0f, 0f), scale: new Vector3(2f, 2f, 2f), name: "Left");
        var second = CreateBoxBrush(position: new Vector3(1f, 0f, 0f), scale: new Vector3(2f, 2f, 2f), name: "Right");
        second.MaterialName = "stone";

        svc.Execute(new CreateBrushCommand(svc.Scene, first));
        svc.Execute(new CreateBrushCommand(svc.Scene, second));

        var command = new MergeSelectedBrushesCommand(svc.Scene, [first, second]);
        svc.Execute(command);

        svc.Scene.Brushes.Should().ContainSingle();
        command.MergedBrush.HasExplicitGeometry.Should().BeTrue();
        command.MergedBrush.MaterialName.Should().Be(first.MaterialName);
        command.MergedBrush.SurfaceMappings.Values.Should().Contain(mapping => mapping.TextureKey == "stone");

        svc.Undo();

        svc.Scene.Brushes.Should().ContainInOrder(first, second);
    }

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

    [Fact]
    public void ApplyBrushTextureCommand_UndoRestoresPreviousBrushTextureAndSurfaceOverrides()
    {
        var svc = new SceneService();
        var brush = new Brush { MaterialName = "stone.png" };
        brush.SetSurfaceMapping(BrushSurfaceIds.Top, SurfaceMapping.Default("metal.png"));
        svc.Execute(new CreateBrushCommand(svc.Scene, brush));

        svc.Execute(new ApplyBrushTextureCommand(svc.Scene, brush, "brick.png"));

        brush.MaterialName.Should().Be("brick.png");
        brush.SurfaceMappings.Should().BeEmpty();

        svc.Undo();

        brush.MaterialName.Should().Be("stone.png");
        brush.GetEffectiveSurfaceMapping(BrushSurfaceIds.Top).TextureKey.Should().Be("metal.png");
    }

    [Fact]
    public void UpdateSurfaceMappingCommand_UndoRestoresPreviousSurfaceValues()
    {
        var svc = new SceneService();
        var brush = new Brush();
        svc.Execute(new CreateBrushCommand(svc.Scene, brush));

        var updatedMapping = new Dictionary<string, SurfaceMapping>
        {
            [BrushSurfaceIds.Top] = new("brick.png", new Vector2(2f, 4f), new Vector2(0.5f, 0.5f), 90f, true)
        };

        svc.Execute(new UpdateSurfaceMappingCommand(svc.Scene, brush, updatedMapping));
        brush.GetEffectiveSurfaceMapping(BrushSurfaceIds.Top).TextureKey.Should().Be("brick.png");

        svc.Undo();
        brush.SurfaceMappings.Should().BeEmpty();
        brush.GetEffectiveSurfaceMapping(BrushSurfaceIds.Top).TextureKey.Should().Be(brush.MaterialName);
    }

    private static Brush CreateBoxBrush(
        Vector3? position = null,
        Vector3? scale = null,
        string name = "Brush")
    {
        return new Brush
        {
            Name = name,
            Primitive = BrushPrimitive.Box,
            Operation = BrushOperation.Additive,
            Transform = new Transform
            {
                Position = position ?? Vector3.Zero,
                EulerDegrees = Vector3.Zero,
                Scale = scale ?? new Vector3(2f, 2f, 2f)
            }
        };
    }
}

public sealed class MeshGeneratorTests
{
    [Fact]
    public void GenerateMesh_ReturnsSurfaceRangesForExplicitGeometry()
    {
        var brush = new Brush
        {
            Primitive = BrushPrimitive.Box,
            Transform = Transform.Identity
        };
        brush.SetGeometry(new BrushGeometry(
        [
            new BrushFace("face-a", [new(-1f, -1f, 0f), new(1f, -1f, 0f), new(1f, 1f, 0f), new(-1f, 1f, 0f)]),
            new BrushFace("face-b", [new(-1f, -1f, 1f), new(-1f, 1f, 1f), new(1f, 1f, 1f), new(1f, -1f, 1f)])
        ]));

        var mesh = MeshGenerator.GenerateMesh(brush);

        mesh.Surfaces.Select(surface => surface.SurfaceId).Should().ContainInOrder("face-a", "face-b");
    }

    [Fact]
    public void GenerateMesh_UsesStableUvAxesForExplicitWallFragments()
    {
        var brush = new Brush
        {
            Primitive = BrushPrimitive.Box,
            Transform = Transform.Identity
        };
        brush.SetGeometry(new BrushGeometry(
        [
            new BrushFace("fragment", [new(-1f, 1f, 0f), new(-1f, -1f, 0f), new(1f, -1f, 0f), new(1f, 1f, 0f)])
        ]));

        var mesh = MeshGenerator.GenerateMesh(brush);

        var topLeft = GetUv(mesh, 0);
        var bottomLeft = GetUv(mesh, 1);
        var bottomRight = GetUv(mesh, 2);
        var topRight = GetUv(mesh, 3);

        bottomLeft.X.Should().BeApproximately(topLeft.X, 0.0001f);
        bottomRight.X.Should().BeApproximately(topRight.X, 0.0001f);
        bottomLeft.Y.Should().BeApproximately(bottomRight.Y, 0.0001f);
        topLeft.Y.Should().BeApproximately(topRight.Y, 0.0001f);
        bottomLeft.X.Should().BeLessThan(bottomRight.X);
        bottomLeft.Y.Should().BeLessThan(topLeft.Y);
    }

    [Fact]
    public void BrushBounds_UsesExplicitGeometryWorldExtents()
    {
        var brush = new Brush
        {
            Primitive = BrushPrimitive.Box,
            Transform = new Transform
            {
                Position = new Vector3(10f, 5f, -2f),
                EulerDegrees = Vector3.Zero,
                Scale = Vector3.One
            }
        };
        brush.SetGeometry(new BrushGeometry(
        [
            new BrushFace("face-a", [new(-4f, -2f, -1f), new(4f, -2f, -1f), new(4f, 2f, -1f), new(-4f, 2f, -1f)]),
            new BrushFace("face-b", [new(-4f, -2f, 3f), new(-4f, 2f, 3f), new(4f, 2f, 3f), new(4f, -2f, 3f)])
        ]));

        BrushBounds.TryGetWorldBounds(brush, out var min, out var max).Should().BeTrue();

        min.Should().Be(new Vector3(6f, 3f, -3f));
        max.Should().Be(new Vector3(14f, 7f, 1f));
    }

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
    public void VertexDataStride_IsMultipleOf8()
    {
        foreach (BrushPrimitive p in Enum.GetValues<BrushPrimitive>())
        {
            var mesh = MeshGenerator.GenerateMesh(p);
            (mesh.Vertices.Length % Mesh.FloatsPerVertex).Should().Be(0, $"{p} vertex array length must be multiple of {Mesh.FloatsPerVertex}");
        }
    }

    [Fact]
    public void BoxMesh_ExposesSixLogicalSurfaces()
    {
        var mesh = MeshGenerator.GenerateMesh(BrushPrimitive.Box);

        mesh.Surfaces.Select(surface => surface.SurfaceId)
            .Should()
            .ContainInOrder(BrushSurfaceIds.Top, BrushSurfaceIds.Bottom, BrushSurfaceIds.Front, BrushSurfaceIds.Back, BrushSurfaceIds.Right, BrushSurfaceIds.Left);
    }

    private static Vector2 GetUv(Mesh mesh, int vertexIndex)
    {
        int offset = vertexIndex * Mesh.FloatsPerVertex;
        return new Vector2(mesh.Vertices[offset + 6], mesh.Vertices[offset + 7]);
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

public sealed class SubtractCutterFaceTests
{
    [Fact]
    public void SubtractIntersectingBrushesCommand_CutterIdNotInSceneAfterExecute()
    {
        var svc = new SceneService();
        var target = CreateBoxBrush(position: Vector3.Zero, scale: new Vector3(4f, 4f, 4f), name: "Target");
        var cutter = CreateBoxBrush(position: Vector3.Zero, scale: new Vector3(2f, 2f, 2f), name: "Cutter");

        svc.Execute(new CreateBrushCommand(svc.Scene, target));
        svc.Execute(new CreateBrushCommand(svc.Scene, cutter));

        var cutterId = cutter.Id;
        svc.Execute(new SubtractIntersectingBrushesCommand(svc.Scene, cutter));

        svc.Scene.Brushes.Should().NotContain(b => b.Id == cutterId);
    }

    [Fact]
    public void SubtractResult_ContainsCutterFaces()
    {
        var svc = new SceneService();
        var target = CreateBoxBrush(position: Vector3.Zero, scale: new Vector3(4f, 4f, 4f), name: "Target");
        var cutter = CreateBoxBrush(position: Vector3.Zero, scale: new Vector3(2f, 2f, 2f), name: "Cutter");

        svc.Execute(new CreateBrushCommand(svc.Scene, target));
        svc.Execute(new CreateBrushCommand(svc.Scene, cutter));

        var command = new SubtractIntersectingBrushesCommand(svc.Scene, cutter);
        svc.Execute(command);

        var replacement = svc.Scene.Brushes.Should().ContainSingle().Which;
        replacement.HasExplicitGeometry.Should().BeTrue();

        var geometry = BrushGeometryFactory.GetGeometry(replacement);
        geometry.Faces.Should().Contain(f => f.IsCutterFace, "the result should contain faces from the cutter");
        geometry.Faces.Should().Contain(f => !f.IsCutterFace, "the result should also contain faces from the target");
    }

    [Fact]
    public void SubtractPegThroughBoard_ResultVerticesStayWithinBoardBounds()
    {
        // Board: wide and thin — extends from (-2, -0.25, -2) to (2, 0.25, 2)
        var board = CreateBoxBrush(position: Vector3.Zero, scale: new Vector3(4f, 0.5f, 4f), name: "Board");
        // Peg: tall and thin — extends from (-0.5, -2, -0.5) to (0.5, 2, 0.5)
        var peg = CreateBoxBrush(position: Vector3.Zero, scale: new Vector3(1f, 4f, 1f), name: "Peg");

        var kernel = new BspBrushBooleanKernel();
        var result = kernel.Subtract(board, peg);
        result.Should().NotBeNull("subtracting an intersecting peg from a board should produce geometry");

        const float boardHalfY = 0.25f;
        const float epsilon = 0.001f;
        var center = result!.Transform.Position;

        foreach (var face in result.Geometry.Faces)
        {
            foreach (var localVertex in face.Vertices)
            {
                var worldY = localVertex.Y + center.Y;
                worldY.Should().BeGreaterThanOrEqualTo(-boardHalfY - epsilon,
                    $"vertex at world Y={worldY:F4} is below the board (local={localVertex}, center={center})");
                worldY.Should().BeLessThanOrEqualTo(boardHalfY + epsilon,
                    $"vertex at world Y={worldY:F4} is above the board (local={localVertex}, center={center})");
            }
        }
    }

    private static Brush CreateBoxBrush(
        Vector3? position = null,
        Vector3? scale = null,
        string name = "Brush")
    {
        return new Brush
        {
            Name = name,
            Primitive = BrushPrimitive.Box,
            Operation = BrushOperation.Additive,
            Transform = new Transform
            {
                Position = position ?? Vector3.Zero,
                EulerDegrees = Vector3.Zero,
                Scale = scale ?? new Vector3(2f, 2f, 2f)
            }
        };
    }
}
