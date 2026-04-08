namespace MapEditor.Core.Entities;

/// <summary>A player or NPC spawn point placed in the scene.</summary>
public sealed class SpawnPoint : IEntity
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "SpawnPoint";
    public Transform Transform { get; set; } = Transform.Identity;

    /// <summary>User-defined tag identifying the spawn type (e.g. "player", "enemy").</summary>
    public string SpawnType { get; set; } = "player";

    public SpawnPoint() { }
    public SpawnPoint(Guid id, string name) { Id = id; Name = name; }
}
