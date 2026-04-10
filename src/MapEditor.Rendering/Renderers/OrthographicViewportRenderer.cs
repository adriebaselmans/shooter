using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;
using MapEditor.Core;
using MapEditor.Rendering.Buffers;
using MapEditor.Rendering.Cameras;
using MapEditor.Rendering.Infrastructure;
using Silk.NET.OpenGL;
using System.Numerics;

namespace MapEditor.Rendering.Renderers;

/// <summary>
/// Renders the scene from an orthographic axis-aligned viewpoint.
/// Shows brush outlines and entity icons. No lighting.
/// </summary>
public sealed class OrthographicViewportRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ITextureCatalog? _textureCatalog;
    private ShaderProgram? _shader;
    private DynamicLineBuffer? _gridBuffer;
    private readonly Dictionary<Guid, BrushMeshBuffer> _brushBuffers = new();
    private bool _shadersLoaded;

    public OrthographicCamera Camera { get; }
    public HashSet<Guid> SelectedEntityIds { get; } = new();
    public Guid? PrimarySelectedEntityId { get; set; }
    public float GridSpacing { get; set; } = 32f;
    /// <summary>Enables per-render visibility diagnostics for automated tests.</summary>
    public bool CaptureDiagnostics { get; set; }
    /// <summary>Diagnostics captured during the most recent render.</summary>
    public ViewportRenderDiagnostics LastDiagnostics { get; private set; } = ViewportRenderDiagnostics.Empty;

    public OrthographicViewportRenderer(GL gl, ViewAxis axis, ITextureCatalog? textureCatalog = null)
    {
        _gl = gl;
        _textureCatalog = textureCatalog;
        Camera = new OrthographicCamera { Axis = axis };
    }

    private void EnsureShaders()
    {
        if (_shadersLoaded) return;
        _shader = ShaderProgram.FromEmbeddedResources(_gl, "flat_color.vert.glsl", "flat_color.frag.glsl");
        _gridBuffer = new DynamicLineBuffer(_gl);
        _shadersLoaded = true;
    }

    public void Render(Scene scene, int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        EnsureShaders();

        _gl.Viewport(0, 0, (uint)width, (uint)height);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.ClearColor(0.15f, 0.15f, 0.15f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        float aspect = (float)width / height;
        var view = Camera.GetViewMatrix();
        var proj = Camera.GetProjectionMatrix(aspect);

        var gridDiagnostics = DrawGrid(view, proj, aspect);
        var brushDiagnostics = DrawBrushes(scene, view, proj);
        DrawResizeHandles(scene, view, proj);
        LastDiagnostics = new ViewportRenderDiagnostics(gridDiagnostics, brushDiagnostics);
        _gl.Disable(EnableCap.Blend);
    }

    private GridRenderDiagnostic DrawGrid(Matrix4x4 view, Matrix4x4 proj, float aspect)
    {
        var geometry = GridGeometryBuilder.BuildOrthographic(
            Camera.Axis,
            GridSpacing,
            aspect,
            Camera.Pan,
            Camera.PanY,
            Camera.Zoom);

        _shader!.Use();
        _shader.SetUniform("uView", view);
        _shader.SetUniform("uProjection", proj);
        _shader.SetUniform("uModel", Matrix4x4.Identity);
        DrawGridBatch(geometry.MinorVertices, new Vector4(0.24f, 0.24f, 0.24f, 0.55f));
        DrawGridBatch(geometry.MajorVertices, new Vector4(0.34f, 0.34f, 0.34f, 0.85f));

        var (primaryAxisColor, secondaryAxisColor) = GetAxisColors(Camera.Axis);
        DrawGridBatch(geometry.PrimaryAxisVertices, primaryAxisColor);
        DrawGridBatch(geometry.SecondaryAxisVertices, secondaryAxisColor);

        return new GridRenderDiagnostic(
            geometry.MinorVertices.Length,
            geometry.MajorVertices.Length,
            geometry.PrimaryAxisVertices.Length,
            geometry.SecondaryAxisVertices.Length);
    }

    private IReadOnlyList<BrushRenderDiagnostic> DrawBrushes(Scene scene, Matrix4x4 view, Matrix4x4 proj)
    {
        List<BrushRenderDiagnostic>? diagnostics = CaptureDiagnostics ? [] : null;
        _shader!.Use();
        _shader.SetUniform("uView", view);
        _shader.SetUniform("uProjection", proj);

        foreach (var brush in scene.Brushes)
        {
            var buf = GetOrCreateBuffer(brush);
            var model = BuildModelMatrix(brush.Transform);
            _shader.SetUniform("uModel", model);

            bool selected = SelectedEntityIds.Contains(brush.Id);
            var fillColor = GetOrthographicFillColor(brush);
            var outlineColor = BrushColorPalette.GetOrthographicOutline(brush.Operation);
            var selectionOutlineColor = BrushColorPalette.GetSelectionOutline();

            _shader.SetUniform("uColor", fillColor);
            bool solidSamplesPassed = DrawWithVisibilityQuery(buf.DrawSolid);

            _shader.SetUniform("uColor", outlineColor);
            bool wireSamplesPassed = DrawWithVisibilityQuery(buf.DrawWireframe);

            if (selected)
            {
                _shader.SetUniform("uColor", selectionOutlineColor);
                wireSamplesPassed |= DrawWithVisibilityQuery(buf.DrawWireframe);
            }

            if (diagnostics is not null)
            {
                diagnostics.Add(new BrushRenderDiagnostic(
                    brush.Id,
                    model,
                    solidSamplesPassed,
                    wireSamplesPassed));
            }
        }

        return diagnostics ?? [];
    }

    private void DrawResizeHandles(Scene scene, Matrix4x4 view, Matrix4x4 proj)
    {
        if (PrimarySelectedEntityId is null)
        {
            return;
        }

        var brush = scene.Brushes.FirstOrDefault(candidate => candidate.Id == PrimarySelectedEntityId.Value);
        if (brush is null)
        {
            return;
        }

        var handleSize = ResizeHandleMath.GetHandleSize(Camera.Zoom, GridSpacing);
        var vertices = ResizeHandleMath.BuildHandleOutlineVertices(brush.Transform, Camera.Axis, handleSize);
        if (vertices.Length == 0)
        {
            return;
        }

        _shader!.Use();
        _shader.SetUniform("uView", view);
        _shader.SetUniform("uProjection", proj);
        _shader.SetUniform("uModel", Matrix4x4.Identity);
        _shader.SetUniform("uColor", BrushColorPalette.GetSelectionOutline());
        _gridBuffer!.SetVertices(vertices);
        _gridBuffer.Draw();
    }

    private void DrawGridBatch(ReadOnlySpan<Vector3> vertices, Vector4 color)
    {
        if (vertices.Length == 0)
        {
            return;
        }

        _shader!.SetUniform("uColor", color);
        _gridBuffer!.SetVertices(vertices);
        _gridBuffer.Draw();
    }

    private BrushMeshBuffer GetOrCreateBuffer(Brush brush)
    {
        if (!_brushBuffers.TryGetValue(brush.Id, out var buf))
        {
            buf = new BrushMeshBuffer(_gl);
            _brushBuffers[brush.Id] = buf;
        }

        string meshSignature = CreateMeshSignature(brush);
        if (buf.MeshSignature != meshSignature || buf.Mesh is null)
        {
            buf.SetMesh(MeshGenerator.GenerateMesh(brush));
            buf.MeshSignature = meshSignature;
        }

        return buf;
    }

    private string CreateMeshSignature(Brush brush) =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{brush.Primitive}|{brush.Transform.Scale.X:0.####}|{brush.Transform.Scale.Y:0.####}|{brush.Transform.Scale.Z:0.####}|{brush.AppearanceVersion}");

    private Vector4 GetOrthographicFillColor(Brush brush)
    {
        var baseFill = BrushColorPalette.GetOrthographicFill(brush.Operation);
        if (_textureCatalog is null || !_textureCatalog.TryGetTexture(brush.MaterialName, out var texture))
        {
            return baseFill;
        }

        var average = texture.AverageColor;
        return new Vector4(
            Math.Clamp((baseFill.X + average.X) * 0.5f, 0f, 1f),
            Math.Clamp((baseFill.Y + average.Y) * 0.5f, 0f, 1f),
            Math.Clamp((baseFill.Z + average.Z) * 0.5f, 0f, 1f),
            baseFill.W);
    }

    private unsafe bool DrawWithVisibilityQuery(Action drawAction)
    {
        if (!CaptureDiagnostics)
        {
            drawAction();
            return false;
        }

        uint query = _gl.GenQuery();
        _gl.BeginQuery(QueryTarget.AnySamplesPassed, query);
        drawAction();
        _gl.EndQuery(QueryTarget.AnySamplesPassed);

        uint result = 0;
        _gl.GetQueryObject(query, QueryObjectParameterName.QueryResult, &result);
        _gl.DeleteQuery(query);
        return result != 0;
    }

    public void RemoveBrush(Guid brushId)
    {
        if (_brushBuffers.Remove(brushId, out var buf))
            buf.Dispose();
    }

    private static Matrix4x4 BuildModelMatrix(Core.Entities.Transform t)
    {
        var scale = Matrix4x4.CreateScale(t.Scale);
        var rot   = Matrix4x4.CreateFromYawPitchRoll(
            float.DegreesToRadians(t.EulerDegrees.Y),
            float.DegreesToRadians(t.EulerDegrees.X),
            float.DegreesToRadians(t.EulerDegrees.Z));
        var trans = Matrix4x4.CreateTranslation(t.Position);
        return scale * rot * trans;
    }

    public void Dispose()
    {
        foreach (var buf in _brushBuffers.Values) buf.Dispose();
        _gridBuffer?.Dispose();
        _shader?.Dispose();
    }

    private static (Vector4 Primary, Vector4 Secondary) GetAxisColors(ViewAxis axis) => axis switch
    {
        ViewAxis.Top => (new Vector4(0.24f, 0.50f, 0.90f, 1f), new Vector4(0.80f, 0.24f, 0.24f, 1f)),
        ViewAxis.Front => (new Vector4(0.26f, 0.80f, 0.34f, 1f), new Vector4(0.80f, 0.24f, 0.24f, 1f)),
        _ => (new Vector4(0.26f, 0.80f, 0.34f, 1f), new Vector4(0.24f, 0.50f, 0.90f, 1f))
    };
}
