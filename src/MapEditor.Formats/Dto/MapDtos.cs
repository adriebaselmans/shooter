using System.Text.Json.Serialization;

namespace MapEditor.Formats.Dto;

/// <summary>Root JSON DTO for a .shmap file.</summary>
public sealed class MapDto
{
    [JsonPropertyName("format_version")]
    public string FormatVersion { get; set; } = "1.3.0";

    [JsonPropertyName("editor_version")]
    public string EditorVersion { get; set; } = "0.1.0";

    [JsonPropertyName("world_settings")]
    public WorldSettingsDto WorldSettings { get; set; } = new();

    [JsonPropertyName("brushes")]
    public List<BrushDto> Brushes { get; set; } = [];

    [JsonPropertyName("lights")]
    public List<LightDto> Lights { get; set; } = [];

    [JsonPropertyName("spawn_points")]
    public List<SpawnPointDto> SpawnPoints { get; set; } = [];

    [JsonPropertyName("pickups")]
    public List<PickupDto> Pickups { get; set; } = [];

    /// <summary>Preserves unknown top-level fields on round-trip.</summary>
    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}

public sealed class WorldSettingsDto
{
    [JsonPropertyName("ambient_color")]
    public float[] AmbientColor { get; set; } = [0.1f, 0.1f, 0.1f];

    [JsonPropertyName("sky_color")]
    public float[] SkyColor { get; set; } = [0.2f, 0.3f, 0.4f];

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}

public sealed class TransformDto
{
    [JsonPropertyName("position")]
    public float[] Position { get; set; } = [0, 0, 0];

    [JsonPropertyName("rotation")]
    public float[] Rotation { get; set; } = [0, 0, 0];

    [JsonPropertyName("scale")]
    public float[] Scale { get; set; } = [1, 1, 1];

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}

public sealed class BrushDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Brush";

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "additive";

    [JsonPropertyName("primitive_type")]
    public string PrimitiveType { get; set; } = "box";

    [JsonPropertyName("transform")]
    public TransformDto Transform { get; set; } = new();

    [JsonPropertyName("material_name")]
    public string MaterialName { get; set; } = "default";

    [JsonPropertyName("surface_mappings")]
    public Dictionary<string, SurfaceMappingDto>? SurfaceMappings { get; set; }

    [JsonPropertyName("geometry")]
    public BrushGeometryDto? Geometry { get; set; }

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}

public sealed class BrushGeometryDto
{
    [JsonPropertyName("faces")]
    public List<BrushFaceDto> Faces { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}

public sealed class BrushFaceDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("vertices")]
    public float[][] Vertices { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}

public sealed class SurfaceMappingDto
{
    [JsonPropertyName("texture_key")]
    public string TextureKey { get; set; } = "default";

    [JsonPropertyName("offset")]
    public float[] Offset { get; set; } = [0f, 0f];

    [JsonPropertyName("scale")]
    public float[] Scale { get; set; } = [1f, 1f];

    [JsonPropertyName("rotation")]
    public float RotationDegrees { get; set; }

    [JsonPropertyName("texture_locked")]
    public bool TextureLocked { get; set; } = true;

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}

public sealed class LightDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Light";

    [JsonPropertyName("light_type")]
    public string LightType { get; set; } = "point";

    [JsonPropertyName("transform")]
    public TransformDto Transform { get; set; } = new();

    [JsonPropertyName("color")]
    public float[] Color { get; set; } = [1, 1, 1];

    [JsonPropertyName("intensity")]
    public float Intensity { get; set; } = 1.0f;

    [JsonPropertyName("range")]
    public float Range { get; set; } = 500.0f;

    [JsonPropertyName("cone_angle")]
    public float? ConeAngle { get; set; }

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}

public sealed class SpawnPointDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "SpawnPoint";

    [JsonPropertyName("transform")]
    public TransformDto Transform { get; set; } = new();

    [JsonPropertyName("spawn_type")]
    public string SpawnType { get; set; } = "player";

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}

public sealed class PickupDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Pickup";

    [JsonPropertyName("transform")]
    public TransformDto Transform { get; set; } = new();

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "HealthSmall";

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
}
