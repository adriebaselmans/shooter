using MapEditor.Core;
using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;
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
    private readonly ITextureCatalog? _textureCatalog;
    private readonly TextureGpuCache _textureCache;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private ShaderProgram? _solidShader;
    private ShaderProgram? _wireShader;
    private DynamicLineBuffer? _gridBuffer;
    private readonly Dictionary<Guid, BrushMeshBuffer> _brushBuffers = new();
    private bool _shadersLoaded;

    public PerspectiveCamera Camera { get; } = new();
    public RenderMode RenderMode { get; set; } = RenderMode.Solid;
    public HashSet<Guid> SelectedEntityIds { get; } = new();
    public Guid? SelectedSurfaceBrushId { get; set; }
    public HashSet<string> SelectedSurfaceIds { get; } = new(StringComparer.Ordinal);
    /// <summary>Enables per-render visibility diagnostics for automated tests.</summary>
    public bool CaptureDiagnostics { get; set; }
    /// <summary>Diagnostics captured during the most recent render.</summary>
    public ViewportRenderDiagnostics LastDiagnostics { get; private set; } = ViewportRenderDiagnostics.Empty;

    public PerspectiveViewportRenderer(GL gl, ITextureCatalog? textureCatalog = null)
    {
        _gl = gl;
        _textureCatalog = textureCatalog;
        _textureCache = new TextureGpuCache(gl);
    }

    private void EnsureShaders()
    {
        if (_shadersLoaded) return;
        _solidShader = ShaderProgram.FromEmbeddedResources(_gl, "brush_solid.vert.glsl", "brush_solid.frag.glsl");
        _wireShader = ShaderProgram.FromEmbeddedResources(_gl, "flat_color.vert.glsl", "flat_color.frag.glsl");
        _gridBuffer = new DynamicLineBuffer(_gl);
        _shadersLoaded = true;
    }

    public void Render(Scene scene, int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        EnsureShaders();
        PruneStaleBuffers(scene);

        _gl.Viewport(0, 0, (uint)width, (uint)height);
        _gl.Enable(EnableCap.DepthTest);
        _gl.ClearColor(0.12f, 0.12f, 0.12f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        float aspect = (float)width / height;
        var view = Camera.GetViewMatrix();
        var proj = Camera.GetProjectionMatrix(aspect);

        var gridDiagnostics = RenderFloorGuide(view, proj);
        IReadOnlyList<BrushRenderDiagnostic> brushDiagnostics = RenderMode == RenderMode.Solid
            ? RenderSolid(scene, view, proj)
            : RenderAllWireframe(scene, view, proj);

        LastDiagnostics = new ViewportRenderDiagnostics(gridDiagnostics, brushDiagnostics);
    }

    private IReadOnlyList<BrushRenderDiagnostic> RenderSolid(Scene scene, Matrix4x4 view, Matrix4x4 proj)
    {
        List<BrushRenderDiagnostic>? diagnostics = CaptureDiagnostics ? [] : null;
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Enable(EnableCap.Texture2D);
        _solidShader!.Use();
        _solidShader.SetUniform("uView", view);
        _solidShader.SetUniform("uProjection", proj);
        _solidShader.SetUniform("uAmbientColor", scene.WorldSettings.AmbientColor);
        _solidShader.SetUniform("uTexture0", 0);
        _solidShader.SetUniform("uTimeSeconds", (float)_clock.Elapsed.TotalSeconds);

        var lights = scene.Lights;
        _solidShader.SetUniform("uLightCount", Math.Min(lights.Count, 8));
        for (int i = 0; i < Math.Min(lights.Count, 8); i++)
        {
            var l = lights[i];
            _solidShader.SetUniform($"uLights[{i}].position", l.Transform.Position);
            _solidShader.SetUniform($"uLights[{i}].color", l.Color);
            _solidShader.SetUniform($"uLights[{i}].intensity", l.Intensity);
            _solidShader.SetUniform($"uLights[{i}].range", l.Range);
        }

        foreach (var brush in scene.Brushes)
        {
            var buf = GetOrCreateBuffer(brush);
            var model = TransformMath.BuildModelMatrix(brush.Transform);
            _solidShader.SetUniform("uModel", model);

            bool solidSamplesPassed = false;
            bool wireSamplesPassed = false;

            var surfaces = buf.Mesh?.Surfaces;
            if (surfaces is null || surfaces.Count == 0)
            {
                var descriptor = ResolveTexture(brush.MaterialName);
                BindTexture(descriptor);
                _solidShader.SetUniform("uUseTexture", descriptor is not null);
                _solidShader.SetUniform("uObjectColor", BrushColorPalette.GetPerspectiveSolid(brush.Operation));
                _solidShader.SetUniform("uIsSubtractive", brush.Operation == BrushOperation.Subtractive);
                solidSamplesPassed = DrawWithVisibilityQuery(buf.DrawSolid);
            }
            else
            {
                foreach (var surface in surfaces)
                {
                    // Enable backface culling for cutter-sourced inner walls
                    if (surface.IsCutterFace)
                    {
                        _gl.Enable(EnableCap.CullFace);
                        _gl.CullFace(TriangleFace.Back);
                    }

                    var mapping = brush.GetEffectiveSurfaceMapping(surface.SurfaceId);
                    var descriptor = ResolveTexture(mapping.TextureKey);
                    BindTexture(descriptor);
                    _solidShader.SetUniform("uUseTexture", descriptor is not null);
                    _solidShader.SetUniform("uObjectColor", GetSurfaceTint(brush.Operation, descriptor));
                    _solidShader.SetUniform("uIsSubtractive", brush.Operation == BrushOperation.Subtractive);
                    solidSamplesPassed |= DrawWithVisibilityQuery(() => buf.DrawSurface(surface));

                    if (surface.IsCutterFace)
                    {
                        _gl.Disable(EnableCap.CullFace);
                    }
                }

                if (SelectedSurfaceBrushId == brush.Id && SelectedSurfaceIds.Count > 0)
                {
                    wireSamplesPassed |= DrawSelectedSurfaces(buf, model, view, proj);
                }
            }

            if (SelectedEntityIds.Contains(brush.Id))
            {
                wireSamplesPassed |= DrawSelectionOutline(buf, model, view, proj);
            }

            if (diagnostics is not null)
            {
                diagnostics.Add(new BrushRenderDiagnostic(brush.Id, model, solidSamplesPassed, wireSamplesPassed));
            }
        }

        _gl.Disable(EnableCap.Texture2D);
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
            var model = TransformMath.BuildModelMatrix(brush.Transform);
            _wireShader.SetUniform("uModel", model);

            var wireColor = BrushColorPalette.GetWireframe(brush.Operation, SelectedEntityIds.Contains(brush.Id));
            _wireShader.SetUniform("uColor", wireColor);
            bool wireSamplesPassed = DrawWithVisibilityQuery(buf.DrawTargetEdges);

            if (buf.HasCutterEdges)
            {
                var dimmedWireColor = wireColor with { W = wireColor.W * 0.3f };
                _wireShader.SetUniform("uColor", dimmedWireColor);
                wireSamplesPassed |= DrawWithVisibilityQuery(buf.DrawCutterEdges);
            }

            if (diagnostics is not null)
            {
                diagnostics.Add(new BrushRenderDiagnostic(brush.Id, model, false, wireSamplesPassed));
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
        DrawGridBatch([Vector3.Zero, new Vector3(0f, MathF.Max(geometry.MajorSpacing * 4f, 256f), 0f)], new Vector4(0.26f, 0.80f, 0.34f, 0.90f));

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

    private bool DrawSelectedSurfaces(BrushMeshBuffer buf, Matrix4x4 model, Matrix4x4 view, Matrix4x4 proj)
    {
        if (buf.Mesh is null)
        {
            return false;
        }

        _wireShader!.Use();
        _wireShader.SetUniform("uView", view);
        _wireShader.SetUniform("uProjection", proj);
        _wireShader.SetUniform("uModel", model);
        _wireShader.SetUniform("uColor", new Vector4(1.0f, 0.83f, 0.28f, 1f));

        bool samplesPassed = false;
        foreach (var surface in buf.Mesh.Surfaces)
        {
            if (SelectedSurfaceIds.Contains(surface.SurfaceId))
            {
                samplesPassed |= DrawWithVisibilityQuery(() => buf.DrawSurface(surface));
            }
        }

        return samplesPassed;
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
            $"{brush.Primitive}|{brush.GeometryVersion}|{brush.Transform.Scale.X:0.####}|{brush.Transform.Scale.Y:0.####}|{brush.Transform.Scale.Z:0.####}|{brush.AppearanceVersion}");

    private TextureAssetDescriptor? ResolveTexture(string textureKey) =>
        _textureCatalog is not null && _textureCatalog.TryGetTexture(textureKey, out var descriptor)
            ? descriptor
            : null;

    private void BindTexture(TextureAssetDescriptor? descriptor)
    {
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _textureCache.GetTextureHandle(descriptor));
        _solidShader!.SetUniform("uIsAnimatedTexture", descriptor?.Animation?.IsAnimated == true);
        _solidShader.SetUniform("uAnimationKind", descriptor?.Kind switch
        {
            TextureMaterialKind.Water => 1,
            TextureMaterialKind.Lava => 2,
            TextureMaterialKind.Effect => 3,
            _ => 0
        });
        _solidShader.SetUniform("uFlowSpeed", descriptor?.Animation?.FlowSpeed ?? 0f);
        _solidShader.SetUniform("uPulseStrength", descriptor?.Animation?.PulseStrength ?? 0f);
    }

    private static Vector4 GetSurfaceTint(BrushOperation operation, TextureAssetDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return BrushColorPalette.GetPerspectiveSolid(operation);
        }

        if (operation == BrushOperation.Subtractive)
        {
            return new Vector4(0.90f, 0.55f, 0.55f, 0.90f);
        }

        if (descriptor.Kind == TextureMaterialKind.Water)
        {
            return new Vector4(0.82f, 0.96f, 1f, 0.72f);
        }

        if (descriptor.Kind == TextureMaterialKind.Lava)
        {
            return new Vector4(1f, 0.82f, 0.62f, 0.96f);
        }

        return new Vector4(1f, 1f, 1f, 0.92f);
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
        _gl.GetQueryObject(query, QueryObjectParameterName.Result, &result);
        _gl.DeleteQuery(query);
        return result != 0;
    }

    /// <summary>Marks a brush's buffer dirty (e.g., after transform change).</summary>
    public void InvalidateBrush(Guid brushId)
    {
        if (_brushBuffers.TryGetValue(brushId, out var buf))
        {
            buf.IsDirty = true;
            buf.MeshSignature = null;
        }
    }

    /// <summary>Removes a brush buffer when the brush is deleted.</summary>
    public void RemoveBrush(Guid brushId)
    {
        if (_brushBuffers.Remove(brushId, out var buf))
        {
            buf.Dispose();
        }
    }

    public void SynchronizeBrushBuffers(IReadOnlyCollection<Guid> liveBrushIds)
    {
        ArgumentNullException.ThrowIfNull(liveBrushIds);

        foreach (var staleBrushId in _brushBuffers.Keys.Where(id => !liveBrushIds.Contains(id)).ToArray())
        {
            RemoveBrush(staleBrushId);
        }
    }

    private void PruneStaleBuffers(Scene scene)
    {
        var liveIds = new HashSet<Guid>(scene.Brushes.Select(b => b.Id));
        SynchronizeBrushBuffers(liveIds);
    }

    public void Dispose()
    {
        foreach (var buf in _brushBuffers.Values)
        {
            buf.Dispose();
        }

        _textureCache.Dispose();
        _gridBuffer?.Dispose();
        _solidShader?.Dispose();
        _wireShader?.Dispose();
    }
}
