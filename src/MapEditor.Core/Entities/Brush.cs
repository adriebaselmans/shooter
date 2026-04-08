namespace MapEditor.Core.Entities;

/// <summary>CSG brush primitive shape types.</summary>
public enum BrushPrimitive { Box, Cylinder, Cone, Wedge }

/// <summary>Determines whether a brush adds or subtracts from solid space.</summary>
public enum BrushOperation { Additive, Subtractive }

/// <summary>A CSG brush entity defining a geometric region in the scene.</summary>
public sealed class Brush : IEntity
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Brush";
    public BrushOperation Operation { get; set; } = BrushOperation.Additive;
    public BrushPrimitive Primitive { get; set; } = BrushPrimitive.Box;
    public Transform Transform { get; set; } = Transform.Identity;
    public string MaterialName { get; set; } = "default";

    public Brush() { }

    public Brush(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>Returns a shallow copy with a new identity.</summary>
    public Brush Clone() => new(Guid.NewGuid(), Name)
    {
        Operation = Operation,
        Primitive = Primitive,
        Transform = Transform,
        MaterialName = MaterialName
    };
}
