using FluentAssertions;
using MapEditor.Core.Entities;
using MapEditor.Core;
using MapEditor.Formats;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace MapEditor.Formats.Tests;

public sealed class MapSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesAllBrushes()
    {
        var scene = BuildTestScene();
        var dto   = MapSerializer.FromScene(scene);
        var roundTripped = MapSerializer.ToScene(dto);

        roundTripped.Brushes.Should().HaveCount(scene.Brushes.Count);
        roundTripped.Brushes[0].Name.Should().Be("Floor");
        roundTripped.Brushes[0].Operation.Should().Be(BrushOperation.Additive);
        roundTripped.Brushes[1].Operation.Should().Be(BrushOperation.Subtractive);
    }

    [Fact]
    public void RoundTrip_PreservesAllPrimitiveTypes()
    {
        var scene = new Scene();
        foreach (BrushPrimitive p in Enum.GetValues<BrushPrimitive>())
            scene.AddBrush(new Brush { Primitive = p });

        var roundTripped = MapSerializer.ToScene(MapSerializer.FromScene(scene));

        var primitives = roundTripped.Brushes.Select(b => b.Primitive).ToList();
        primitives.Should().Contain(BrushPrimitive.Box);
        primitives.Should().Contain(BrushPrimitive.Cylinder);
        primitives.Should().Contain(BrushPrimitive.Cone);
        primitives.Should().Contain(BrushPrimitive.Wedge);
    }

    [Fact]
    public void RoundTrip_PreservesTransform()
    {
        var scene = new Scene();
        var brush = new Brush
        {
            Transform = new Core.Entities.Transform
            {
                Position     = new Vector3(10, 20, 30),
                EulerDegrees = new Vector3(45, 90, 0),
                Scale        = new Vector3(2, 3, 4)
            }
        };
        scene.AddBrush(brush);

        var roundTripped = MapSerializer.ToScene(MapSerializer.FromScene(scene));

        roundTripped.Brushes[0].Transform.Position.Should().Be(new Vector3(10, 20, 30));
        roundTripped.Brushes[0].Transform.EulerDegrees.Should().Be(new Vector3(45, 90, 0));
        roundTripped.Brushes[0].Transform.Scale.Should().Be(new Vector3(2, 3, 4));
    }

    [Fact]
    public void RoundTrip_PreservesLights()
    {
        var scene = new Scene();
        scene.AddLight(new LightEntity
        {
            Name      = "SunLight",
            LightType = LightType.Spot,
            Intensity = 2.5f,
            Range     = 1000f,
            ConeAngle = 45f
        });

        var rt = MapSerializer.ToScene(MapSerializer.FromScene(scene));

        rt.Lights[0].Name.Should().Be("SunLight");
        rt.Lights[0].LightType.Should().Be(LightType.Spot);
        rt.Lights[0].ConeAngle.Should().Be(45f);
    }

    [Fact]
    public void RoundTrip_PreservesSpawnPoints()
    {
        var scene = new Scene();
        scene.AddSpawnPoint(new SpawnPoint { Name = "PlayerStart", SpawnType = "player" });

        var rt = MapSerializer.ToScene(MapSerializer.FromScene(scene));

        rt.SpawnPoints[0].SpawnType.Should().Be("player");
    }

    [Fact]
    public void RoundTrip_PreservesWorldSettings()
    {
        var scene = new Scene
        {
            WorldSettings =
            {
                AmbientColor = new Vector3(0.3f, 0.3f, 0.3f),
                SkyColor     = new Vector3(0.5f, 0.7f, 1.0f)
            }
        };

        var rt = MapSerializer.ToScene(MapSerializer.FromScene(scene));

        rt.WorldSettings.AmbientColor.X.Should().BeApproximately(0.3f, 0.001f);
        rt.WorldSettings.SkyColor.Z.Should().BeApproximately(1.0f, 0.001f);
    }

    private static Scene BuildTestScene()
    {
        var scene = new Scene();
        scene.AddBrush(new Brush { Name = "Floor", Operation = BrushOperation.Additive });
        scene.AddBrush(new Brush { Name = "Hole",  Operation = BrushOperation.Subtractive });
        scene.AddLight(new LightEntity { Name = "Lamp" });
        scene.AddSpawnPoint(new SpawnPoint { Name = "Start" });
        return scene;
    }
}

public sealed class MapFileServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsScene()
    {
        var svc = new MapFileService();
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.shmap");

        try
        {
            var scene = new Scene();
            scene.AddBrush(new Brush { Name = "TestBrush" });

            await svc.SaveAsync(scene, path);
            var loaded = await svc.LoadAsync(path);

            loaded.Brushes.Should().ContainSingle()
                .Which.Name.Should().Be("TestBrush");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Load_InvalidJson_ThrowsMapLoadException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bad_{Guid.NewGuid()}.shmap");
        await File.WriteAllTextAsync(path, "{ this is not valid json !!!}");

        try
        {
            var svc = new MapFileService();
            await svc.Invoking(s => s.LoadAsync(path))
                     .Should().ThrowAsync<MapLoadException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Load_UnknownFields_PreservedOnSave()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fwd_{Guid.NewGuid()}.shmap");

        // Write a map with an extra unknown top-level field
        var json = """
            {
              "format_version": "1.0.0",
              "editor_version": "0.1.0",
              "world_settings": {},
              "brushes": [],
              "lights": [],
              "spawn_points": [],
              "future_feature": "preserved"
            }
            """;

        await File.WriteAllTextAsync(path, json);

        try
        {
            var svc    = new MapFileService();
            var scene  = await svc.LoadAsync(path);
            var outPath = path + ".out.shmap";
            await svc.SaveAsync(scene, outPath);
            var savedJson = await File.ReadAllTextAsync(outPath);
            savedJson.Should().Contain("future_feature");
            File.Delete(outPath);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
