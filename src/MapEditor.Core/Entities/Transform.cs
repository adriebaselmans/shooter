using System.Numerics;

namespace MapEditor.Core.Entities;

/// <summary>Immutable world-space transform: position, Euler rotation (degrees), and scale.</summary>
public sealed record Transform
{
    public static readonly Transform Identity = new();

    public Vector3 Position { get; init; } = Vector3.Zero;
    public Vector3 EulerDegrees { get; init; } = Vector3.Zero;
    public Vector3 Scale { get; init; } = Vector3.One;

    /// <summary>Returns a copy with the given position.</summary>
    public Transform WithPosition(Vector3 position) => this with { Position = position };

    /// <summary>Returns a copy with the given rotation.</summary>
    public Transform WithRotation(Vector3 eulerDegrees) => this with { EulerDegrees = eulerDegrees };

    /// <summary>Returns a copy with the given scale.</summary>
    public Transform WithScale(Vector3 scale) => this with { Scale = scale };
}
