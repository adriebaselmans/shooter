using FluentAssertions;
using MapEditor.Core;
using MapEditor.Core.Commands;
using MapEditor.Core.Entities;
using MapEditor.Rendering.Cameras;
using MapEditor.Rendering.Renderers;
using System.Numerics;

namespace MapEditor.Rendering.Tests;

[Collection(GlRenderCollectionDefinition.Name)]
public sealed class ViewportRenderingAcceptanceTests
{
    private const int CaptureSize = 256;

    [Fact]
    public void OrthographicViewport_Render_ShowsVisibleGridAndAxisLines()
    {
        using var surface = new ViewportRenderTestSurface(CaptureSize, CaptureSize);
        using var renderer = new OrthographicViewportRenderer(surface.Gl, ViewAxis.Top);
        renderer.Camera.Zoom = 64f;

        var snapshot = surface.Render(() => renderer.Render(new Scene(), surface.Width, surface.Height));

        snapshot.CountWhere(IsNotNear(38, 4)).Should().BeGreaterThan(1_500);
        snapshot.CountWhere(IsDominantRed(20)).Should().BeGreaterThan(200);
        snapshot.CountWhere(IsDominantBlue(20)).Should().BeGreaterThan(200);
    }

    [Fact]
    public void FrontViewport_Render_ShowsVisibleRedAndGreenAxes()
    {
        using var surface = new ViewportRenderTestSurface(CaptureSize, CaptureSize);
        using var renderer = new OrthographicViewportRenderer(surface.Gl, ViewAxis.Front);
        renderer.Camera.Zoom = 64f;

        var snapshot = surface.Render(() => renderer.Render(new Scene(), surface.Width, surface.Height));

        snapshot.CountWhere(IsNotNear(38, 4)).Should().BeGreaterThan(1_500);
        snapshot.CountWhere(IsDominantRed(20)).Should().BeGreaterThan(200);
        snapshot.CountWhere(IsDominantGreen(20)).Should().BeGreaterThan(200);
    }

    [Fact]
    public void SideViewport_Render_ShowsVisibleBlueAndGreenAxes()
    {
        using var surface = new ViewportRenderTestSurface(CaptureSize, CaptureSize);
        using var renderer = new OrthographicViewportRenderer(surface.Gl, ViewAxis.Side);
        renderer.Camera.Zoom = 64f;

        var snapshot = surface.Render(() => renderer.Render(new Scene(), surface.Width, surface.Height));

        snapshot.CountWhere(IsNotNear(38, 4)).Should().BeGreaterThan(1_500);
        snapshot.CountWhere(IsDominantBlue(20)).Should().BeGreaterThan(200);
        snapshot.CountWhere(IsDominantGreen(20)).Should().BeGreaterThan(200);
    }

    [Fact]
    public void PerspectiveViewport_Render_ShowsVisibleFloorGuide()
    {
        using var surface = new ViewportRenderTestSurface(CaptureSize, CaptureSize);
        using var renderer = new PerspectiveViewportRenderer(surface.Gl);
        ConfigurePerspectiveCamera(renderer.Camera);

        var snapshot = surface.Render(() => renderer.Render(new Scene(), surface.Width, surface.Height));

        snapshot.CountWhere(IsNotNear(31, 4)).Should().BeGreaterThan(400);
        snapshot.CountWhere(IsDominantRed(18)).Should().BeGreaterThan(25);
        snapshot.CountWhere(IsDominantGreen(18)).Should().BeGreaterThan(25);
        snapshot.CountWhere(IsDominantBlue(18)).Should().BeGreaterThan(25);
    }

    [Fact]
    public void PerspectiveViewport_Render_PreservesBrushTintInLowLightScenes()
    {
        using var surface = new ViewportRenderTestSurface(CaptureSize, CaptureSize);
        using var renderer = new PerspectiveViewportRenderer(surface.Gl);
        ConfigurePerspectiveCamera(renderer.Camera);

        var sceneService = new SceneService();
        var emptySnapshot = surface.Render(() => renderer.Render(sceneService.Scene, surface.Width, surface.Height));

        sceneService.Execute(new CreateBrushCommand(
            sceneService.Scene,
            new Brush
            {
                Name = "Tinted Box",
                Primitive = BrushPrimitive.Box,
                Operation = BrushOperation.Additive,
                Transform = new Transform
                {
                    Position = new Vector3(24f, 48f, 40f),
                    EulerDegrees = Vector3.Zero,
                    Scale = new Vector3(176f, 96f, 144f)
                }
            }));

        var tintedSnapshot = surface.Render(() => renderer.Render(sceneService.Scene, surface.Width, surface.Height));

        tintedSnapshot.CountDifferentPixels(emptySnapshot).Should().BeGreaterThan(250);
        tintedSnapshot.CountWhere(IsDominantBlue(14))
            .Should().BeGreaterThan(emptySnapshot.CountWhere(IsDominantBlue(14)) + 150);
    }

    [Fact]
    public void SceneCreationAndTransformUpdates_AreReflectedAcrossAllViewports()
    {
        using var surface = new ViewportRenderTestSurface(CaptureSize, CaptureSize);
        using var topRenderer = new OrthographicViewportRenderer(surface.Gl, ViewAxis.Top);
        using var frontRenderer = new OrthographicViewportRenderer(surface.Gl, ViewAxis.Front);
        using var sideRenderer = new OrthographicViewportRenderer(surface.Gl, ViewAxis.Side);
        using var perspectiveRenderer = new PerspectiveViewportRenderer(surface.Gl);

        topRenderer.CaptureDiagnostics = true;
        frontRenderer.CaptureDiagnostics = true;
        sideRenderer.CaptureDiagnostics = true;
        perspectiveRenderer.CaptureDiagnostics = true;
        ConfigureOrthographicCamera(topRenderer.Camera, 128f);
        ConfigureOrthographicCamera(frontRenderer.Camera, 128f);
        ConfigureOrthographicCamera(sideRenderer.Camera, 128f);
        ConfigurePerspectiveCamera(perspectiveRenderer.Camera);

        var sceneService = new SceneService();
        sceneService.Scene.WorldSettings.AmbientColor = new Vector3(0.7f, 0.7f, 0.7f);
        sceneService.Execute(new CreateLightCommand(
            sceneService.Scene,
            new LightEntity
            {
                Transform = new Transform
                {
                    Position = new Vector3(240f, 320f, -180f),
                    EulerDegrees = Vector3.Zero,
                    Scale = Vector3.One
                },
                Intensity = 1.2f,
                Range = 2_500f
            }));

        var emptySnapshots = RenderAllViewports(sceneService.Scene, topRenderer, frontRenderer, sideRenderer, perspectiveRenderer, surface);

        var createdBrush = new Brush
        {
            Name = "Acceptance Box",
            Primitive = BrushPrimitive.Box,
            Operation = BrushOperation.Additive,
            Transform = new Transform
            {
                Position = new Vector3(24f, 48f, 40f),
                EulerDegrees = Vector3.Zero,
                Scale = new Vector3(176f, 96f, 144f)
            }
        };
        sceneService.Execute(new CreateBrushCommand(sceneService.Scene, createdBrush));
        sceneService.Scene.Brushes.Should().ContainSingle();

        var createdSnapshots = RenderAllViewports(sceneService.Scene, topRenderer, frontRenderer, sideRenderer, perspectiveRenderer, surface);

        var topCreatedDiagnostic = topRenderer.LastDiagnostics.Brushes.Should().ContainSingle().Which;
        var frontCreatedDiagnostic = frontRenderer.LastDiagnostics.Brushes.Should().ContainSingle().Which;
        var sideCreatedDiagnostic = sideRenderer.LastDiagnostics.Brushes.Should().ContainSingle().Which;
        var perspectiveCreatedDiagnostic = perspectiveRenderer.LastDiagnostics.Brushes.Should().ContainSingle().Which;

        AssertOrthographicBrushVisibility(topCreatedDiagnostic, createdBrush.Id);
        AssertOrthographicBrushVisibility(frontCreatedDiagnostic, createdBrush.Id);
        AssertOrthographicBrushVisibility(sideCreatedDiagnostic, createdBrush.Id);
        AssertPerspectiveBrushVisibility(perspectiveCreatedDiagnostic, createdBrush.Id);
        createdSnapshots.Top.CountDifferentPixels(emptySnapshots.Top).Should().BeGreaterThan(250);
        createdSnapshots.Front.CountDifferentPixels(emptySnapshots.Front).Should().BeGreaterThan(150);
        createdSnapshots.Side.CountDifferentPixels(emptySnapshots.Side).Should().BeGreaterThan(150);
        createdSnapshots.Perspective.CountDifferentPixels(emptySnapshots.Perspective).Should().BeGreaterThan(250);

        topCreatedDiagnostic.BrushId.Should().Be(createdBrush.Id);
        frontCreatedDiagnostic.BrushId.Should().Be(createdBrush.Id);
        sideCreatedDiagnostic.BrushId.Should().Be(createdBrush.Id);
        perspectiveCreatedDiagnostic.BrushId.Should().Be(createdBrush.Id);
        topCreatedDiagnostic.ModelMatrix.Should().NotBe(Matrix4x4.Identity);
        frontCreatedDiagnostic.ModelMatrix.Should().NotBe(Matrix4x4.Identity);
        sideCreatedDiagnostic.ModelMatrix.Should().NotBe(Matrix4x4.Identity);
        perspectiveCreatedDiagnostic.ModelMatrix.Should().NotBe(Matrix4x4.Identity);

        sceneService.Execute(new TransformBrushCommand(
            sceneService.Scene,
            createdBrush,
            new Transform
            {
                Position = new Vector3(72f, 84f, 52f),
                EulerDegrees = Vector3.Zero,
                Scale = new Vector3(136f, 104f, 120f)
            }));

        var movedSnapshots = RenderAllViewports(sceneService.Scene, topRenderer, frontRenderer, sideRenderer, perspectiveRenderer, surface);

        var topMovedDiagnostic = topRenderer.LastDiagnostics.Brushes.Should().ContainSingle().Which;
        var frontMovedDiagnostic = frontRenderer.LastDiagnostics.Brushes.Should().ContainSingle().Which;
        var sideMovedDiagnostic = sideRenderer.LastDiagnostics.Brushes.Should().ContainSingle().Which;
        var perspectiveMovedDiagnostic = perspectiveRenderer.LastDiagnostics.Brushes.Should().ContainSingle().Which;

        AssertOrthographicBrushVisibility(topMovedDiagnostic, createdBrush.Id);
        AssertOrthographicBrushVisibility(frontMovedDiagnostic, createdBrush.Id);
        AssertOrthographicBrushVisibility(sideMovedDiagnostic, createdBrush.Id);
        AssertPerspectiveBrushVisibility(perspectiveMovedDiagnostic, createdBrush.Id);
        movedSnapshots.Top.CountDifferentPixels(createdSnapshots.Top).Should().BeGreaterThan(250);
        movedSnapshots.Front.CountDifferentPixels(createdSnapshots.Front).Should().BeGreaterThan(150);
        movedSnapshots.Side.CountDifferentPixels(createdSnapshots.Side).Should().BeGreaterThan(150);
        movedSnapshots.Perspective.CountDifferentPixels(createdSnapshots.Perspective).Should().BeGreaterThan(250);

        topMovedDiagnostic.ModelMatrix.Should().NotBe(topCreatedDiagnostic.ModelMatrix);
        frontMovedDiagnostic.ModelMatrix.Should().NotBe(frontCreatedDiagnostic.ModelMatrix);
        sideMovedDiagnostic.ModelMatrix.Should().NotBe(sideCreatedDiagnostic.ModelMatrix);
        perspectiveMovedDiagnostic.ModelMatrix.Should().NotBe(perspectiveCreatedDiagnostic.ModelMatrix);

        sceneService.Execute(new DeleteBrushCommand(sceneService.Scene, createdBrush));

        var deletedSnapshots = RenderAllViewports(sceneService.Scene, topRenderer, frontRenderer, sideRenderer, perspectiveRenderer, surface);

        topRenderer.LastDiagnostics.Brushes.Should().BeEmpty();
        frontRenderer.LastDiagnostics.Brushes.Should().BeEmpty();
        sideRenderer.LastDiagnostics.Brushes.Should().BeEmpty();
        perspectiveRenderer.LastDiagnostics.Brushes.Should().BeEmpty();
        deletedSnapshots.Top.CountDifferentPixels(movedSnapshots.Top).Should().BeGreaterThan(250);
        deletedSnapshots.Front.CountDifferentPixels(movedSnapshots.Front).Should().BeGreaterThan(150);
        deletedSnapshots.Side.CountDifferentPixels(movedSnapshots.Side).Should().BeGreaterThan(150);
        deletedSnapshots.Perspective.CountDifferentPixels(movedSnapshots.Perspective).Should().BeGreaterThan(250);
    }

    [Fact]
    public void SubtractIntersectingBrushes_CutterDoesNotAppearInRenderDiagnostics()
    {
        using var surface = new ViewportRenderTestSurface(CaptureSize, CaptureSize);
        using var topRenderer = new OrthographicViewportRenderer(surface.Gl, ViewAxis.Top);
        using var frontRenderer = new OrthographicViewportRenderer(surface.Gl, ViewAxis.Front);
        using var sideRenderer = new OrthographicViewportRenderer(surface.Gl, ViewAxis.Side);
        using var perspectiveRenderer = new PerspectiveViewportRenderer(surface.Gl);

        topRenderer.CaptureDiagnostics = true;
        frontRenderer.CaptureDiagnostics = true;
        sideRenderer.CaptureDiagnostics = true;
        perspectiveRenderer.CaptureDiagnostics = true;
        ConfigureOrthographicCamera(topRenderer.Camera, 128f);
        ConfigureOrthographicCamera(frontRenderer.Camera, 128f);
        ConfigureOrthographicCamera(sideRenderer.Camera, 128f);
        ConfigurePerspectiveCamera(perspectiveRenderer.Camera);

        var sceneService = new SceneService();
        sceneService.Scene.WorldSettings.AmbientColor = new Vector3(0.7f, 0.7f, 0.7f);
        sceneService.Execute(new CreateLightCommand(
            sceneService.Scene,
            new LightEntity
            {
                Transform = new Transform
                {
                    Position = new Vector3(240f, 320f, -180f),
                    EulerDegrees = Vector3.Zero,
                    Scale = Vector3.One
                },
                Intensity = 1.2f,
                Range = 2_500f
            }));

        var target = new Brush
        {
            Name = "Target",
            Primitive = BrushPrimitive.Box,
            Operation = BrushOperation.Additive,
            Transform = new Transform
            {
                Position = new Vector3(24f, 48f, 40f),
                EulerDegrees = Vector3.Zero,
                Scale = new Vector3(176f, 96f, 144f)
            }
        };
        var cutter = new Brush
        {
            Name = "Cutter",
            Primitive = BrushPrimitive.Box,
            Operation = BrushOperation.Additive,
            Transform = new Transform
            {
                Position = new Vector3(24f, 48f, 40f),
                EulerDegrees = Vector3.Zero,
                Scale = new Vector3(80f, 48f, 64f)
            }
        };

        sceneService.Execute(new CreateBrushCommand(sceneService.Scene, target));
        sceneService.Execute(new CreateBrushCommand(sceneService.Scene, cutter));

        // Pre-subtract: both brush IDs should appear in diagnostics
        var preSnapshots = RenderAllViewports(sceneService.Scene, topRenderer, frontRenderer, sideRenderer, perspectiveRenderer, surface);

        topRenderer.LastDiagnostics.Brushes.Should().HaveCount(2);
        frontRenderer.LastDiagnostics.Brushes.Should().HaveCount(2);
        sideRenderer.LastDiagnostics.Brushes.Should().HaveCount(2);
        perspectiveRenderer.LastDiagnostics.Brushes.Should().HaveCount(2);

        topRenderer.LastDiagnostics.Brushes.Should().Contain(d => d.BrushId == target.Id);
        topRenderer.LastDiagnostics.Brushes.Should().Contain(d => d.BrushId == cutter.Id);

        // Execute subtract
        var cutterId = cutter.Id;
        sceneService.Execute(new SubtractIntersectingBrushesCommand(sceneService.Scene, cutter));

        // Post-subtract: cutter ID must NOT appear in any viewport diagnostics
        var postSnapshots = RenderAllViewports(sceneService.Scene, topRenderer, frontRenderer, sideRenderer, perspectiveRenderer, surface);

        topRenderer.LastDiagnostics.Brushes.Should().NotContain(d => d.BrushId == cutterId);
        frontRenderer.LastDiagnostics.Brushes.Should().NotContain(d => d.BrushId == cutterId);
        sideRenderer.LastDiagnostics.Brushes.Should().NotContain(d => d.BrushId == cutterId);
        perspectiveRenderer.LastDiagnostics.Brushes.Should().NotContain(d => d.BrushId == cutterId);

        // Pixel snapshots must differ from pre-subtract (visual change occurred)
        postSnapshots.Top.CountDifferentPixels(preSnapshots.Top).Should().BeGreaterThan(0);
        postSnapshots.Front.CountDifferentPixels(preSnapshots.Front).Should().BeGreaterThan(0);
        postSnapshots.Side.CountDifferentPixels(preSnapshots.Side).Should().BeGreaterThan(0);
        postSnapshots.Perspective.CountDifferentPixels(preSnapshots.Perspective).Should().BeGreaterThan(0);
    }

    private static ViewportSnapshots RenderAllViewports(
        Scene scene,
        OrthographicViewportRenderer topRenderer,
        OrthographicViewportRenderer frontRenderer,
        OrthographicViewportRenderer sideRenderer,
        PerspectiveViewportRenderer perspectiveRenderer,
        ViewportRenderTestSurface surface)
    {
        return new ViewportSnapshots(
            surface.Render(() => topRenderer.Render(scene, surface.Width, surface.Height)),
            surface.Render(() => frontRenderer.Render(scene, surface.Width, surface.Height)),
            surface.Render(() => sideRenderer.Render(scene, surface.Width, surface.Height)),
            surface.Render(() => perspectiveRenderer.Render(scene, surface.Width, surface.Height)));
    }

    private static void ConfigureOrthographicCamera(OrthographicCamera camera, float zoom)
    {
        camera.Zoom = zoom;
        camera.Pan = 0f;
        camera.PanY = 0f;
    }

    private static void ConfigurePerspectiveCamera(PerspectiveCamera camera)
    {
        camera.Target = new Vector3(32f, 32f, 32f);
        camera.Distance = 650f;
        camera.Yaw = -45f;
        camera.Pitch = 30f;
    }

    private static void AssertBrushDiagnosticRecorded(BrushRenderDiagnostic diagnostic, Guid expectedBrushId)
    {
        diagnostic.BrushId.Should().Be(expectedBrushId);
        diagnostic.ModelMatrix.Should().NotBe(Matrix4x4.Identity);
    }

    private static void AssertOrthographicBrushVisibility(BrushRenderDiagnostic diagnostic, Guid expectedBrushId)
    {
        AssertBrushDiagnosticRecorded(diagnostic, expectedBrushId);
        diagnostic.SolidSamplesPassed.Should().BeTrue();
        diagnostic.WireSamplesPassed.Should().BeTrue();
    }

    private static void AssertPerspectiveBrushVisibility(BrushRenderDiagnostic diagnostic, Guid expectedBrushId)
    {
        AssertBrushDiagnosticRecorded(diagnostic, expectedBrushId);
        diagnostic.SolidSamplesPassed.Should().BeTrue();
    }

    private readonly record struct ViewportSnapshots(
        PixelSnapshot Top,
        PixelSnapshot Front,
        PixelSnapshot Side,
        PixelSnapshot Perspective);

    private static Func<byte, byte, byte, byte, bool> IsNotNear(byte value, byte tolerance) =>
        (r, g, b, _) =>
            Math.Abs(r - value) > tolerance ||
            Math.Abs(g - value) > tolerance ||
            Math.Abs(b - value) > tolerance;

    private static Func<byte, byte, byte, byte, bool> IsDominantRed(byte margin) =>
        (r, g, b, _) => r > g + margin && r > b + margin;

    private static Func<byte, byte, byte, byte, bool> IsDominantBlue(byte margin) =>
        (r, g, b, _) => b > r + margin && b > g + margin;

    private static Func<byte, byte, byte, byte, bool> IsDominantGreen(byte margin) =>
        (r, g, b, _) => g > r + margin && g > b + margin;
}
