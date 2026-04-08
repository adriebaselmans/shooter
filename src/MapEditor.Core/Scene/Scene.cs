using System.Text.Json;
using MapEditor.Core.Entities;

namespace MapEditor.Core;

/// <summary>Arguments for the <see cref="Scene.SceneChanged"/> event.</summary>
public sealed class SceneChangedEventArgs : EventArgs
{
    public static readonly SceneChangedEventArgs Default = new();
}

/// <summary>
/// Root scene graph. Holds all entities and world settings.
/// Raise <see cref="SceneChanged"/> to notify observers of any mutation.
/// All mutations must go through <see cref="SceneService"/> to maintain undo/redo integrity.
/// </summary>
public sealed class Scene
{
    private readonly List<Brush> _brushes = [];
    private readonly List<LightEntity> _lights = [];
    private readonly List<SpawnPoint> _spawnPoints = [];

    public WorldSettings WorldSettings { get; set; } = new();
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    public IReadOnlyList<Brush> Brushes => _brushes;
    public IReadOnlyList<LightEntity> Lights => _lights;
    public IReadOnlyList<SpawnPoint> SpawnPoints => _spawnPoints;

    /// <summary>Fired whenever the scene content changes.</summary>
    public event EventHandler<SceneChangedEventArgs>? SceneChanged;

    internal void AddBrush(Brush brush)
    {
        _brushes.Add(brush);
        RaiseChanged();
    }

    internal void InsertBrush(int index, Brush brush)
    {
        _brushes.Insert(index, brush);
        RaiseChanged();
    }

    internal void RemoveBrush(Brush brush)
    {
        _brushes.Remove(brush);
        RaiseChanged();
    }

    internal void AddLight(LightEntity light)
    {
        _lights.Add(light);
        RaiseChanged();
    }

    internal void InsertLight(int index, LightEntity light)
    {
        _lights.Insert(index, light);
        RaiseChanged();
    }

    internal void RemoveLight(LightEntity light)
    {
        _lights.Remove(light);
        RaiseChanged();
    }

    internal void AddSpawnPoint(SpawnPoint sp)
    {
        _spawnPoints.Add(sp);
        RaiseChanged();
    }

    internal void InsertSpawnPoint(int index, SpawnPoint sp)
    {
        _spawnPoints.Insert(index, sp);
        RaiseChanged();
    }

    internal void RemoveSpawnPoint(SpawnPoint sp)
    {
        _spawnPoints.Remove(sp);
        RaiseChanged();
    }

    internal void RaiseChanged() =>
        SceneChanged?.Invoke(this, SceneChangedEventArgs.Default);

    /// <summary>Replaces all content with data from <paramref name="other"/>. Does not copy event handlers.</summary>
    internal void ReplaceFrom(Scene other)
    {
        _brushes.Clear();
        _brushes.AddRange(other._brushes);
        _lights.Clear();
        _lights.AddRange(other._lights);
        _spawnPoints.Clear();
        _spawnPoints.AddRange(other._spawnPoints);
        WorldSettings = other.WorldSettings;
        AdditionalData = other.AdditionalData;
        RaiseChanged();
    }
}
