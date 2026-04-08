using System.Numerics;

namespace MapEditor.Core.Entities;

/// <summary>Global scene environment settings.</summary>
public sealed class WorldSettings
{
    /// <summary>Ambient light color as linear RGB (0..1).</summary>
    public Vector3 AmbientColor { get; set; } = new Vector3(0.1f, 0.1f, 0.1f);

    /// <summary>Sky / background color as linear RGB (0..1).</summary>
    public Vector3 SkyColor { get; set; } = new Vector3(0.2f, 0.3f, 0.4f);
}
