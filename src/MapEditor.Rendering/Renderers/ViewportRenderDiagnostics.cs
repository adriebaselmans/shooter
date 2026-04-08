using System.Numerics;

namespace MapEditor.Rendering.Renderers;

/// <summary>Captures how many grid vertices were submitted during the last viewport render.</summary>
public sealed record GridRenderDiagnostic(
    /// <summary>Submitted orthographic/perspective minor-grid vertex count.</summary>
    int MinorVertexCount,
    /// <summary>Submitted orthographic/perspective major-grid vertex count.</summary>
    int MajorVertexCount,
    /// <summary>Submitted primary-axis guide vertex count.</summary>
    int PrimaryAxisVertexCount,
    /// <summary>Submitted secondary-axis guide vertex count.</summary>
    int SecondaryAxisVertexCount);

/// <summary>Captures per-brush visibility diagnostics from the last viewport render.</summary>
public sealed record BrushRenderDiagnostic(
    /// <summary>The rendered brush identifier.</summary>
    Guid BrushId,
    /// <summary>The model matrix used for the brush during the render.</summary>
    Matrix4x4 ModelMatrix,
    /// <summary>True when the brush solid pass produced at least one framebuffer sample.</summary>
    bool SolidSamplesPassed,
    /// <summary>True when the brush wire pass produced at least one framebuffer sample.</summary>
    bool WireSamplesPassed);

/// <summary>Captures the last viewport render's grid and brush diagnostics.</summary>
public sealed record ViewportRenderDiagnostics(
    /// <summary>Grid submission diagnostics for the last render.</summary>
    GridRenderDiagnostic Grid,
    /// <summary>Per-brush diagnostics for the last render.</summary>
    IReadOnlyList<BrushRenderDiagnostic> Brushes)
{
    /// <summary>An empty diagnostics instance used before the first render.</summary>
    public static ViewportRenderDiagnostics Empty { get; } =
        new(new GridRenderDiagnostic(0, 0, 0, 0), []);
}
