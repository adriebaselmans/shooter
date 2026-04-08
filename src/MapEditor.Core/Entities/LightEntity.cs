using System.Numerics;

namespace MapEditor.Core.Entities;

/// <summary>Light source types supported by the editor.</summary>
public enum LightType { Point, Spot }

/// <summary>A light entity placed in the scene.</summary>
public sealed class LightEntity : IEntity
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Light";
    public LightType LightType { get; set; } = LightType.Point;
    public Transform Transform { get; set; } = Transform.Identity;

    /// <summary>Light color as linear RGB (0..1 per channel).</summary>
    public Vector3 Color { get; set; } = Vector3.One;

    public float Intensity { get; set; } = 1.0f;
    public float Range { get; set; } = 500.0f;

    /// <summary>Half-angle in degrees (spot lights only).</summary>
    public float ConeAngle { get; set; } = 30.0f;

    public LightEntity() { }
    public LightEntity(Guid id, string name) { Id = id; Name = name; }
}
