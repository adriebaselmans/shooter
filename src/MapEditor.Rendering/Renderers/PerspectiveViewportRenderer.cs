using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;
using MapEditor.Core;
using MapEditor.Rendering.Buffers;
using MapEditor.Rendering.Cameras;
using MapEditor.Rendering.Infrastructure;
using Silk.NET.OpenGL;
using System.Numerics;

namespace MapEditor.Rendering.Renderers;

/// <summary>Render mode for the perspective viewport.</summary>
public enum RenderMode { Wireframe, Solid }

/// <summary>
/// Renders the scene in a perspective 3D view using Phong shading.
/// Manages per-brush mesh buffers and shader programs.
/// </summary>
public sealed class PerspectiveViewportRenderer : IDisposable
{
    private readonly GL _gl;
    private ShaderProgram? _solidShader;
    private ShaderProgram? _wireShader;
    private DynamicLineBuffer? _gridBuffer;
    private readonly Dictionary<Guid, BrushMeshBuffer> _brushBuffers = new();
    private bool _shadersLoaded;

    public PerspectiveCamera Camera { get; } = new();
    public RenderMode RenderMode { get; set; } = RenderMode.Solid;
    public HashSet<Guid> SelectedEntityIds { get; } = new();
    /// <summary>Enables per-render visibility diagnostics for automated tests.</summary>
    public bool CaptureDiagnostics { get; set; }
    /// <summary>Diagnostics captured during the most recent render.</summary>
    public ViewportRenderDiagnostics LastDiagnostics { get; private set; } = ViewportRenderDiagnostics.Empty;

    public PerspectiveViewportRenderer(GL gl)
    {
        _gl = gl;
    }

    private void EnsureShaders()
    {
        if (_shadersLoaded) return;
        _solidShader = ShaderProgram.FromEmbeddedResources(_gl, "brush_solid.vert.glsl", "brush_solid.frag.glsl");
        _wireShader  = ShaderProgram.FromEmbeddedResources(_gl, "flat_color.vert.glsl",  "flat_color.frag.glsl");
        _gridBuffer = new DynamicLineBuffer(_gl);
        _shadersLoaded = true;
    }

    public void Render(Scene scene, int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        EnsureShaders();

        _gl.Viewport(0, 0, (uint)width, (uint)height);
        _gl.Enable(EnableCap.DepthTest);
        _gl.ClearColor(0.12f, 0.12f, 0.12f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        float aspect = (float)width / height;
        var view = Camera.GetViewMatrix();
        var proj = Camera.GetProjectionMatrix(aspect);

        var gridDiagnostics = RenderFloorGuide(view, proj);

        IReadOnlyList<BrushRenderDiagnostic> brushDiagnostics;
        if (RenderMode == RenderMode.Solid)
            brushDiagnostics = RenderSolid(scene, view, proj);
        else
            brushDiagnostics = RenderAllWireframe(scene, view, proj);

        LastDiagnostics = new ViewportRenderDiagnostics(gridDiagnostics, brushDiagnostics);
    }

    private IReadOnlyList<BrushRenderDiagnostic> RenderSolid(Scene scene, Matrix4x4 view, Matrix4x4 proj)
    {
        List<BrushRenderDiagnostic>? diagnostics = CaptureDiagnostics ? [] : null;
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _solidShader!.Use();
        _solidShader.SetUniform("uView", view);
        _solidShader.SetUniform("uProjection", proj);
        _solidShader.SetUniform("uAmbientColor", scene.WorldSettings.AmbientColor);

        // Upload light data
        var lights = scene.Lights;
        _solidShader.SetUniform("uLightCount", Math.Min(lights.Count, 8));
        for (int i = 0; i < Math.Min(lights.Count, 8); i++)
        {
            var l = lights[i];
            _solidShader.SetUniform($"uLights[{i}].position", l.Transform.Position);
            _solidShader.SetUniform($"uLights[{i}].color",    l.Color);
            _solidShader.SetUniform($"uLights[{i}].intensity", l.Intensity);
            _solidShader.SetUniform($"uLights[{i}].range",    l.Range);
        }

        foreach (var brush in scene.Brushes)
        {
            var buf = GetOrCreateBuffer(brush);
            var model = BuildModelMatrix(brush.Transform);

            _solidShader.SetUniform("uModel", model);
            _solidShader.SetUniform("uObjectColor", BrushColorPalette.GetPerspectiveSolid(brush.Operation));
            _solidShader.SetUniform("uIsSubtractive", brush.Operation == BrushOperation.Subtractive);

            bool solidSamplesPassed = DrawWithVisibilityQuery(buf.DrawSolid);
            bool wireSamplesPassed = false;

            if (SelectedEntityIds.Contains(brush.Id))
            {
                wireSamplesPassed = DrawSelectionOutline(buf, model, view, proj);
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

        _gl.Disable(EnableCap.Blend);
        return diagnostics ?? [];
    }

    private IReadOnlyList<BrushRenderDiagnostic> RenderAllWireframe(Scene scene, Matrix4x4 view, Matrix4x4 proj)
    {
        List<BrushRenderDiagnostic>? diagnostics = CaptureDiagnostics ? [] : null;
        _wireShader!.Use();
        _wireShader.SetUniform("uView", view);
        _wireShader.SetUniform("uProjection", proj);

        foreach (var brush in scene.Brushes)
        {
            var buf = GetOrCreateBuffer(brush);
            var model = BuildModelMatrix(brush.Transform);
            _wireShader.SetUniform("uModel", model);
            var color = BrushColorPalette.GetWireframe(brush.Operation, SelectedEntityIds.Contains(brush.Id));
            _wireShader.SetUniform("uColor", color);
            bool wireSamplesPassed = DrawWithVisibilityQuery(buf.DrawWireframe);

            if (diagnostics is not null)
            {
                diagnostics.Add(new BrushRenderDiagnostic(
                    brush.Id,
                    model,
                    false,
                    wireSamplesPassed));
            }
        }

        return diagnostics ?? [];
    }

    private GridRenderDiagnostic RenderFloorGuide(Matrix4x4 view, Matrix4x4 proj)
    {
        var geometry = GridGeometryBuilder.BuildPerspectiveFloor(32f, Camera);

        _wireShader!.Use();
        _wireShader.SetUniform("uView", view);
        _wireShader.SetUniform("uProjection", proj);
        _wireShader.SetUniform("uModel", Matrix4x4.Identity);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.DepthMask(false);

        DrawGridBatch(geometry.MinorVertices, new Vector4(0.24f, 0.24f, 0.24f, 0.45f));
        DrawGridBatch(geometry.MajorVertices, new Vector4(0.38f, 0.38f, 0.38f, 0.70f));
        DrawGridBatch(geometry.PrimaryAxisVertices, new Vector4(0.24f, 0.52f, 0.92f, 0.90f));
        DrawGridBatch(geometry.SecondaryAxisVertices, new Vector4(0.86f, 0.26f, 0.26f, 0.90f));
        DrawGridBatch(
            [Vector3.Zero, new Vector3(0f, MathF.Max(geometry.MajorSpacing * 4f, 256f), 0f)],
            new Vector4(0.26f, 0.80f, 0.34f, 0.90f));

        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);

        return new GridRenderDiagnostic(
            geometry.MinorVertices.Length,
            geometry.MajorVertices.Length,
            geometry.PrimaryAxisVertices.Length,
            geometry.SecondaryAxisVertices.Length);
    }

    private bool DrawSelectionOutline(BrushMeshBuffer buf, Matrix4x4 model, Matrix4x4 view, Matrix4x4 proj)
    {
        _wireShader!.Use();
        _wireShader.SetUniform("uView", view);
        _wireShader.SetUniform("uProjection", proj);
        _wireShader.SetUniform("uColor", BrushColorPalette.GetSelectionOutline());

        _gl.Disable(EnableCap.DepthTest);
        var scaledModel = Matrix4x4.CreateScale(1.02f) * model;
        _wireShader.SetUniform("uModel", scaledModel);
        bool wireSamplesPassed = DrawWithVisibilityQuery(buf.DrawWireframe);
        _gl.Enable(EnableCap.DepthTest);
        return wireSamplesPassed;
    }

    private void DrawGridBatch(ReadOnlySpan<Vector3> vertices, Vector4 color)
    {
        if (vertices.Length == 0)
        {
            return;
        }

        _wireShader!.SetUniform("uColor", color);
        _gridBuffer!.SetVertices(vertices);
        _gridBuffer.Draw();
    }

    private BrushMeshBuffer GetOrCreateBuffer(Brush brush)
    {
        if (!_brushBuffers.TryGetValue(brush.Id, out var buf))
        {
            buf = new BrushMeshBuffer(_gl);
            buf.SetMesh(MeshGenerator.GenerateMesh(brush.Primitive));
            _brushBuffers[brush.Id] = buf;
        }
        return buf;
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

    /// <summary>Marks a brush's buffer dirty (e.g., after transform change).</summary>
    public void InvalidateBrush(Guid brushId)
    {
        if (_brushBuffers.TryGetValue(brushId, out var buf))
            buf.IsDirty = true;
    }

    /// <summary>Removes a brush buffer when the brush is deleted.</summary>
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
        _solidShader?.Dispose();
        _wireShader?.Dispose();
    }
}
