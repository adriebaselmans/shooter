using System.Text.Json;
using MapEditor.Core;
using MapEditor.Formats.Dto;

namespace MapEditor.Formats;

/// <summary>Exception thrown when a map file cannot be parsed.</summary>
public sealed class MapLoadException : Exception
{
    public MapLoadException(string message, Exception? inner = null)
        : base(message, inner) { }
}

/// <summary>Reads and writes <see cref="Scene"/> objects to .shmap (JSON) files.</summary>
public sealed class MapFileService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented       = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>Loads a scene from the file at <paramref name="path"/>.</summary>
    /// <exception cref="MapLoadException">Thrown when the file cannot be read or parsed.</exception>
    public async Task<Scene> LoadAsync(string path, CancellationToken ct = default)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var dto = await JsonSerializer.DeserializeAsync<MapDto>(stream, SerializerOptions, ct)
                      ?? throw new MapLoadException("Map file is empty or null.");
            ValidateVersion(dto.FormatVersion);
            return MapSerializer.ToScene(dto);
        }
        catch (MapLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new MapLoadException($"Failed to load map '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>Saves a scene to the file at <paramref name="path"/>.</summary>
    public async Task SaveAsync(Scene scene, string path, CancellationToken ct = default)
    {
        var dto  = MapSerializer.FromScene(scene);
        var json = JsonSerializer.Serialize(dto, SerializerOptions);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, json, System.Text.Encoding.UTF8, ct);
    }

    private static void ValidateVersion(string? versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString)) return;

        if (!Version.TryParse(versionString, out var version)) return;

        if (version.Major > 1)
            throw new MapLoadException(
                $"Map format version {versionString} is newer than this editor supports (max major: 1). " +
                "Please update the editor.");
    }
}
