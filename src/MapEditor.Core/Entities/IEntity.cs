namespace MapEditor.Core.Entities;

/// <summary>Common interface for all scene entities.</summary>
public interface IEntity
{
    Guid Id { get; }
    string Name { get; set; }
}
