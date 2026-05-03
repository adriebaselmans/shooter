using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using MapEditor.Core.Geometry;
using Shooter;
using Shooter.Game;
using Shooter.Platform.Metal;
using Shooter.Render;
using Silk.NET.Windowing;
using StbImageSharp;

namespace Shooter.RenderSystem;

public sealed class MetalBootstrapBackend : IRenderBackend
{
    private const ulong PixelFormatR8Unorm = 10;
    private const ulong PixelFormatR16Float = 25;
    private const ulong PixelFormatRgba8Unorm = 70;
    private const ulong PixelFormatBgra8Unorm = 80;
    private const ulong PixelFormatRgba16Float = 112;
    private const ulong PixelFormatDepth32Float = 252;

    private const ulong TextureUsageShaderRead = 1;
    private const ulong TextureUsageRenderTarget = 4;

    private const ulong PrimitiveTypeTriangle = 3;
    private const ulong PrimitiveTypeTriangleStrip = 4;

    private const ulong CompareLess = 1;
    private const ulong CompareLessEqual = 3;
    private const ulong CompareAlways = 7;

    private const int ShadowMapSize = 3072;
    private const float ShadowHalfExtent = 30f;
    private const float ShadowNear = -40f;
    private const float ShadowFar = 40f;
    private const int BloomMipCount = 5;
    private const int SsaoKernelSize = 16;
    private const int SsaoNoiseSize = 4;
    private const int HybridGiScale = 4;

    private IWindow? _window;
    private LightingEnvironment? _lighting;
    private IntPtr _nsWindow;
    private IntPtr _contentView;
    private IntPtr _layer;
    private IntPtr _device;
    private IntPtr _queue;

    private IntPtr _scenePipeline;
    private IntPtr _shadowPipeline;
    private IntPtr _postPipeline;
    private IntPtr _bloomThresholdPipeline;
    private IntPtr _bloomDownPipeline;
    private IntPtr _bloomUpPipeline;
    private IntPtr _ssaoPipeline;
    private IntPtr _ssaoBlurPipeline;
    private IntPtr _hybridGiPipeline;
    private IntPtr _hybridGiBlurPipeline;
    private IntPtr _alphaOverlayPipeline;
    private IntPtr _additiveOverlayPipeline;
    private IntPtr _uiPipeline;

    private IntPtr _depthStateWriteLess;
    private IntPtr _depthStateReadLessEqual;
    private IntPtr _depthStateDisabled;

    private IntPtr _linearClampSampler;
    private IntPtr _linearRepeatSampler;

    private IntPtr _vertexBuffer;
    private int _vertexCapacity;
    private GpuVertex[] _gpuVertices = [];

    private IntPtr _hdrTexture;
    private IntPtr _normalTexture;
    private IntPtr _albedoTexture;
    private IntPtr _depthTexture;
    private IntPtr _aoTexture;
    private IntPtr _aoBlurTexture;
    private readonly IntPtr[] _hybridGiTextures = new IntPtr[2];
    private IntPtr _hybridGiDenoiseTexture;
    private readonly IntPtr[] _bloomTextures = new IntPtr[BloomMipCount];
    private readonly int[] _bloomWidths = new int[BloomMipCount];
    private readonly int[] _bloomHeights = new int[BloomMipCount];
    private IntPtr _shadowTexture;
    private IntPtr _noiseTexture;
    private IntPtr _worldTriangleBuffer;
    private int _worldTriangleCount;
    private int _fbWidth;
    private int _fbHeight;
    private int _hybridGiWidth;
    private int _hybridGiHeight;
    private int _hybridGiHistoryIndex;
    private int _hybridGiSampleCount;
    private bool _hybridGiHasHistory;
    private Vector3 _hybridGiLastCamPos;
    private Vector3 _hybridGiLastForward;

    private readonly Dictionary<string, IntPtr> _textures = new(StringComparer.OrdinalIgnoreCase);
    private IntPtr _whiteTexture;
    private IntPtr _flatNormalTexture;

    private readonly List<SceneBatch> _sceneBatches = [];
    private readonly List<OverlayBatch> _alphaOverlayBatches = [];
    private readonly List<OverlayBatch> _additiveOverlayBatches = [];
    private readonly List<UiBatch> _uiBatches = [];
    private WorldSourceVertex[] _worldSource = [];
    private readonly List<WorldBatch> _worldBatches = [];

    private readonly Dictionary<WeaponKind, MetalModel> _weaponModels = new();
    private MetalModel? _rocketModel;

    private int _shadowVertexCount;
    private bool _initialized;

    public BackendKind Kind => BackendKind.Metal;
    public string Name => "Metal";

    public void Initialize(IWindow window, GameWorld world, LightingEnvironment lighting)
    {
        _window = window;
        _lighting = lighting;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            throw new PlatformNotSupportedException("Metal backend is only available on macOS.");

        if (window is not Silk.NET.Core.Contexts.INativeWindowSource nativeSource)
            throw new InvalidOperationException("Window does not expose native handles.");
        var native = nativeSource.Native ?? throw new InvalidOperationException("Native window handle unavailable.");
        if (!native.Cocoa.HasValue || native.Cocoa.Value == IntPtr.Zero)
            throw new InvalidOperationException("Cocoa NSWindow handle unavailable.");

        _nsWindow = native.Cocoa.Value;
        _contentView = Msg(_nsWindow, "contentView");
        if (_contentView == IntPtr.Zero)
            throw new InvalidOperationException("Failed to query NSWindow contentView.");

        _device = MetalNative.MTLCreateSystemDefaultDevice();
        if (_device == IntPtr.Zero)
            throw new InvalidOperationException("Metal device creation failed. Is Metal available on this macOS system?");
        _device = ObjCRuntime.objc_retain(_device);
        _queue = Msg(_device, "newCommandQueue");
        if (_queue == IntPtr.Zero)
            throw new InvalidOperationException("Metal command queue creation failed.");

        var layerClass = ObjCRuntime.objc_getClass("CAMetalLayer");
        _layer = Msg(layerClass, "layer");
        if (_layer == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create CAMetalLayer.");
        _layer = ObjCRuntime.objc_retain(_layer);
        MsgVoid(_layer, "setDevice:", _device);
        MsgVoid(_layer, "setPixelFormat:", PixelFormatBgra8Unorm);
        MsgVoid(_layer, "setFramebufferOnly:", false);
        MsgVoid(_contentView, "setWantsLayer:", true);
        MsgVoid(_contentView, "setLayer:", _layer);

        BuildPipelines();
        BuildDepthStates();
        BuildSamplers();
        _whiteTexture = CreateTextureFromPixels(1, 1, [255, 255, 255, 255], PixelFormatRgba8Unorm, TextureUsageShaderRead);
        _flatNormalTexture = CreateTextureFromPixels(1, 1, [128, 128, 255, 255], PixelFormatRgba8Unorm, TextureUsageShaderRead);
        _noiseTexture = CreateSsaoNoiseTexture();
        _shadowTexture = CreateTexture2D(PixelFormatDepth32Float, ShadowMapSize, ShadowMapSize, TextureUsageRenderTarget | TextureUsageShaderRead);
        if (_shadowTexture == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create Metal shadow depth texture.");

        BuildWorldSources(world);
        BuildWorldTriangleBuffer(world);
        LoadWeaponModels();
        LoadRocketModel();

        EnsureVertexCapacity(Math.Max(_worldSource.Length * 2 + 32768, 65536));
        Resize(window.FramebufferSize.X, window.FramebufferSize.Y);

        _initialized = true;
        Console.WriteLine($"[Renderer] Metal full-effects backend initialized (world verts={_worldSource.Length}, weaponModels={_weaponModels.Count}, rocket={(_rocketModel is null ? 0 : 1)}).");
    }

    public void Resize(int width, int height)
    {
        if (_device == IntPtr.Zero || _layer == IntPtr.Zero) return;
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (_fbWidth == width && _fbHeight == height && _hdrTexture != IntPtr.Zero) return;

        _fbWidth = width;
        _fbHeight = height;
        MsgVoid(_layer, "setDrawableSize:", new CGSize(width, height));

        ReleaseIfSet(ref _hdrTexture);
        ReleaseIfSet(ref _normalTexture);
        ReleaseIfSet(ref _albedoTexture);
        ReleaseIfSet(ref _depthTexture);
        ReleaseIfSet(ref _aoTexture);
        ReleaseIfSet(ref _aoBlurTexture);
        ReleaseIfSet(ref _hybridGiDenoiseTexture);
        for (int i = 0; i < 2; i++)
            ReleaseIfSet(ref _hybridGiTextures[i]);
        for (int i = 0; i < BloomMipCount; i++)
        {
            ReleaseIfSet(ref _bloomTextures[i]);
            _bloomWidths[i] = Math.Max(1, width >> (i + 1));
            _bloomHeights[i] = Math.Max(1, height >> (i + 1));
        }

        _hdrTexture = CreateTexture2D(PixelFormatRgba16Float, width, height, TextureUsageRenderTarget | TextureUsageShaderRead);
        _normalTexture = CreateTexture2D(PixelFormatRgba16Float, width, height, TextureUsageRenderTarget | TextureUsageShaderRead);
        _albedoTexture = CreateTexture2D(PixelFormatRgba16Float, width, height, TextureUsageRenderTarget | TextureUsageShaderRead);
        _depthTexture = CreateTexture2D(PixelFormatDepth32Float, width, height, TextureUsageRenderTarget | TextureUsageShaderRead);
        _aoTexture = CreateTexture2D(PixelFormatR16Float, width, height, TextureUsageRenderTarget | TextureUsageShaderRead);
        _aoBlurTexture = CreateTexture2D(PixelFormatR16Float, width, height, TextureUsageRenderTarget | TextureUsageShaderRead);
        _hybridGiWidth = Math.Max(1, width / HybridGiScale);
        _hybridGiHeight = Math.Max(1, height / HybridGiScale);
        for (int i = 0; i < 2; i++)
            _hybridGiTextures[i] = CreateTexture2D(PixelFormatRgba16Float, _hybridGiWidth, _hybridGiHeight, TextureUsageRenderTarget | TextureUsageShaderRead);
        _hybridGiDenoiseTexture = CreateTexture2D(PixelFormatRgba16Float, _hybridGiWidth, _hybridGiHeight, TextureUsageRenderTarget | TextureUsageShaderRead);
        for (int i = 0; i < BloomMipCount; i++)
            _bloomTextures[i] = CreateTexture2D(PixelFormatRgba16Float, _bloomWidths[i], _bloomHeights[i], TextureUsageRenderTarget | TextureUsageShaderRead);

        ResetHybridGi();

        if (_hdrTexture == IntPtr.Zero || _normalTexture == IntPtr.Zero || _albedoTexture == IntPtr.Zero || _depthTexture == IntPtr.Zero || _aoTexture == IntPtr.Zero || _aoBlurTexture == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate Metal HDR/post textures.");
    }

    public unsafe void Render(double dt, RenderFrameData frame)
    {
        if (!_initialized || _window is null || _queue == IntPtr.Zero || _vertexBuffer == IntPtr.Zero) return;
        Resize(_window.FramebufferSize.X, _window.FramebufferSize.Y);

        FrameMatrices matrices = BuildFrameMatrices(frame);
        int totalVerts = BuildFrameVertices(frame, matrices);
        UploadVertices(totalVerts);

        using var pool = new AutoReleasePool();
        IntPtr drawable = Msg(_layer, "nextDrawable");
        if (drawable == IntPtr.Zero) return;
        IntPtr backBuffer = Msg(drawable, "texture");
        if (backBuffer == IntPtr.Zero) return;

        IntPtr commandBuffer = Msg(_queue, "commandBuffer");
        if (commandBuffer == IntPtr.Zero) return;

        UpdateHybridGiHistory(frame);

        RunShadowPass(commandBuffer);
        RunScenePass(commandBuffer, frame, matrices);
        RunHybridGiPass(commandBuffer, frame, matrices);
        RunSsaoPass(commandBuffer, matrices, frame.Lighting);
        RunBloomPass(commandBuffer);
        RunPostPass(commandBuffer, backBuffer, frame.Lighting);
        RunUiPass(commandBuffer, backBuffer);

        MsgVoid(commandBuffer, "presentDrawable:", drawable);
        MsgVoid(commandBuffer, "commit");

        if (frame.ShowDebug)
        {
            Console.Title = $"Shooter [{Name}] | verts={totalVerts} shadow={_shadowVertexCount} scene={_sceneBatches.Count} gi={_hybridGiSampleCount} overlay={_alphaOverlayBatches.Count + _additiveOverlayBatches.Count} fps={frame.FpsValue:F0}";
        }
    }

    private FrameMatrices BuildFrameMatrices(RenderFrameData frame)
    {
        float aspect = _fbHeight > 0 ? (float)_fbWidth / _fbHeight : 16f / 9f;
        Matrix4x4 view = Matrix4x4.CreateLookAt(frame.Player.EyePosition, frame.Player.EyePosition + frame.Player.Forward(), Vector3.UnitY);
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(WeaponViewmodelRenderer.FovYRadians, aspect, 0.05f, 1000f);
        Matrix4x4 viewProj = view * proj;
        Matrix4x4.Invert(proj, out var invProj);
        Matrix4x4.Invert(viewProj, out var invViewProj);
        Matrix4x4 lightSpace = BuildLightSpace(frame.Player.Position, frame.Lighting);
        Vector3 toSunView = Vector3.Normalize(Vector3.TransformNormal(frame.Lighting.ToSun, view));
        return new FrameMatrices(view, proj, invProj, invViewProj, viewProj, lightSpace, toSunView);
    }

    private int BuildFrameVertices(RenderFrameData frame, FrameMatrices m)
    {
        _sceneBatches.Clear();
        _alphaOverlayBatches.Clear();
        _additiveOverlayBatches.Clear();
        _uiBatches.Clear();

        int estimate = _worldSource.Length * 2 + EstimateModelVerts(frame) + frame.Holes.Count * 6 + frame.Scorches.Count * 6 + 1024;
        EnsureVertexCapacity(Math.Max(estimate, 16384));
        int cursor = 0;

        // Shadow vertices for world only.
        _shadowVertexCount = 0;
        for (int i = 0; i < _worldSource.Length; i++)
        {
            Vector4 clip = Vector4.Transform(new Vector4(_worldSource[i].WorldPos, 1f), m.LightSpace);
            clip = ConvertGlClipToMetal(clip);
            _gpuVertices[cursor++] = new GpuVertex(clip, Vector4.Zero, new Vector4(_worldSource[i].WorldPos, 1f), Vector4.Zero, Vector4.Zero, _worldSource[i].Uv, Vector2.Zero);
            _shadowVertexCount++;
        }

        // World geometry.
        int worldBase = cursor;
        for (int i = 0; i < _worldSource.Length; i++)
        {
            ref readonly var s = ref _worldSource[i];
            Vector4 clip = Vector4.Transform(new Vector4(s.WorldPos, 1f), m.ViewProj);
            clip = ConvertGlClipToMetal(clip);
            _gpuVertices[cursor + i] = new GpuVertex(
                clip,
                new Vector4(s.BaseColor, 1f),
                new Vector4(s.WorldPos, 1f),
                new Vector4(s.WorldNormal, 0f),
                new Vector4(Vector3.Normalize(Vector3.TransformNormal(s.WorldNormal, m.View)), 0f),
                s.Uv,
                Vector2.Zero);
        }
        foreach (var batch in _worldBatches)
        {
            _sceneBatches.Add(new SceneBatch(worldBase + batch.StartVertex, batch.VertexCount,
                batch.BaseColorHandle != IntPtr.Zero ? batch.BaseColorHandle : _whiteTexture,
                batch.NormalHandle != IntPtr.Zero ? batch.NormalHandle : _flatNormalTexture,
                batch.RoughnessHandle != IntPtr.Zero ? batch.RoughnessHandle : _whiteTexture,
                batch.AoHandle != IntPtr.Zero ? batch.AoHandle : _whiteTexture,
                true,
                true,
                false,
                batch.MaterialParams,
                batch.MaterialFx0,
                batch.MaterialFx1));
        }
        cursor += _worldSource.Length;

        // Rockets.
        if (_rocketModel is not null)
        {
            foreach (var r in frame.Rockets.Active)
            {
                Vector3 fwd = r.Forward.LengthSquared() > 1e-6f ? Vector3.Normalize(r.Forward) : Vector3.UnitZ;
                Vector3 worldUp = MathF.Abs(fwd.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX;
                Vector3 right = Vector3.Normalize(Vector3.Cross(worldUp, fwd));
                Vector3 up = Vector3.Cross(fwd, right);
                var rot = new Matrix4x4(
                    right.X, right.Y, right.Z, 0f,
                    up.X, up.Y, up.Z, 0f,
                    -fwd.X, -fwd.Y, -fwd.Z, 0f,
                    0f, 0f, 0f, 1f);
                var model = rot * Matrix4x4.CreateTranslation(r.Position);
                cursor = AppendModel(_rocketModel, model, m.View, m.ViewProj, cursor, _sceneBatches,
                    receiveShadows: true, writeNormal: true, viewSpaceLighting: false,
                    materialParams: new Vector4(0.34f, 0.26f, 0.0f, 1.0f));
            }
        }

        // Viewmodel.
        if (_weaponModels.TryGetValue(frame.Weapons.Current.Def.Kind, out var weaponModel))
        {
            var w = frame.Weapons.Current;
            float kick = 0f;
            if (w.Cooldown > 0 && w.Def.FireRateHz > 0)
            {
                float frac = w.Cooldown * w.Def.FireRateHz;
                kick = MathF.Max(0f, frac) * w.Def.RecoilStrength;
            }
            var pos = WeaponViewmodelRenderer.MuzzleViewOffset + new Vector3(0f, -kick * 0.04f, 0f);
            var rot = Matrix4x4.CreateFromYawPitchRoll(0f, kick * 0.25f, 0f);
            var modelMat = rot * Matrix4x4.CreateTranslation(pos);
            cursor = AppendViewModel(weaponModel, modelMat, m.Proj, cursor);
        }

        // Decals / scorches / particles / muzzle flash.
        cursor = AppendBulletHoles(frame.Holes, m.ViewProj, cursor);
        cursor = AppendScorches(frame.Scorches, m.ViewProj, cursor);
        cursor = AppendParticles(frame.Particles, frame.Player.Right(), Vector3.Normalize(Vector3.Cross(frame.Player.Right(), frame.Player.Forward())), m.ViewProj, cursor);
        cursor = AppendMuzzleFlash(frame.MuzzleFlash, cursor);
        cursor = AppendHud(frame, cursor);
        return cursor;
    }

    private int AppendModel(MetalModel model, Matrix4x4 modelMat, Matrix4x4 view, Matrix4x4 viewProj, int cursor,
        List<SceneBatch> batches, bool receiveShadows, bool writeNormal, bool viewSpaceLighting, Vector4 materialParams)
    {
        Matrix4x4.Invert(modelMat, out var inv);
        Matrix4x4 normalMat = Matrix4x4.Transpose(inv);
        foreach (var prim in model.Primitives)
        {
            int start = cursor;
            foreach (var s in prim.Vertices)
            {
                Vector3 worldPos = Vector3.Transform(s.Position, modelMat);
                Vector3 worldNormal = Vector3.Normalize(Vector3.TransformNormal(s.Normal, normalMat));
                Vector3 viewNormal = Vector3.Normalize(Vector3.TransformNormal(worldNormal, view));
                Vector4 clip = Vector4.Transform(new Vector4(worldPos, 1f), viewProj);
                clip = ConvertGlClipToMetal(clip);
                _gpuVertices[cursor++] = new GpuVertex(
                    clip,
                    s.Color,
                    new Vector4(worldPos, 1f),
                    new Vector4(worldNormal, 0f),
                    new Vector4(viewNormal, 0f),
                    s.Uv,
                    Vector2.Zero);
            }
            batches.Add(new SceneBatch(start, cursor - start,
                prim.TextureHandle != IntPtr.Zero ? prim.TextureHandle : _whiteTexture,
                _flatNormalTexture,
                _whiteTexture,
                _whiteTexture,
                receiveShadows, writeNormal, viewSpaceLighting, materialParams,
                new Vector4(0f, 0f, 1f, 0f), Vector4.Zero));
        }
        return cursor;
    }

    private int AppendViewModel(MetalModel model, Matrix4x4 modelMat, Matrix4x4 proj, int cursor)
    {
        Matrix4x4.Invert(modelMat, out var inv);
        Matrix4x4 normalMat = Matrix4x4.Transpose(inv);
        foreach (var prim in model.Primitives)
        {
            int start = cursor;
            foreach (var s in prim.Vertices)
            {
                Vector3 viewPos = Vector3.Transform(s.Position, modelMat);
                Vector3 viewNormal = Vector3.Normalize(Vector3.TransformNormal(s.Normal, normalMat));
                Vector4 clip = Vector4.Transform(new Vector4(viewPos, 1f), proj);
                clip = ConvertGlClipToMetal(clip);
                _gpuVertices[cursor++] = new GpuVertex(
                    clip,
                    s.Color,
                    new Vector4(viewPos, 1f),
                    new Vector4(viewNormal, 0f),
                    new Vector4(viewNormal, 0f),
                    s.Uv,
                    Vector2.Zero);
            }
            _sceneBatches.Add(new SceneBatch(start, cursor - start,
                prim.TextureHandle != IntPtr.Zero ? prim.TextureHandle : _whiteTexture,
                _flatNormalTexture,
                _whiteTexture,
                _whiteTexture,
                false, false, true, new Vector4(0.28f, 0.24f, 0.0f, 0.0f),
                new Vector4(0f, 0f, 1f, 0f), Vector4.Zero));
        }
        return cursor;
    }

    private int AppendBulletHoles(BulletHoleManager holes, Matrix4x4 viewProj, int cursor)
    {
        if (holes.Count == 0) return cursor;
        int start = cursor;
        float s = BulletHoleManager.QuadHalfSize;
        foreach (var h in holes.Holes)
        {
            Vector3 n = h.Normal;
            Vector3 up = MathF.Abs(n.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 right = Vector3.Normalize(Vector3.Cross(n, up));
            up = Vector3.Cross(right, n);
            Vector3 p = h.Position;
            Vector3 v00 = p - right * s - up * s;
            Vector3 v10 = p + right * s - up * s;
            Vector3 v11 = p + right * s + up * s;
            Vector3 v01 = p - right * s + up * s;
            cursor = AddOverlayQuad(cursor, viewProj, v00, v10, v11, v01, new Vector4(0.025f, 0.018f, 0.015f, 0.95f), 1f, OverlayMode.Decal);
        }
        _alphaOverlayBatches.Add(new OverlayBatch(start, cursor - start, OverlayMode.Decal, true));
        return cursor;
    }

    private int AppendScorches(ScorchManager scorches, Matrix4x4 viewProj, int cursor)
    {
        if (scorches.Count == 0) return cursor;
        int start = cursor;
        foreach (var s in scorches.Scorches)
        {
            Vector3 n = s.Normal;
            Vector3 up = MathF.Abs(n.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 right = Vector3.Normalize(Vector3.Cross(n, up));
            up = Vector3.Cross(right, n);
            float h = s.HalfSize;
            Vector3 p = s.Position;
            Vector3 v00 = p - right * h - up * h;
            Vector3 v10 = p + right * h - up * h;
            Vector3 v11 = p + right * h + up * h;
            Vector3 v01 = p - right * h + up * h;
            cursor = AddOverlayQuad(cursor, viewProj, v00, v10, v11, v01, new Vector4(1f, 1f, 1f, 1f), s.Seed, OverlayMode.Scorch);
        }
        _alphaOverlayBatches.Add(new OverlayBatch(start, cursor - start, OverlayMode.Scorch, true));
        return cursor;
    }

    private int AppendParticles(ParticleSystem particles, Vector3 cameraRight, Vector3 cameraUp, Matrix4x4 viewProj, int cursor)
    {
        if (particles.Active.Count == 0) return cursor;
        int start = cursor;
        foreach (var p in particles.Active)
        {
            float life = p.Lifetime > 0f ? Math.Clamp(1f - p.Age / p.Lifetime, 0f, 1f) : 0f;
            float alpha = p.Color.W * life;
            float size = p.Size * ((p.Kind == ParticleKind.Smoke || p.Kind == ParticleKind.Dust) ? (1.0f + (1f - life) * 0.55f) : 1.0f);
            Vector3 right = cameraRight * size;
            Vector3 up = cameraUp * size;
            Vector3 v00 = p.Position - right - up;
            Vector3 v10 = p.Position + right - up;
            Vector3 v11 = p.Position + right + up;
            Vector3 v01 = p.Position - right + up;
            cursor = AddOverlayQuad(cursor, viewProj, v00, v10, v11, v01, new Vector4(p.Color.X, p.Color.Y, p.Color.Z, alpha), 0f, OverlayMode.Particle);
        }
        _alphaOverlayBatches.Add(new OverlayBatch(start, cursor - start, OverlayMode.Particle, true));
        return cursor;
    }

    private int AppendMuzzleFlash(MuzzleFlash? flash, int cursor)
    {
        if (flash is null || !flash.IsActive) return cursor;
        float aspect = _fbHeight > 0 ? (float)_fbWidth / _fbHeight : 16f / 9f;
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(WeaponViewmodelRenderer.FovYRadians, aspect, 0.01f, 10f);
        float t = flash.Intensity;
        float pulse = 0.55f + 0.45f * t;
        float halfSize = 0.18f * flash.SeedScale * flash.WeaponScale * pulse;
        int start = cursor;
        cursor = AddMuzzleQuad(cursor, proj, flash.ViewOffset, halfSize, halfSize, flash.SeedAngle, t);
        cursor = AddMuzzleQuad(cursor, proj, flash.ViewOffset, halfSize * 0.65f, halfSize * 0.65f, flash.SeedAngle + MathF.PI * 0.5f, t);
        _additiveOverlayBatches.Add(new OverlayBatch(start, cursor - start, OverlayMode.MuzzleFlash, false));
        return cursor;
    }

    private int AddOverlayQuad(int cursor, Matrix4x4 viewProj, Vector3 v00, Vector3 v10, Vector3 v11, Vector3 v01, Vector4 color, float misc, OverlayMode mode)
    {
        cursor = AddOverlayVertex(cursor, viewProj, v00, new Vector2(-1f, -1f), color, misc);
        cursor = AddOverlayVertex(cursor, viewProj, v10, new Vector2(1f, -1f), color, misc);
        cursor = AddOverlayVertex(cursor, viewProj, v11, new Vector2(1f, 1f), color, misc);
        cursor = AddOverlayVertex(cursor, viewProj, v00, new Vector2(-1f, -1f), color, misc);
        cursor = AddOverlayVertex(cursor, viewProj, v11, new Vector2(1f, 1f), color, misc);
        cursor = AddOverlayVertex(cursor, viewProj, v01, new Vector2(-1f, 1f), color, misc);
        return cursor;
    }

    private int AddMuzzleQuad(int cursor, Matrix4x4 proj, Vector3 center, float hx, float hy, float rotation, float intensity)
    {
        float c = MathF.Cos(rotation);
        float s = MathF.Sin(rotation);
        Vector2[] baseQuad = [new(-1f, -1f), new(1f, -1f), new(1f, 1f), new(-1f, 1f)];
        int[] idx = [0, 1, 2, 0, 2, 3];
        foreach (int i in idx)
        {
            Vector2 q = baseQuad[i];
            Vector2 rp = new(q.X * c - q.Y * s, q.X * s + q.Y * c);
            Vector3 viewPos = center + new Vector3(rp.X * hx, rp.Y * hy, 0f);
            Vector4 clip = Vector4.Transform(new Vector4(viewPos, 1f), proj);
            clip = ConvertGlClipToMetal(clip);
            _gpuVertices[cursor++] = new GpuVertex(clip, new Vector4(1f, 1f, 1f, intensity), new Vector4(viewPos, 1f), Vector4.Zero, Vector4.Zero, q, new Vector2(intensity, 0f));
        }
        return cursor;
    }

    private int AddOverlayVertex(int cursor, Matrix4x4 viewProj, Vector3 worldPos, Vector2 uv, Vector4 color, float misc)
    {
        Vector4 clip = Vector4.Transform(new Vector4(worldPos, 1f), viewProj);
        clip = ConvertGlClipToMetal(clip);
        _gpuVertices[cursor++] = new GpuVertex(clip, color, new Vector4(worldPos, 1f), Vector4.Zero, Vector4.Zero, uv, new Vector2(misc, 0f));
        return cursor;
    }

    private int AppendHud(RenderFrameData frame, int cursor)
    {
        float pxX = 2f / Math.Max(1, _fbWidth);
        float pxY = 2f / Math.Max(1, _fbHeight);

        static Vector4 C(float r, float g, float b, float a) => new(r, g, b, a);
        void AddRect(float x0, float y0, float x1, float y1, Vector4 color)
        {
            int start = cursor;
            AddUiVertex(new Vector4(x0, y0, 0f, 1f), color);
            AddUiVertex(new Vector4(x1, y0, 0f, 1f), color);
            AddUiVertex(new Vector4(x1, y1, 0f, 1f), color);
            AddUiVertex(new Vector4(x0, y0, 0f, 1f), color);
            AddUiVertex(new Vector4(x1, y1, 0f, 1f), color);
            AddUiVertex(new Vector4(x0, y1, 0f, 1f), color);
            _uiBatches.Add(new UiBatch(start, 6));
        }

        void AddUiVertex(Vector4 clip, Vector4 color)
        {
            _gpuVertices[cursor++] = new GpuVertex(ConvertGlClipToMetal(clip), color, Vector4.Zero, Vector4.Zero, Vector4.Zero, Vector2.Zero, Vector2.Zero);
        }

        const int armLen = 8, armThick = 2, gap = 4;
        var white = C(1f, 1f, 1f, 0.9f);
        AddRect(-armLen * pxX, armThick * pxY * 0.5f, -gap * pxX, -armThick * pxY * 0.5f, white);
        AddRect(gap * pxX, armThick * pxY * 0.5f, armLen * pxX, -armThick * pxY * 0.5f, white);
        AddRect(-armThick * pxX * 0.5f, -gap * pxY, armThick * pxX * 0.5f, -armLen * pxY, white);
        AddRect(-armThick * pxX * 0.5f, armLen * pxY, armThick * pxX * 0.5f, gap * pxY, white);

        float barW = 280 * pxX, barH = 22 * pxY;
        float barX = -1f + 16 * pxX, barY = -1f + 16 * pxY;
        AddRect(barX - 2 * pxX, barY + barH + 2 * pxY, barX + barW + 2 * pxX, barY - 2 * pxY, C(0.05f, 0.05f, 0.05f, 0.7f));
        float frac = frame.Player.MaxHealth > 0 ? (float)frame.Player.Health / frame.Player.MaxHealth : 0f;
        AddRect(barX, barY + barH, barX + barW * Math.Clamp(frac, 0, 1), barY, C(0.85f, 0.15f, 0.15f, 0.95f));

        var w = frame.Weapons.Current;
        float aBarW = 220 * pxX, aBarH = 18 * pxY;
        float aX = 1f - 16 * pxX - aBarW, aY = -1f + 16 * pxY;
        AddRect(aX - 2 * pxX, aY + aBarH + 2 * pxY, aX + aBarW + 2 * pxX, aY - 2 * pxY, C(0.05f, 0.05f, 0.05f, 0.7f));
        float aFrac = w.Def.InfiniteAmmo ? 1f : (w.Def.AmmoMax > 0 ? (float)w.Ammo / w.Def.AmmoMax : 0f);
        AddRect(aX, aY + aBarH, aX + aBarW * Math.Clamp(aFrac, 0, 1), aY, C(0.95f, 0.85f, 0.20f, 0.95f));

        float slot = 28 * pxX, slotY = 28 * pxY, sX = -1f + 16 * pxX, sY = 1f - 16 * pxY - slotY;
        for (int i = 0; i < frame.Weapons.Weapons.Count; i++)
        {
            float x = sX + i * (slot + 6 * pxX);
            var ws = frame.Weapons.Weapons[i];
            var color = i == frame.Weapons.CurrentIndex ? C(0.9f, 0.9f, 0.3f, 0.95f)
                : ws.Owned ? C(0.7f, 0.7f, 0.7f, 0.85f)
                : C(0.25f, 0.25f, 0.25f, 0.6f);
            AddRect(x, sY + slotY, x + slot, sY, color);
        }

        return cursor;
    }

    private int EstimateModelVerts(RenderFrameData frame)
    {
        int total = 0;
        if (_rocketModel is not null)
            foreach (var p in _rocketModel.Primitives)
                total += p.Vertices.Length * frame.Rockets.Active.Count;
        if (_weaponModels.TryGetValue(frame.Weapons.Current.Def.Kind, out var weapon))
            foreach (var p in weapon.Primitives)
                total += p.Vertices.Length;
        return total;
    }

    private void RunShadowPass(IntPtr commandBuffer)
    {
        if (_shadowPipeline == IntPtr.Zero || _shadowTexture == IntPtr.Zero || _shadowVertexCount <= 0) return;
        IntPtr pass = CreateDepthOnlyRenderPass(_shadowTexture, clearDepth: true);
        IntPtr encoder = Msg(commandBuffer, "renderCommandEncoderWithDescriptor:", pass);
        if (encoder == IntPtr.Zero) return;
        MsgVoid(encoder, "setRenderPipelineState:", _shadowPipeline);
        if (_depthStateWriteLess != IntPtr.Zero) MsgVoid(encoder, "setDepthStencilState:", _depthStateWriteLess);
        MsgVoid(encoder, "setVertexBuffer:offset:atIndex:", _vertexBuffer, 0UL, 0UL);
        MsgVoid(encoder, "drawPrimitives:vertexStart:vertexCount:", PrimitiveTypeTriangle, 0UL, (ulong)_shadowVertexCount);
        MsgVoid(encoder, "endEncoding");
    }

    private unsafe void RunScenePass(IntPtr commandBuffer, RenderFrameData frame, FrameMatrices m)
    {
        if (_scenePipeline == IntPtr.Zero) return;
        IntPtr pass = CreateSceneRenderPass();
        IntPtr encoder = Msg(commandBuffer, "renderCommandEncoderWithDescriptor:", pass);
        if (encoder == IntPtr.Zero) return;

        MsgVoid(encoder, "setRenderPipelineState:", _scenePipeline);
        if (_depthStateWriteLess != IntPtr.Zero) MsgVoid(encoder, "setDepthStencilState:", _depthStateWriteLess);
        MsgVoid(encoder, "setVertexBuffer:offset:atIndex:", _vertexBuffer, 0UL, 0UL);
        MsgVoid(encoder, "setFragmentSamplerState:atIndex:", _linearClampSampler, 0UL);
        MsgVoid(encoder, "setFragmentSamplerState:atIndex:", _linearClampSampler, 1UL);

        var frameUniforms = BuildSceneFrameUniforms(frame, m);
        SetFragmentBytes(encoder, ref frameUniforms, 0UL);
        MsgVoid(encoder, "setFragmentTexture:atIndex:", _shadowTexture, 1UL);

        foreach (var batch in _sceneBatches)
        {
            var draw = new SceneDrawUniforms
            {
                ReceiveShadows = batch.ReceiveShadows ? 1u : 0u,
                WriteNormal = batch.WriteNormal ? 1u : 0u,
                ViewSpaceLighting = batch.ViewSpaceLighting ? 1u : 0u,
                Pad = 0u,
                MaterialParams = batch.MaterialParams,
                MaterialFx0 = batch.MaterialFx0,
                MaterialFx1 = batch.MaterialFx1,
            };
            SetFragmentBytes(encoder, ref draw, 1UL);
            MsgVoid(encoder, "setFragmentTexture:atIndex:", batch.BaseColorHandle != IntPtr.Zero ? batch.BaseColorHandle : _whiteTexture, 0UL);
            MsgVoid(encoder, "setFragmentTexture:atIndex:", batch.NormalHandle != IntPtr.Zero ? batch.NormalHandle : _flatNormalTexture, 2UL);
            MsgVoid(encoder, "setFragmentTexture:atIndex:", batch.RoughnessHandle != IntPtr.Zero ? batch.RoughnessHandle : _whiteTexture, 3UL);
            MsgVoid(encoder, "setFragmentTexture:atIndex:", batch.AoHandle != IntPtr.Zero ? batch.AoHandle : _whiteTexture, 4UL);
            MsgVoid(encoder, "drawPrimitives:vertexStart:vertexCount:", PrimitiveTypeTriangle, (ulong)batch.StartVertex, (ulong)batch.VertexCount);
        }

        DrawOverlayBatches(encoder, _alphaOverlayBatches, _alphaOverlayPipeline, depthEnabled: true);
        DrawOverlayBatches(encoder, _additiveOverlayBatches, _additiveOverlayPipeline, depthEnabled: false);
        MsgVoid(encoder, "endEncoding");
    }

    private void DrawOverlayBatches(IntPtr encoder, List<OverlayBatch> batches, IntPtr pipeline, bool depthEnabled)
    {
        if (pipeline == IntPtr.Zero || batches.Count == 0) return;
        MsgVoid(encoder, "setRenderPipelineState:", pipeline);
        MsgVoid(encoder, "setVertexBuffer:offset:atIndex:", _vertexBuffer, 0UL, 0UL);
        MsgVoid(encoder, "setDepthStencilState:", depthEnabled ? _depthStateReadLessEqual : _depthStateDisabled);
        foreach (var batch in batches)
        {
            MsgVoid(encoder, "setDepthStencilState:", batch.DepthTest ? _depthStateReadLessEqual : _depthStateDisabled);
            uint mode = batch.Mode switch
            {
                OverlayMode.Decal => 1u,
                OverlayMode.Scorch => 2u,
                OverlayMode.MuzzleFlash => 3u,
                OverlayMode.Particle => 4u,
                _ => 0u,
            };
            var draw = new OverlayDrawUniforms { Mode = mode };
            SetFragmentBytes(encoder, ref draw, 0UL);
            MsgVoid(encoder, "drawPrimitives:vertexStart:vertexCount:", PrimitiveTypeTriangle, (ulong)batch.StartVertex, (ulong)batch.VertexCount);
        }
    }

    private void UpdateHybridGiHistory(RenderFrameData frame)
    {
        if (!frame.Lighting.HybridPathTracingEnabled)
        {
            ResetHybridGi();
            return;
        }

        Vector3 camPos = frame.Player.EyePosition;
        Vector3 camForward = frame.Player.Forward();
        bool changed = !_hybridGiHasHistory
            || Vector3.DistanceSquared(camPos, _hybridGiLastCamPos) > 0.0004f
            || Vector3.DistanceSquared(camForward, _hybridGiLastForward) > 0.0001f;
        if (changed)
            ResetHybridGi();

        _hybridGiLastCamPos = camPos;
        _hybridGiLastForward = camForward;
        _hybridGiHasHistory = true;
    }

    private void ResetHybridGi()
    {
        _hybridGiSampleCount = 0;
        _hybridGiHistoryIndex = 0;
        _hybridGiHasHistory = false;
    }

    private void RunHybridGiPass(IntPtr commandBuffer, RenderFrameData frame, FrameMatrices m)
    {
        if (!frame.Lighting.HybridPathTracingEnabled || _hybridGiPipeline == IntPtr.Zero || _worldTriangleBuffer == IntPtr.Zero || _worldTriangleCount == 0)
            return;
        int writeIndex = 1 - _hybridGiHistoryIndex;
        var u = new HybridGiUniforms
        {
            InvViewProj = m.InvViewProj,
            CameraPos = new Vector4(frame.Player.EyePosition, 0f),
            ToSun = new Vector4(frame.Lighting.ToSun, 0f),
            SkyZenith = new Vector4(0.32f, 0.55f, 1.05f, 0f),
            SkyHorizon = new Vector4(0.95f, 0.85f, 0.75f, 0f),
            GroundColor = new Vector4(frame.Lighting.GroundAlbedo, 0f),
            Params0 = new Vector4(frame.Lighting.Turbidity, _hybridGiSampleCount, frame.Lighting.HybridPathTracingStrength, _worldTriangleCount),
        };

        IntPtr pass = CreateColorRenderPass(_hybridGiTextures[writeIndex], clearColor: _hybridGiSampleCount == 0, clear: MetalNative.CreateClearColor(0, 0, 0, 1));
        IntPtr encoder = Msg(commandBuffer, "renderCommandEncoderWithDescriptor:", pass);
        if (encoder == IntPtr.Zero) return;
        MsgVoid(encoder, "setRenderPipelineState:", _hybridGiPipeline);
        MsgVoid(encoder, "setFragmentSamplerState:atIndex:", _linearClampSampler, 0UL);
        MsgVoid(encoder, "setFragmentTexture:atIndex:", _depthTexture, 0UL);
        MsgVoid(encoder, "setFragmentTexture:atIndex:", _normalTexture, 1UL);
        MsgVoid(encoder, "setFragmentTexture:atIndex:", _albedoTexture, 2UL);
        MsgVoid(encoder, "setFragmentTexture:atIndex:", _hybridGiTextures[_hybridGiHistoryIndex], 3UL);
        SetFragmentBytes(encoder, ref u, 0UL);
        MsgVoid(encoder, "setFragmentBuffer:offset:atIndex:", _worldTriangleBuffer, 0UL, 1UL);
        MsgVoid(encoder, "drawPrimitives:vertexStart:vertexCount:", PrimitiveTypeTriangle, 0UL, 3UL);
        MsgVoid(encoder, "endEncoding");

        IntPtr blurPass = CreateColorRenderPass(_hybridGiDenoiseTexture, clearColor: true, clear: MetalNative.CreateClearColor(0, 0, 0, 1));
        IntPtr blurEncoder = Msg(commandBuffer, "renderCommandEncoderWithDescriptor:", blurPass);
        if (blurEncoder != IntPtr.Zero)
        {
            MsgVoid(blurEncoder, "setRenderPipelineState:", _hybridGiBlurPipeline);
            MsgVoid(blurEncoder, "setFragmentSamplerState:atIndex:", _linearClampSampler, 0UL);
            MsgVoid(blurEncoder, "setFragmentTexture:atIndex:", _hybridGiTextures[writeIndex], 0UL);
            MsgVoid(blurEncoder, "setFragmentTexture:atIndex:", _depthTexture, 1UL);
            MsgVoid(blurEncoder, "setFragmentTexture:atIndex:", _normalTexture, 2UL);
            MsgVoid(blurEncoder, "drawPrimitives:vertexStart:vertexCount:", PrimitiveTypeTriangle, 0UL, 3UL);
            MsgVoid(blurEncoder, "endEncoding");
        }

        _hybridGiHistoryIndex = writeIndex;
        _hybridGiSampleCount = Math.Min(_hybridGiSampleCount + 1, 4096);
    }

    private unsafe void RunSsaoPass(IntPtr commandBuffer, FrameMatrices m, LightingEnvironment lighting)
    {
        if (_ssaoPipeline == IntPtr.Zero || _ssaoBlurPipeline == IntPtr.Zero || _aoTexture == IntPtr.Zero || _aoBlurTexture == IntPtr.Zero || _depthTexture == IntPtr.Zero || _normalTexture == IntPtr.Zero)
            return;

        var ssao = BuildSsaoUniforms(m, lighting);

        IntPtr aoPass = CreateColorRenderPass(_aoTexture, clearColor: true, clear: MetalNative.CreateClearColor(1, 1, 1, 1));
        IntPtr aoEncoder = Msg(commandBuffer, "renderCommandEncoderWithDescriptor:", aoPass);
        if (aoEncoder != IntPtr.Zero)
        {
            MsgVoid(aoEncoder, "setRenderPipelineState:", _ssaoPipeline);
            MsgVoid(aoEncoder, "setFragmentSamplerState:atIndex:", _linearClampSampler, 0UL);
            MsgVoid(aoEncoder, "setFragmentSamplerState:atIndex:", _linearRepeatSampler, 1UL);
            MsgVoid(aoEncoder, "setFragmentTexture:atIndex:", _depthTexture, 0UL);
            MsgVoid(aoEncoder, "setFragmentTexture:atIndex:", _normalTexture, 1UL);
            MsgVoid(aoEncoder, "setFragmentTexture:atIndex:", _noiseTexture, 2UL);
            SetFragmentBytes(aoEncoder, ref ssao, 0UL);
            MsgVoid(aoEncoder, "drawPrimitives:vertexStart:vertexCount:", PrimitiveTypeTriangle, 0UL, 3UL);
            MsgVoid(aoEncoder, "endEncoding");
        }

        var blur = new SsaoBlurUniforms { TexelSize = new Vector4(1f / _fbWidth, 1f / _fbHeight, 0f, 0f) };
        IntPtr blurPass = CreateColorRenderPass(_aoBlurTexture, clearColor: true, clear: MetalNative.CreateClearColor(1, 1, 1, 1));
        IntPtr blurEncoder = Msg(commandBuffer, "renderCommandEncoderWithDescriptor:", blurPass);
        if (blurEncoder != IntPtr.Zero)
        {
            MsgVoid(blurEncoder, "setRenderPipelineState:", _ssaoBlurPipeline);
            MsgVoid(blurEncoder, "setFragmentSamplerState:atIndex:", _linearClampSampler, 0UL);
            MsgVoid(blurEncoder, "setFragmentTexture:atIndex:", _aoTexture, 0UL);
            MsgVoid(blurEncoder, "setFragmentTexture:atIndex:", _depthTexture, 1UL);
            SetFragmentBytes(blurEncoder, ref blur, 0UL);
            MsgVoid(blurEncoder, "drawPrimitives:vertexStart:vertexCount:", PrimitiveTypeTriangle, 0UL, 3UL);
            MsgVoid(blurEncoder, "endEncoding");
        }
    }

    private void RunBloomPass(IntPtr commandBuffer)
    {
        if (_hdrTexture == IntPtr.Zero || _bloomTextures[0] == IntPtr.Zero) return;

        IntPtr pass = CreateColorRenderPass(_bloomTextures[0], clearColor: true, clear: MetalNative.CreateClearColor(0, 0, 0, 1));
        IntPtr encoder = Msg(commandBuffer, "renderCommandEncoderWithDescriptor:", pass);
        if (encoder != IntPtr.Zero)
        {
            MsgVoid(encoder, "setRenderPipelineState:", _bloomThresholdPipeline);
            MsgVoid(encoder, "setFragmentSamplerState:atIndex:", _linearClampSampler, 0UL);
            MsgVoid(encoder, "setFragmentTexture:atIndex:", _hdrTexture, 0UL);
            MsgVoid(encoder, "drawPrimitives:vertexStart:vertexCount:", PrimitiveTypeTriangle, 0UL, 3UL);
            MsgVoid(encoder, "endEncoding");
        }

        for (int i = 1; i < BloomMipCount; i++)
        {
            var u = new BloomTexelUniforms { TexelSize = new Vector4(1f / _bloomWidths[i - 1], 1f / _bloomHeights[i - 1], 0f, 0f) };
            pass = CreateColorRenderPass(_bloomTextures[i], clearColor: true, clear: MetalNative.CreateClearColor(0, 0, 0, 1));
            encoder = Msg(commandBuffer, "renderCommandEncoderWithDescriptor:", pass);
            if (encoder == IntPtr.Zero) continue;
            MsgVoid(encoder, "setRenderPipelineState:", _bloomDownPipeline);
            MsgVoid(encoder, "setFragmentSamplerState:atIndex:", _linearClampSampler, 0UL);
            MsgVoid(encoder, "setFragmentTexture:atIndex:", _bloomTextures[i - 1], 0UL);
            SetFragmentBytes(encoder, ref u, 0UL);
            MsgVoid(encoder, "drawPrimitives:vertexStart:vertexCount:", PrimitiveTypeTriangle, 0UL, 3UL);
            MsgVoid(encoder, "endEncoding");
        }

        for (int i = BloomMipCount - 1; i >= 1; i--)
        {
            var u = new BloomTexelUniforms { TexelSize = new Vector4(1f / _bloomWidths[i], 1f / _bloomHeights[i], 1f, 0f) };
            pass = CreateColorRenderPass(_bloomTextures[i - 1], clearColor: false, clear: default);
            encoder = Msg(commandBuffer, "renderCommandEncoderWithDescriptor:", pass);
            if (encoder == IntPtr.Zero) continue;
            MsgVoid(encoder, "setRenderPipelineState:", _bloomUpPipeline);
            MsgVoid(encoder, "setFragmentSamplerState:atIndex:", _linearClampSampler, 0UL);
            MsgVoid(encoder, "setFragmentTexture:atIndex:", _bloomTextures[i], 0UL);
            SetFragmentBytes(encoder, ref u, 0UL);
            MsgVoid(encoder, "drawPrimitives:vertexStart:vertexCount:", PrimitiveTypeTriangle, 0UL, 3UL);
            MsgVoid(encoder, "endEncoding");
        }
    }

    private void RunPostPass(IntPtr commandBuffer, IntPtr drawableTexture, LightingEnvironment lighting)
    {
        if (_postPipeline == IntPtr.Zero) return;
        var post = new PostUniforms
        {
            Params = new Vector4(lighting.Exposure, lighting.BloomStrength, lighting.SsaoStrength, lighting.GradeContrast),
            Grade = new Vector4(lighting.GradeSaturation, lighting.GradeShadowCool, lighting.GradeHighlightWarm, lighting.VignetteStrength),
            Hybrid = new Vector4(lighting.HybridPathTracingEnabled ? lighting.HybridPathTracingStrength : 0f, 0f, 0f, 0f),
        };
        IntPtr pass = CreateColorRenderPass(drawableTexture, clearColor: true, clear: MetalNative.CreateClearColor(0.08, 0.10, 0.12, 1.0));
        IntPtr encoder = Msg(commandBuffer, "renderCommandEncoderWithDescriptor:", pass);
        if (encoder == IntPtr.Zero) return;
        MsgVoid(encoder, "setRenderPipelineState:", _postPipeline);
        MsgVoid(encoder, "setFragmentSamplerState:atIndex:", _linearClampSampler, 0UL);
        MsgVoid(encoder, "setFragmentTexture:atIndex:", _hdrTexture, 0UL);
        MsgVoid(encoder, "setFragmentTexture:atIndex:", _bloomTextures[0], 1UL);
        MsgVoid(encoder, "setFragmentTexture:atIndex:", _aoBlurTexture, 2UL);
        MsgVoid(encoder, "setFragmentTexture:atIndex:", _hybridGiDenoiseTexture != IntPtr.Zero ? _hybridGiDenoiseTexture : _hybridGiTextures[_hybridGiHistoryIndex], 3UL);
        SetFragmentBytes(encoder, ref post, 0UL);
        MsgVoid(encoder, "drawPrimitives:vertexStart:vertexCount:", PrimitiveTypeTriangle, 0UL, 3UL);
        MsgVoid(encoder, "endEncoding");
    }

    private void RunUiPass(IntPtr commandBuffer, IntPtr drawableTexture)
    {
        if (_uiPipeline == IntPtr.Zero || _uiBatches.Count == 0) return;
        IntPtr pass = CreateColorRenderPass(drawableTexture, clearColor: false, clear: default);
        IntPtr encoder = Msg(commandBuffer, "renderCommandEncoderWithDescriptor:", pass);
        if (encoder == IntPtr.Zero) return;
        MsgVoid(encoder, "setRenderPipelineState:", _uiPipeline);
        MsgVoid(encoder, "setVertexBuffer:offset:atIndex:", _vertexBuffer, 0UL, 0UL);
        foreach (var batch in _uiBatches)
            MsgVoid(encoder, "drawPrimitives:vertexStart:vertexCount:", PrimitiveTypeTriangle, (ulong)batch.StartVertex, (ulong)batch.VertexCount);
        MsgVoid(encoder, "endEncoding");
    }

    private SceneFrameUniforms BuildSceneFrameUniforms(RenderFrameData frame, FrameMatrices m)
    {
        Vector3 sunDir = Vector3.Normalize(frame.Lighting.SunDirection);
        Vector3 skyColor = new(0.32f, 0.55f, 1.05f);
        return new SceneFrameUniforms
        {
            SunDir = new Vector4(sunDir, 0f),
            ToSunView = new Vector4(m.ToSunView, 0f),
            SunColor = new Vector4(frame.Lighting.SunColor, 0f),
            SkyColor = new Vector4(skyColor, 0f),
            GroundColor = new Vector4(frame.Lighting.GroundAlbedo, 0f),
            CameraPos = new Vector4(frame.Player.EyePosition, 0f),
            FogColor = new Vector4(frame.Lighting.FogColor, 0f),
            Params0 = new Vector4(frame.Lighting.SunIntensity, MathF.Max(0.1f, frame.Lighting.IrradianceIntensity), frame.Lighting.ShadowSoftness, 0f),
            Params1 = new Vector4(frame.Lighting.FogDensity, frame.Lighting.FogStart, frame.Lighting.FogHeightFalloff, frame.Lighting.FogBaseHeight),
            Time = new Vector4(Environment.TickCount / 1000f, 0f, 0f, 0f),
            LightSpace = m.LightSpace,
        };
    }

    private unsafe SsaoUniforms BuildSsaoUniforms(FrameMatrices m, LightingEnvironment lighting)
    {
        var u = new SsaoUniforms
        {
            NoiseScaleRadiusBias = new Vector4(_fbWidth / (float)SsaoNoiseSize, _fbHeight / (float)SsaoNoiseSize, lighting.SsaoRadius, lighting.SsaoBias),
            Proj = m.Proj,
            InvProj = m.InvProj,
        };
        for (int i = 0; i < SsaoKernelSize; i++)
        {
            Vector3 s = _ssaoKernel[i];
            u.Samples[i * 4 + 0] = s.X;
            u.Samples[i * 4 + 1] = s.Y;
            u.Samples[i * 4 + 2] = s.Z;
            u.Samples[i * 4 + 3] = 0f;
        }
        return u;
    }

    private readonly Vector3[] _ssaoKernel = BuildSsaoKernel();

    private static Vector3[] BuildSsaoKernel()
    {
        var kernel = new Vector3[SsaoKernelSize];
        var rng = new Random(0xACE);
        for (int i = 0; i < SsaoKernelSize; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1);
            float y = (float)(rng.NextDouble() * 2 - 1);
            float z = (float)rng.NextDouble();
            var v = Vector3.Normalize(new Vector3(x, y, z));
            v *= (float)rng.NextDouble();
            float scale = i / (float)SsaoKernelSize;
            scale = 0.1f + scale * scale * 0.9f;
            kernel[i] = v * scale;
        }
        return kernel;
    }

    private static Matrix4x4 BuildLightSpace(Vector3 playerPos, LightingEnvironment env)
    {
        var sunDir = Vector3.Normalize(env.SunDirection);
        var lightPos = playerPos - sunDir * 20f;
        var up = MathF.Abs(sunDir.Y) > 0.95f ? Vector3.UnitX : Vector3.UnitY;
        var view = Matrix4x4.CreateLookAt(lightPos, lightPos + sunDir, up);
        var center = Vector3.Transform(playerPos, view);
        float texel = (ShadowHalfExtent * 2f) / ShadowMapSize;
        center.X = MathF.Round(center.X / texel) * texel;
        center.Y = MathF.Round(center.Y / texel) * texel;
        var playerView = Vector3.Transform(playerPos, view);
        var snappedView = view * Matrix4x4.CreateTranslation(new Vector3(playerView.X - center.X, playerView.Y - center.Y, 0f) * -1f);
        var proj = Matrix4x4.CreateOrthographicOffCenter(-ShadowHalfExtent, ShadowHalfExtent, -ShadowHalfExtent, ShadowHalfExtent, ShadowNear, ShadowFar);
        return snappedView * proj;
    }

    private void BuildWorldSources(GameWorld world)
    {
        _worldBatches.Clear();
        var list = new List<WorldSourceVertex>(world.AllTriangles.Count * 3);
        foreach (var wb in world.Brushes)
        {
            int start = list.Count;
            IntPtr baseTex = ResolveTexture(wb.TexturePath);
            string? normalPath = MaterialMapConventions.ResolveNormal(wb.TexturePath);
            string? roughPath = MaterialMapConventions.ResolveRoughness(wb.TexturePath);
            string? aoPath = MaterialMapConventions.ResolveAo(wb.TexturePath);
            IntPtr normalTex = string.IsNullOrWhiteSpace(normalPath) ? _flatNormalTexture : ResolveTexture(normalPath);
            IntPtr roughTex = string.IsNullOrWhiteSpace(roughPath) ? _whiteTexture : ResolveTexture(roughPath);
            IntPtr aoTex = string.IsNullOrWhiteSpace(aoPath) ? _whiteTexture : ResolveTexture(aoPath);
            float[] verts = wb.Mesh.Vertices;
            uint[] idx = wb.Mesh.Indices;
            for (int i = 0; i < idx.Length; i++)
            {
                int vi = (int)idx[i] * Mesh.FloatsPerVertex;
                Vector3 localPos = new(verts[vi + 0], verts[vi + 1], verts[vi + 2]);
                Vector3 localNormal = new(verts[vi + 3], verts[vi + 4], verts[vi + 5]);
                Vector2 uv = new(verts[vi + 6], verts[vi + 7]);
                Vector3 worldPos = Vector3.Transform(localPos, wb.Model);
                Vector3 worldNormal = Vector3.Normalize(Vector3.TransformNormal(localNormal, wb.NormalMatrix));
                list.Add(new WorldSourceVertex(worldPos, worldNormal, uv, wb.TintColor, baseTex));
            }
            _worldBatches.Add(new WorldBatch(
                start,
                list.Count - start,
                baseTex,
                normalTex,
                roughTex,
                aoTex,
                new Vector4(wb.Roughness, wb.SpecularStrength, wb.DetailNormalStrength, 1f),
                new Vector4((float)wb.MaterialKind, wb.EmissiveStrength, wb.Opacity, wb.FresnelStrength),
                new Vector4(wb.FlowSpeed.X, wb.FlowSpeed.Y, wb.DistortionStrength, wb.PulseStrength)));
        }
        _worldSource = [.. list];
    }

    private unsafe void BuildWorldTriangleBuffer(GameWorld world)
    {
        _worldTriangleCount = world.AllTriangles.Count;
        if (_worldTriangleCount == 0) return;
        ReleaseIfSet(ref _worldTriangleBuffer);
        ulong size = (ulong)(_worldTriangleCount * sizeof(GpuTriangle));
        _worldTriangleBuffer = Msg(_device, "newBufferWithLength:options:", size, 0UL);
        if (_worldTriangleBuffer == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate Metal world triangle buffer.");
        _worldTriangleBuffer = ObjCRuntime.objc_retain(_worldTriangleBuffer);
        IntPtr contents = Msg(_worldTriangleBuffer, "contents");
        if (contents == IntPtr.Zero)
            throw new InvalidOperationException("Failed to map Metal world triangle buffer.");
        var tris = new GpuTriangle[_worldTriangleCount];
        for (int i = 0; i < _worldTriangleCount; i++)
        {
            var t = world.AllTriangles[i];
            tris[i] = new GpuTriangle(new Vector4(t.V0, 1f), new Vector4(t.V1, 1f), new Vector4(t.V2, 1f), new Vector4(t.Normal, 0f));
        }
        fixed (GpuTriangle* p = tris)
            Buffer.MemoryCopy(p, (void*)contents, (long)size, (long)size);
    }

    private void LoadWeaponModels()
    {
        TryAddWeapon(WeaponKind.Ak47, "ak47.glb");
        TryAddWeapon(WeaponKind.Shotgun, "shotgun.glb");
        TryAddWeapon(WeaponKind.RocketLauncher, "rocketlaucher.glb");
    }

    private void TryAddWeapon(WeaponKind kind, string file)
    {
        var path = Path.Combine(AssetLocator.Root, "StylooGunsAssetPack", "GLB", file);
        var data = ModelData.TryLoad(path)?.AlignBarrelToForward(targetForwardLength: 0.55f);
        if (data is null) return;
        _weaponModels[kind] = BuildMetalModel(data);
    }

    private void LoadRocketModel()
    {
        var path = Path.Combine(AssetLocator.Root, "StylooGunsAssetPack", "GLB", "quadrocket.glb");
        var data = ModelData.TryLoad(path)?.AlignBarrelToForward(targetForwardLength: RocketRenderer.RocketLength);
        if (data is null) return;
        _rocketModel = BuildMetalModel(data);
    }

    private MetalModel BuildMetalModel(ModelData data)
    {
        var prims = new List<MetalPrimitive>(data.Primitives.Count);
        foreach (var p in data.Primitives)
        {
            var verts = new LocalSourceVertex[p.Indices.Length];
            for (int i = 0; i < p.Indices.Length; i++)
            {
                int vi = (int)p.Indices[i] * 8;
                verts[i] = new LocalSourceVertex(
                    new Vector3(p.Vertices[vi + 0], p.Vertices[vi + 1], p.Vertices[vi + 2]),
                    new Vector3(p.Vertices[vi + 3], p.Vertices[vi + 4], p.Vertices[vi + 5]),
                    new Vector2(p.Vertices[vi + 6], p.Vertices[vi + 7]),
                    p.BaseColorFactor);
            }
            IntPtr tex = p.BaseColorImage is { Length: > 0 } ? CreateTextureFromImageBytes(p.BaseColorImage) : _whiteTexture;
            prims.Add(new MetalPrimitive(verts, tex));
        }
        return new MetalModel(prims);
    }

    private IntPtr CreateTextureFromImageBytes(byte[] imageBytes)
    {
        try
        {
            var image = ImageResult.FromMemory(imageBytes, ColorComponents.RedGreenBlueAlpha);
            return CreateTextureFromPixels(image.Width, image.Height, image.Data, PixelFormatRgba8Unorm, TextureUsageShaderRead);
        }
        catch
        {
            return _whiteTexture;
        }
    }

    private IntPtr ResolveTexture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return _whiteTexture;
        if (_textures.TryGetValue(path, out var handle)) return handle;
        try
        {
            using var stream = File.OpenRead(path);
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            handle = CreateTextureFromPixels(image.Width, image.Height, image.Data, PixelFormatRgba8Unorm, TextureUsageShaderRead);
            _textures[path] = handle;
            return handle;
        }
        catch
        {
            return _whiteTexture;
        }
    }

    private IntPtr CreateTexture2D(ulong pixelFormat, int width, int height, ulong usage)
    {
        IntPtr texDescClass = ObjCRuntime.objc_getClass("MTLTextureDescriptor");
        IntPtr desc = Msg(texDescClass, "texture2DDescriptorWithPixelFormat:width:height:mipmapped:", pixelFormat, (ulong)Math.Max(1, width), (ulong)Math.Max(1, height), false);
        MsgVoid(desc, "setUsage:", usage);
        IntPtr texture = Msg(_device, "newTextureWithDescriptor:", desc);
        if (texture == IntPtr.Zero) return IntPtr.Zero;
        return ObjCRuntime.objc_retain(texture);
    }

    private IntPtr CreateTextureFromPixels(int width, int height, byte[] rgba, ulong pixelFormat, ulong usage)
    {
        IntPtr texture = CreateTexture2D(pixelFormat, width, height, usage);
        if (texture == IntPtr.Zero) return IntPtr.Zero;
        IntPtr data = Marshal.AllocHGlobal(rgba.Length);
        try
        {
            Marshal.Copy(rgba, 0, data, rgba.Length);
            var region = new MTLRegion { Origin = new MTLOrigin(), Size = new MTLSize { Width = (ulong)width, Height = (ulong)height, Depth = 1 } };
            MsgVoid(texture, "replaceRegion:mipmapLevel:withBytes:bytesPerRow:", region, 0UL, data, (ulong)(width * 4));
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
        return texture;
    }

    private IntPtr CreateSsaoNoiseTexture()
    {
        var bytes = new byte[SsaoNoiseSize * SsaoNoiseSize * 4];
        var rng = new Random(0xBEE);
        for (int i = 0; i < SsaoNoiseSize * SsaoNoiseSize; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1);
            float y = (float)(rng.NextDouble() * 2 - 1);
            bytes[i * 4 + 0] = (byte)(Math.Clamp(x * 0.5f + 0.5f, 0f, 1f) * 255f);
            bytes[i * 4 + 1] = (byte)(Math.Clamp(y * 0.5f + 0.5f, 0f, 1f) * 255f);
            bytes[i * 4 + 2] = 128;
            bytes[i * 4 + 3] = 255;
        }
        return CreateTextureFromPixels(SsaoNoiseSize, SsaoNoiseSize, bytes, PixelFormatRgba8Unorm, TextureUsageShaderRead);
    }

    private unsafe void EnsureVertexCapacity(int required)
    {
        if (required <= _vertexCapacity && _vertexBuffer != IntPtr.Zero) return;
        _vertexCapacity = Math.Max(required, Math.Max(32768, _vertexCapacity * 2));
        _gpuVertices = new GpuVertex[_vertexCapacity];
        ReleaseIfSet(ref _vertexBuffer);
        ulong size = (ulong)(_vertexCapacity * sizeof(GpuVertex));
        _vertexBuffer = Msg(_device, "newBufferWithLength:options:", size, 0UL);
        if (_vertexBuffer == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate Metal dynamic vertex buffer.");
        _vertexBuffer = ObjCRuntime.objc_retain(_vertexBuffer);
    }

    private unsafe void UploadVertices(int count)
    {
        if (count <= 0) return;
        IntPtr contents = Msg(_vertexBuffer, "contents");
        if (contents == IntPtr.Zero) return;
        fixed (GpuVertex* p = _gpuVertices)
        {
            long bytes = count * sizeof(GpuVertex);
            Buffer.MemoryCopy(p, (void*)contents, bytes, bytes);
        }
    }

    private IntPtr CreateSceneRenderPass()
    {
        IntPtr rpdClass = ObjCRuntime.objc_getClass("MTLRenderPassDescriptor");
        IntPtr pass = Msg(rpdClass, "renderPassDescriptor");
        IntPtr colors = Msg(pass, "colorAttachments");
        IntPtr color0 = Msg(colors, "objectAtIndexedSubscript:", 0UL);
        MsgVoid(color0, "setTexture:", _hdrTexture);
        MsgVoid(color0, "setLoadAction:", 2UL);
        MsgVoid(color0, "setStoreAction:", 1UL);
        MsgVoid(color0, "setClearColor:", MetalNative.CreateClearColor(0.18, 0.24, 0.34, 1.0));

        IntPtr color1 = Msg(colors, "objectAtIndexedSubscript:", 1UL);
        MsgVoid(color1, "setTexture:", _normalTexture);
        MsgVoid(color1, "setLoadAction:", 2UL);
        MsgVoid(color1, "setStoreAction:", 1UL);
        MsgVoid(color1, "setClearColor:", MetalNative.CreateClearColor(0.0, 0.0, 0.0, 0.0));

        IntPtr color2 = Msg(colors, "objectAtIndexedSubscript:", 2UL);
        MsgVoid(color2, "setTexture:", _albedoTexture);
        MsgVoid(color2, "setLoadAction:", 2UL);
        MsgVoid(color2, "setStoreAction:", 1UL);
        MsgVoid(color2, "setClearColor:", MetalNative.CreateClearColor(0.0, 0.0, 0.0, 0.0));

        IntPtr depth = Msg(pass, "depthAttachment");
        MsgVoid(depth, "setTexture:", _depthTexture);
        MsgVoid(depth, "setLoadAction:", 2UL);
        MsgVoid(depth, "setStoreAction:", 1UL);
        MsgVoid(depth, "setClearDepth:", 1.0);
        return pass;
    }

    private IntPtr CreateColorRenderPass(IntPtr colorTexture, bool clearColor, MTLClearColor clear)
    {
        IntPtr rpdClass = ObjCRuntime.objc_getClass("MTLRenderPassDescriptor");
        IntPtr pass = Msg(rpdClass, "renderPassDescriptor");
        IntPtr colors = Msg(pass, "colorAttachments");
        IntPtr color0 = Msg(colors, "objectAtIndexedSubscript:", 0UL);
        MsgVoid(color0, "setTexture:", colorTexture);
        MsgVoid(color0, "setLoadAction:", clearColor ? 2UL : 1UL);
        MsgVoid(color0, "setStoreAction:", 1UL);
        if (clearColor)
            MsgVoid(color0, "setClearColor:", clear);
        return pass;
    }

    private IntPtr CreateDepthOnlyRenderPass(IntPtr depthTexture, bool clearDepth)
    {
        IntPtr rpdClass = ObjCRuntime.objc_getClass("MTLRenderPassDescriptor");
        IntPtr pass = Msg(rpdClass, "renderPassDescriptor");
        IntPtr depth = Msg(pass, "depthAttachment");
        MsgVoid(depth, "setTexture:", depthTexture);
        MsgVoid(depth, "setLoadAction:", clearDepth ? 2UL : 1UL);
        MsgVoid(depth, "setStoreAction:", 1UL);
        if (clearDepth)
            MsgVoid(depth, "setClearDepth:", 1.0);
        return pass;
    }

    private void BuildPipelines()
    {
        const string metal = """
#include <metal_stdlib>
using namespace metal;

struct InVertex {
    float4 position;
    float4 color;
    float4 worldPos;
    float4 worldNormal;
    float4 viewNormal;
    float2 uv;
    float2 misc;
};

struct SceneVsOut {
    float4 position [[position]];
    float4 color;
    float3 worldPos;
    float3 worldNormal;
    float3 viewNormal;
    float2 uv;
    float2 misc;
};

struct SceneOut {
    half4 color [[color(0)]];
    half4 normal [[color(1)]];
    half4 albedo [[color(2)]];
};

struct SceneFrameUniforms {
    float4 sunDir;
    float4 toSunView;
    float4 sunColor;
    float4 skyColor;
    float4 groundColor;
    float4 cameraPos;
    float4 fogColor;
    float4 params0;
    float4 params1;
    float4 time;
    float4x4 lightSpace;
};

struct SceneDrawUniforms {
    uint receiveShadows;
    uint writeNormal;
    uint viewSpaceLighting;
    uint pad;
    float4 materialParams;
    float4 materialFx0;
    float4 materialFx1;
};

struct OverlayDrawUniforms {
    uint mode;
    uint pad0;
    uint pad1;
    uint pad2;
};

struct PostUniforms {
    float4 params;
    float4 grade;
    float4 hybrid;
};

struct BloomTexelUniforms {
    float4 texelSize;
};

struct SsaoUniforms {
    float4 noiseScaleRadiusBias;
    float4x4 proj;
    float4x4 invProj;
    float4 samples[16];
};

struct SsaoBlurUniforms {
    float4 texelSize;
};

struct HybridGiUniforms {
    float4x4 invViewProj;
    float4 cameraPos;
    float4 toSun;
    float4 skyZenith;
    float4 skyHorizon;
    float4 groundColor;
    float4 params0; // turbidity, sampleCount, giStrength, triCount
};

struct GpuTriangle {
    float4 v0;
    float4 v1;
    float4 v2;
    float4 n;
};

SceneVsOut make_scene_vs(const device InVertex* verts, uint vid) {
    SceneVsOut o;
    InVertex v = verts[vid];
    o.position = v.position;
    o.color = v.color;
    o.worldPos = v.worldPos.xyz;
    o.worldNormal = v.worldNormal.xyz;
    o.viewNormal = v.viewNormal.xyz;
    o.uv = v.uv;
    o.misc = v.misc;
    return o;
}

vertex SceneVsOut vs_scene(const device InVertex* verts [[buffer(0)]], uint vid [[vertex_id]]) {
    return make_scene_vs(verts, vid);
}

vertex float4 vs_shadow(const device InVertex* verts [[buffer(0)]], uint vid [[vertex_id]]) {
    return verts[vid].position;
}

vertex SceneVsOut vs_overlay(const device InVertex* verts [[buffer(0)]], uint vid [[vertex_id]]) {
    return make_scene_vs(verts, vid);
}

vertex SceneVsOut vs_ui(const device InVertex* verts [[buffer(0)]], uint vid [[vertex_id]]) {
    return make_scene_vs(verts, vid);
}

struct FsQuadOut {
    float4 position [[position]];
    float2 uv;
};

vertex FsQuadOut vs_fullscreen(uint vid [[vertex_id]]) {
    float2 pos[3] = { float2(-1.0, -1.0), float2(3.0, -1.0), float2(-1.0, 3.0) };
    float2 uv[3]  = { float2(0.0, 0.0),  float2(2.0, 0.0),  float2(0.0, 2.0) };
    FsQuadOut o;
    o.position = float4(pos[vid], 0.0, 1.0);
    o.uv = uv[vid];
    return o;
}

float hash21(float2 p) { return fract(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float hash31(float3 p) { return fract(sin(dot(p, float3(127.1, 311.7, 191.999))) * 43758.5453); }
float noise2(float2 p){
    float2 i = floor(p), f = fract(p);
    float a = hash21(i);
    float b = hash21(i + float2(1,0));
    float c = hash21(i + float2(0,1));
    float d = hash21(i + float2(1,1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

float3 viewPosFromDepth(float2 uv, float depth, constant SsaoUniforms& u){
    float4 clip = float4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    float4 v = u.invProj * clip;
    return v.xyz / max(v.w, 1e-5);
}

float3 worldPosFromDepth(float2 uv, float depth, constant HybridGiUniforms& u){
    float4 clip = float4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    float4 w = u.invViewProj * clip;
    return w.xyz / max(w.w, 1e-5);
}

float3 atmosphere(float3 dir, constant HybridGiUniforms& u){
    float sunCos = clamp(dot(dir, u.toSun.xyz), -1.0, 1.0);
    float sunDot = max(sunCos, 0.0);
    float t = clamp(1.0 - max(dir.y, 0.0), 0.0, 1.0);
    float3 sky = mix(u.skyZenith.xyz, u.skyHorizon.xyz, pow(t, 2.5));
    float mie = pow(sunDot, 8.0) * 0.5 + pow(sunDot, 256.0) * 8.0;
    sky += float3(1.6, 1.2, 0.85) * mie;
    if (dir.y < 0.0){
        float gy = clamp(-dir.y * 5.0, 0.0, 1.0);
        sky = mix(sky, u.groundColor.xyz * 0.25, gy);
    }
    sky *= mix(0.85, 1.35, clamp((u.params0.x - 2.0) / 8.0, 0.0, 1.0));
    return sky;
}

bool intersectTri(float3 ro, float3 rd, GpuTriangle tri, thread float& tHit){
    float3 e1 = tri.v1.xyz - tri.v0.xyz;
    float3 e2 = tri.v2.xyz - tri.v0.xyz;
    float3 p = cross(rd, e2);
    float det = dot(e1, p);
    if (abs(det) < 1e-6) return false;
    float invDet = 1.0 / det;
    float3 s = ro - tri.v0.xyz;
    float uB = dot(s, p) * invDet;
    if (uB < 0.0 || uB > 1.0) return false;
    float3 q = cross(s, e1);
    float vB = dot(rd, q) * invDet;
    if (vB < 0.0 || uB + vB > 1.0) return false;
    float t = dot(e2, q) * invDet;
    if (t <= 0.02) return false;
    tHit = t;
    return true;
}

float3 cosineHemisphere(float2 xi){
    float r = sqrt(xi.x);
    float phi = 6.2831853 * xi.y;
    return float3(r * cos(phi), r * sin(phi), sqrt(max(0.0, 1.0 - xi.x)));
}

float3 tangentFromNormal(float3 n){
    float3 up = abs(n.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    return normalize(cross(up, n));
}

float3 tangentToWorld(float3 sampleDir, float3 n){
    float3 t = tangentFromNormal(n);
    float3 b = normalize(cross(n, t));
    return normalize(float3x3(t, b, n) * sampleDir);
}

float3x3 cotangentFrame(float3 n, float3 p, float2 uv){
    float3 dp1 = dfdx(p);
    float3 dp2 = dfdy(p);
    float2 duv1 = dfdx(uv);
    float2 duv2 = dfdy(uv);
    float3 dp2perp = cross(dp2, n);
    float3 dp1perp = cross(n, dp1);
    float3 t = dp2perp * duv1.x + dp1perp * duv2.x;
    float3 b = dp2perp * duv1.y + dp1perp * duv2.y;
    float invmax = rsqrt(max(dot(t, t), dot(b, b)));
    return float3x3(t * invmax, b * invmax, n);
}

float3 detailNormalFromAlbedo(texture2d<float> tex, sampler s, float2 uv, float3 n, float strength){
    if (strength <= 0.0001) return normalize(n);
    float2 texel = 1.0 / float2(tex.get_width(), tex.get_height());
    float3 luma = float3(0.2126, 0.7152, 0.0722);
    float left  = dot(tex.sample(s, uv - float2(texel.x, 0.0)).rgb, luma);
    float right = dot(tex.sample(s, uv + float2(texel.x, 0.0)).rgb, luma);
    float down  = dot(tex.sample(s, uv - float2(0.0, texel.y)).rgb, luma);
    float up    = dot(tex.sample(s, uv + float2(0.0, texel.y)).rgb, luma);
    float3 t = tangentFromNormal(n);
    float3 b = normalize(cross(n, t));
    float3 mapN = normalize(float3((left - right) * strength * 6.0, (down - up) * strength * 6.0, 1.0));
    return normalize(float3x3(t, b, n) * mapN);
}

float sampleShadow(float3 worldPos, float3 n, constant SceneFrameUniforms& frame, depth2d<float> shadowTex, sampler s){
    float4 lp = frame.lightSpace * float4(worldPos, 1.0);
    lp.y = -lp.y;
    lp.z = 0.5 * (lp.z + lp.w);
    float3 proj = lp.xyz / max(lp.w, 1e-5);
    float2 uv = proj.xy * 0.5 + 0.5;
    if (proj.z > 1.0 || uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        return 1.0;
    float bias = max(0.00022 * (1.0 - max(dot(n, -frame.sunDir.xyz), 0.0)), 0.00005);
    float depth = proj.z - bias;
    float2 texel = 1.0 / float2(shadowTex.get_width(), shadowTex.get_height());
    float distToCamera = length(frame.cameraPos.xyz - worldPos);
    float adaptiveSoftness = mix(0.75, 1.65, clamp((distToCamera - 6.0) / 28.0, 0.0, 1.0)) * frame.params0.z;
    float2 taps[16] = {
        float2(-0.326, -0.406), float2(-0.840, -0.074), float2(-0.696,  0.457), float2(-0.203,  0.621),
        float2( 0.962, -0.195), float2( 0.473, -0.480), float2( 0.519,  0.767), float2( 0.185, -0.893),
        float2( 0.507,  0.064), float2( 0.896,  0.412), float2(-0.322, -0.933), float2(-0.792, -0.598),
        float2(-0.118,  0.122), float2( 0.142, -0.132), float2(-0.452,  0.212), float2( 0.328,  0.286)
    };
    float sum = 0.0;
    for (int i = 0; i < 16; ++i) {
        float sd = shadowTex.sample(s, uv + taps[i] * texel * adaptiveSoftness);
        sum += depth <= sd ? 1.0 : 0.0;
    }
    return sum / 16.0;
}

float3 sunSpecular(float3 n, float3 viewDir, float3 lightDir, constant SceneFrameUniforms& frame, float roughness, float specularStrength, float visibility){
    roughness = clamp(roughness, 0.0, 1.0);
    float ndl = max(dot(n, lightDir), 0.0);
    float ndv = max(dot(n, viewDir), 0.0);
    if (ndl <= 0.0 || ndv <= 0.0 || specularStrength <= 0.0001) return float3(0.0);
    float3 h = normalize(viewDir + lightDir);
    float ndh = max(dot(n, h), 0.0);
    float shininess = mix(96.0, 12.0, roughness);
    float spec = pow(ndh, shininess) * ndl * visibility;
    float fres = mix(0.04, 1.0, pow(1.0 - ndv, 5.0));
    return frame.sunColor.xyz * frame.params0.x * spec * fres * specularStrength;
}

float3 applyFog(float3 color, float3 worldPos, constant SceneFrameUniforms& frame, constant SceneDrawUniforms& draw){
    if (draw.materialParams.w <= 0.0) return color;
    float dist = max(length(frame.cameraPos.xyz - worldPos) - frame.params1.y, 0.0);
    float fog = 1.0 - exp(-dist * frame.params1.x);
    float height = clamp(exp(-(worldPos.y - frame.params1.w) * frame.params1.z), 0.0, 1.0);
    fog = clamp(fog * height, 0.0, 1.0);
    return mix(color, frame.fogColor.xyz, fog);
}

fragment SceneOut fs_scene(SceneVsOut in [[stage_in]],
                           texture2d<float> tex [[texture(0)]],
                           depth2d<float> shadowTex [[texture(1)]],
                           texture2d<float> normalTex [[texture(2)]],
                           texture2d<float> roughTex [[texture(3)]],
                           texture2d<float> aoTex [[texture(4)]],
                           sampler linearSamp [[sampler(0)]],
                           constant SceneFrameUniforms& frame [[buffer(0)]],
                           constant SceneDrawUniforms& draw [[buffer(1)]]) {
    SceneOut o;
    float4 base = tex.sample(linearSamp, in.uv) * in.color;
    if (base.a < 0.05) discard_fragment();
    float3 geomN = normalize(draw.viewSpaceLighting != 0 ? in.viewNormal : in.worldNormal);
    float2 flowUv = in.uv + draw.materialFx1.xy * frame.time.x;
    float2 distort = float2(
        sin((in.worldPos.x + frame.time.x * 0.9) * 1.7),
        cos((in.worldPos.z - frame.time.x * 0.7) * 1.4)) * (draw.materialFx1.z * 0.06);
    float2 sampleUv = flowUv + distort;
    base = tex.sample(linearSamp, sampleUv) * in.color;
    float3 sampledNormal = normalTex.sample(linearSamp, sampleUv).xyz * 2.0 - 1.0;
    float3 n = dot(sampledNormal, sampledNormal) > 0.2
        ? normalize(cotangentFrame(geomN, in.worldPos, sampleUv) * sampledNormal)
        : detailNormalFromAlbedo(tex, linearSamp, sampleUv, geomN, draw.materialParams.z);
    float roughness = roughTex.sample(linearSamp, sampleUv).r;
    roughness = max(roughness, draw.materialParams.x * 0.5);
    float ao = aoTex.sample(linearSamp, sampleUv).r;
    float3 toSun = normalize(draw.viewSpaceLighting != 0 ? frame.toSunView.xyz : -frame.sunDir.xyz);
    float3 viewDir = normalize(draw.viewSpaceLighting != 0 ? -in.worldPos : (frame.cameraPos.xyz - in.worldPos));
    float vis = draw.receiveShadows != 0 ? sampleShadow(in.worldPos, normalize(in.worldNormal), frame, shadowTex, linearSamp) : 1.0;
    float ndl = max(dot(n, toSun), 0.0);
    float hemi = clamp(n.y * 0.5 + 0.5, 0.0, 1.0);
    float3 ambient = mix(frame.groundColor.xyz * 0.35, frame.skyColor.xyz, hemi) * frame.params0.y * ao;
    float3 lit = base.rgb * ambient
               + base.rgb * frame.sunColor.xyz * frame.params0.x * ndl * vis
               + sunSpecular(n, viewDir, toSun, frame, roughness, draw.materialParams.y, vis);
    int kind = int(draw.materialFx0.x + 0.5);
    if (kind == 1) {
        float fres = pow(1.0 - max(dot(n, viewDir), 0.0), 5.0) * draw.materialFx0.w;
        float3 waterTint = mix(float3(0.08, 0.34, 0.54), float3(0.14, 0.52, 0.72), clamp(base.b * 1.4, 0.0, 1.0));
        float3 refl = mix(frame.fogColor.xyz * 0.45 + waterTint * 0.35, frame.sunColor.xyz * 1.15 + waterTint * 0.25, clamp(fres, 0.0, 1.0));
        float3 body = waterTint * 0.72 + base.rgb * 0.18 + ambient * 0.18;
        lit = mix(body, refl + sunSpecular(n, viewDir, toSun, frame, 0.04, max(draw.materialParams.y, 0.35), 1.0), clamp(draw.materialFx0.z, 0.0, 1.0));
    } else if (kind == 2) {
        float pulse = 1.0 + sin(frame.time.x * 4.0 + in.worldPos.x * 0.35 + in.worldPos.z * 0.28) * draw.materialFx1.w;
        float3 emissive = base.rgb * draw.materialFx0.y * pulse;
        lit = base.rgb * 0.20 + emissive + base.rgb * frame.sunColor.xyz * frame.params0.x * ndl * vis * 0.15;
    }
    lit = applyFog(lit, in.worldPos, frame, draw);
    o.color = half4(float4(lit, base.a));
    o.normal = draw.writeNormal != 0 ? half4(float4(normalize(in.viewNormal), 1.0)) : half4(0.0);
    o.albedo = half4(float4(base.rgb, roughness));
    return o;
}

fragment SceneOut fs_overlay(SceneVsOut in [[stage_in]], constant OverlayDrawUniforms& draw [[buffer(0)]]) {
    SceneOut o;
    o.normal = half4(0.0);
    o.albedo = half4(0.0);
    if (draw.mode == 1u) {
        float r = length(in.uv);
        if (r > 1.0) discard_fragment();
        float a = smoothstep(1.0, 0.55, r) * in.color.a;
        o.color = half4(float4(in.color.rgb, a));
        return o;
    }
    if (draw.mode == 2u) {
        float2 q = in.uv * 3.0 + float2(in.misc.x * 17.0, in.misc.x * 31.0);
        float n = noise2(q) * 0.5 + noise2(q * 2.0) * 0.25 + noise2(q * 4.0) * 0.125;
        float r = length(in.uv) * (0.85 + n * 0.30);
        if (r > 1.0) discard_fragment();
        float core = 1.0 - smoothstep(0.0, 0.55, r);
        float halo = 1.0 - smoothstep(0.55, 1.0, r);
        float alpha = clamp(core * 0.92 + halo * 0.45, 0.0, 0.95);
        float3 sooty = float3(0.012, 0.010, 0.008);
        float3 ash = float3(0.060, 0.045, 0.035);
        float3 col = mix(ash, sooty, core);
        o.color = half4(float4(col, alpha));
        return o;
    }
    if (draw.mode == 3u) {
        float r = length(in.uv);
        if (r > 1.0) discard_fragment();
        float core = exp(-r * 7.0) * 1.6;
        float halo = exp(-r * 2.2) * 0.55;
        float streak = exp(-abs(in.uv.y) * 16.0) * exp(-abs(in.uv.x) * 1.8) * 0.6;
        float a = clamp((core + halo + streak) * in.color.a, 0.0, 1.5);
        float3 hot = float3(1.0, 0.96, 0.78);
        float3 warm = float3(1.0, 0.65, 0.20);
        float3 color = mix(warm, hot, clamp(core, 0.0, 1.0));
        o.color = half4(float4(color * a * 6.0, a));
        return o;
    }
    if (draw.mode == 4u) {
        float r = length(in.uv);
        if (r > 1.0) discard_fragment();
        float a = smoothstep(1.0, 0.15, r) * in.color.a;
        o.color = half4(float4(in.color.rgb, a));
        return o;
    }
    o.color = half4(in.color);
    return o;
}

fragment float4 fs_ui(SceneVsOut in [[stage_in]]) {
    return in.color;
}

fragment float4 fs_hybrid_gi(FsQuadOut in [[stage_in]],
                             depth2d<float> depthTex [[texture(0)]],
                             texture2d<float> normalTex [[texture(1)]],
                             texture2d<float> albedoTex [[texture(2)]],
                             texture2d<float> prevTex [[texture(3)]],
                             sampler s [[sampler(0)]],
                             constant HybridGiUniforms& u [[buffer(0)]],
                             const device GpuTriangle* tris [[buffer(1)]]) {
    float depth = depthTex.sample(s, in.uv);
    if (depth >= 0.99999) return float4(0.0, 0.0, 0.0, 1.0);
    float3 n = normalTex.sample(s, in.uv).xyz;
    if (dot(n, n) < 0.001) return prevTex.sample(s, in.uv);
    n = normalize(n);
    float3 albedo = albedoTex.sample(s, in.uv).rgb;
    float3 worldPos = worldPosFromDepth(in.uv, depth, u);
    float3 origin = worldPos + n * 0.03;
    int triCount = int(u.params0.w);
    float3 current = float3(0.0);
    for (int rayIdx = 0; rayIdx < 2; ++rayIdx) {
        float2 xi = float2(
            hash31(float3(in.uv + float2(rayIdx * 0.173, rayIdx * 0.417), u.params0.y * 1.17 + rayIdx)),
            hash31(float3(in.uv.yx + 17.31 + rayIdx * 0.271, u.params0.y * 1.91 + rayIdx * 0.73)));
        float3 rayDir = tangentToWorld(cosineHemisphere(xi), n);
        bool blocked = false;
        float bestT = 1e20;
        for (int i = 0; i < triCount; ++i) {
            float tHit;
            if (intersectTri(origin, rayDir, tris[i], tHit) && tHit < bestT) {
                bestT = tHit;
                blocked = true;
            }
        }
        current += blocked ? float3(0.0) : albedo * atmosphere(rayDir, u);
    }
    current *= 0.5;
    if (u.params0.y <= 0.5) return float4(current, 1.0);
    float history = min(u.params0.y / (u.params0.y + 1.0), 0.97);
    float3 prev = prevTex.sample(s, in.uv).rgb;
    return float4(mix(current, prev, history), 1.0);
}

fragment float4 fs_hybrid_gi_blur(FsQuadOut in [[stage_in]],
                                  texture2d<float> giTex [[texture(0)]],
                                  depth2d<float> depthTex [[texture(1)]],
                                  texture2d<float> normalTex [[texture(2)]],
                                  sampler s [[sampler(0)]]) {
    float centerDepth = depthTex.sample(s, in.uv);
    float3 centerNormal = normalize(normalTex.sample(s, in.uv).xyz + 1e-5);
    float2 texel = 1.0 / float2(giTex.get_width(), giTex.get_height());
    float3 sum = float3(0.0);
    float weight = 0.0;
    for (int y = -1; y <= 1; ++y) {
        for (int x = -1; x <= 1; ++x) {
            float2 o = float2(x, y) * texel;
            float d = depthTex.sample(s, in.uv + o * 4.0);
            float3 nrm = normalize(normalTex.sample(s, in.uv + o * 4.0).xyz + 1e-5);
            float w = exp(-abs(d - centerDepth) * 2500.0) * pow(max(dot(centerNormal, nrm), 0.0), 8.0);
            sum += giTex.sample(s, in.uv + o).rgb * w;
            weight += w;
        }
    }
    return float4(weight > 0.0 ? sum / weight : giTex.sample(s, in.uv).rgb, 1.0);
}

fragment float4 fs_post(FsQuadOut in [[stage_in]],
                        texture2d<float> hdrTex [[texture(0)]],
                        texture2d<float> bloomTex [[texture(1)]],
                        texture2d<float> aoTex [[texture(2)]],
                        texture2d<float> giTex [[texture(3)]],
                        sampler s [[sampler(0)]],
                        constant PostUniforms& u [[buffer(0)]]) {
    float3 hdr = hdrTex.sample(s, in.uv).rgb;
    float3 bloom = bloomTex.sample(s, in.uv).rgb;
    float ao = aoTex.sample(s, in.uv).r;
    float3 gi = giTex.sample(s, in.uv).rgb;
    float aoMul = mix(1.0, ao, clamp(u.params.z, 0.0, 1.0));
    float3 c = (hdr * aoMul + bloom * u.params.y + gi * u.hybrid.x) * u.params.x;
    const float a = 2.51;
    const float b = 0.03;
    const float cc = 2.43;
    const float d = 0.59;
    const float e = 0.14;
    c = clamp((c * (a * c + b)) / (c * (cc * c + d) + e), 0.0, 1.0);
    float lum = dot(c, float3(0.2126, 0.7152, 0.0722));
    float3 cool = float3(1.0 - u.grade.y * 0.35, 1.0, 1.0 + u.grade.y);
    float3 warm = float3(1.0 + u.grade.z, 1.0 + u.grade.z * 0.35, 1.0 - u.grade.z * 0.55);
    c *= mix(cool, warm, smoothstep(0.18, 0.90, lum));
    c = mix(float3(lum), c, u.grade.x);
    c = (c - 0.5) * u.params.w + 0.5;
    c = clamp(c, 0.0, 1.0);
    c = pow(c, float3(1.0 / 2.2));
    float2 q = in.uv * 2.0 - 1.0;
    float vignette = 1.0 - dot(q, q) * 0.22 * u.grade.w;
    c *= clamp(vignette, 0.0, 1.0);
    return float4(c, 1.0);
}

fragment float4 fs_bloom_threshold(FsQuadOut in [[stage_in]], texture2d<float> tex [[texture(0)]], sampler s [[sampler(0)]]) {
    float3 c = tex.sample(s, in.uv).rgb;
    float bright = max(max(c.r, c.g), c.b);
    float soft = clamp(bright - 0.9, 0.0, 0.5);
    soft = soft * soft / 0.5;
    float contrib = max(soft, max(bright - 1.0, 0.0));
    contrib = bright > 0.0 ? contrib / max(bright, 1e-4) : 0.0;
    return float4(c * contrib, 1.0);
}

fragment float4 fs_bloom_down(FsQuadOut in [[stage_in]], texture2d<float> tex [[texture(0)]], sampler s [[sampler(0)]], constant BloomTexelUniforms& u [[buffer(0)]]) {
    float2 t = u.texelSize.xy;
    float3 a = tex.sample(s, in.uv + t * float2(-1.0, -1.0)).rgb;
    float3 b = tex.sample(s, in.uv + t * float2( 0.0, -1.0)).rgb;
    float3 c = tex.sample(s, in.uv + t * float2( 1.0, -1.0)).rgb;
    float3 d = tex.sample(s, in.uv + t * float2(-0.5, -0.5)).rgb;
    float3 e = tex.sample(s, in.uv + t * float2( 0.5, -0.5)).rgb;
    float3 f = tex.sample(s, in.uv + t * float2(-1.0,  0.0)).rgb;
    float3 g = tex.sample(s, in.uv).rgb;
    float3 h = tex.sample(s, in.uv + t * float2( 1.0,  0.0)).rgb;
    float3 i = tex.sample(s, in.uv + t * float2(-0.5,  0.5)).rgb;
    float3 j = tex.sample(s, in.uv + t * float2( 0.5,  0.5)).rgb;
    float3 k = tex.sample(s, in.uv + t * float2(-1.0,  1.0)).rgb;
    float3 l = tex.sample(s, in.uv + t * float2( 0.0,  1.0)).rgb;
    float3 m = tex.sample(s, in.uv + t * float2( 1.0,  1.0)).rgb;
    float3 res =
        (d + e + i + j) * (0.5 / 4.0) +
        (a + b + g + f) * (0.125 / 4.0) +
        (b + c + h + g) * (0.125 / 4.0) +
        (f + g + l + k) * (0.125 / 4.0) +
        (g + h + m + l) * (0.125 / 4.0);
    return float4(res, 1.0);
}

fragment float4 fs_bloom_up(FsQuadOut in [[stage_in]], texture2d<float> tex [[texture(0)]], sampler s [[sampler(0)]], constant BloomTexelUniforms& u [[buffer(0)]]) {
    float2 t = u.texelSize.xy * u.texelSize.z;
    float3 a = tex.sample(s, in.uv + float2(-t.x, -t.y)).rgb;
    float3 b = tex.sample(s, in.uv + float2( 0.0, -t.y)).rgb * 2.0;
    float3 c = tex.sample(s, in.uv + float2( t.x, -t.y)).rgb;
    float3 d = tex.sample(s, in.uv + float2(-t.x,  0.0)).rgb * 2.0;
    float3 e = tex.sample(s, in.uv).rgb * 4.0;
    float3 f = tex.sample(s, in.uv + float2( t.x,  0.0)).rgb * 2.0;
    float3 g = tex.sample(s, in.uv + float2(-t.x,  t.y)).rgb;
    float3 h = tex.sample(s, in.uv + float2( 0.0,  t.y)).rgb * 2.0;
    float3 i = tex.sample(s, in.uv + float2( t.x,  t.y)).rgb;
    return float4(((a + c + g + i) + (b + d + f + h) + e) * (1.0 / 16.0), 1.0);
}

fragment float fs_ssao(FsQuadOut in [[stage_in]],
                       depth2d<float> depthTex [[texture(0)]],
                       texture2d<float> normalTex [[texture(1)]],
                       texture2d<float> noiseTex [[texture(2)]],
                       sampler linearSamp [[sampler(0)]],
                       sampler repeatSamp [[sampler(1)]],
                       constant SsaoUniforms& u [[buffer(0)]]) {
    float d = depthTex.sample(linearSamp, in.uv);
    if (d >= 0.99999) return 1.0;
    float3 P = viewPosFromDepth(in.uv, d, u);
    float3 N = normalTex.sample(linearSamp, in.uv).xyz;
    if (dot(N, N) < 0.001) return 1.0;
    N = normalize(N);
    float3 rnd = noiseTex.sample(repeatSamp, in.uv * u.noiseScaleRadiusBias.xy).xyz * 2.0 - 1.0;
    rnd.z = 0.0;
    float3 T = normalize(rnd - N * dot(rnd, N));
    float3 B = cross(N, T);
    float3x3 TBN = float3x3(T, B, N);
    float occ = 0.0;
    for (int i = 0; i < 16; ++i) {
        float3 s = TBN * u.samples[i].xyz;
        float3 sp = P + s * u.noiseScaleRadiusBias.z;
        float4 ofs = u.proj * float4(sp, 1.0);
        ofs.xyz /= max(ofs.w, 1e-5);
        float2 suv = ofs.xy * 0.5 + 0.5;
        if (suv.x < 0.0 || suv.x > 1.0 || suv.y < 0.0 || suv.y > 1.0) continue;
        float sd = depthTex.sample(linearSamp, suv);
        if (sd >= 0.99999) continue;
        float3 sv = viewPosFromDepth(suv, sd, u);
        float rangeCheck = smoothstep(0.0, 1.0, u.noiseScaleRadiusBias.z / max(abs(P.z - sv.z), 0.0001));
        occ += ((sv.z >= sp.z + u.noiseScaleRadiusBias.w) ? 1.0 : 0.0) * rangeCheck;
    }
    occ = 1.0 - occ / 16.0;
    return pow(clamp(occ, 0.0, 1.0), 1.5);
}

fragment float fs_ssao_blur(FsQuadOut in [[stage_in]],
                            texture2d<float> aoTex [[texture(0)]],
                            depth2d<float> depthTex [[texture(1)]],
                            sampler s [[sampler(0)]],
                            constant SsaoBlurUniforms& u [[buffer(0)]]) {
    float centerD = depthTex.sample(s, in.uv);
    float sum = 0.0;
    float weight = 0.0;
    for (int y = -2; y < 2; ++y) {
        for (int x = -2; x < 2; ++x) {
            float2 o = float2(x, y) * u.texelSize.xy;
            float d = depthTex.sample(s, in.uv + o);
            float w = exp(-abs(d - centerD) * 4000.0);
            sum += aoTex.sample(s, in.uv + o).r * w;
            weight += w;
        }
    }
    return weight > 0.0 ? sum / weight : 1.0;
}

fragment void fs_shadow() {}
""";

        IntPtr library = CreateLibrary(metal);
        _scenePipeline = CreateRenderPipeline(library, "vs_scene", "fs_scene", PixelFormatRgba16Float, PixelFormatRgba16Float, PixelFormatRgba16Float, PixelFormatDepth32Float, blendMode: BlendMode.Disabled);
        _shadowPipeline = CreateDepthOnlyPipeline(library, "vs_shadow", "fs_shadow", PixelFormatDepth32Float);
        _postPipeline = CreateSingleColorPipeline(library, "vs_fullscreen", "fs_post", PixelFormatBgra8Unorm, BlendMode.Disabled);
        _bloomThresholdPipeline = CreateSingleColorPipeline(library, "vs_fullscreen", "fs_bloom_threshold", PixelFormatRgba16Float, BlendMode.Disabled);
        _bloomDownPipeline = CreateSingleColorPipeline(library, "vs_fullscreen", "fs_bloom_down", PixelFormatRgba16Float, BlendMode.Disabled);
        _bloomUpPipeline = CreateSingleColorPipeline(library, "vs_fullscreen", "fs_bloom_up", PixelFormatRgba16Float, BlendMode.Additive);
        _ssaoPipeline = CreateSingleColorPipeline(library, "vs_fullscreen", "fs_ssao", PixelFormatR16Float, BlendMode.Disabled);
        _ssaoBlurPipeline = CreateSingleColorPipeline(library, "vs_fullscreen", "fs_ssao_blur", PixelFormatR16Float, BlendMode.Disabled);
        _hybridGiPipeline = CreateSingleColorPipeline(library, "vs_fullscreen", "fs_hybrid_gi", PixelFormatRgba16Float, BlendMode.Disabled);
        _hybridGiBlurPipeline = CreateSingleColorPipeline(library, "vs_fullscreen", "fs_hybrid_gi_blur", PixelFormatRgba16Float, BlendMode.Disabled);
        _alphaOverlayPipeline = CreateRenderPipeline(library, "vs_overlay", "fs_overlay", PixelFormatRgba16Float, PixelFormatRgba16Float, PixelFormatRgba16Float, PixelFormatDepth32Float, BlendMode.Alpha);
        _additiveOverlayPipeline = CreateRenderPipeline(library, "vs_overlay", "fs_overlay", PixelFormatRgba16Float, PixelFormatRgba16Float, PixelFormatRgba16Float, PixelFormatDepth32Float, BlendMode.Additive);
        _uiPipeline = CreateSingleColorPipeline(library, "vs_ui", "fs_ui", PixelFormatBgra8Unorm, BlendMode.Alpha);
        ObjCRuntime.objc_release(library);
    }

    private void BuildDepthStates()
    {
        _depthStateWriteLess = CreateDepthState(CompareLess, write: true);
        _depthStateReadLessEqual = CreateDepthState(CompareLessEqual, write: false);
        _depthStateDisabled = CreateDepthState(CompareAlways, write: false);
    }

    private void BuildSamplers()
    {
        _linearClampSampler = CreateSampler(repeat: false);
        _linearRepeatSampler = CreateSampler(repeat: true);
    }

    private IntPtr CreateSampler(bool repeat)
    {
        IntPtr cls = ObjCRuntime.objc_getClass("MTLSamplerDescriptor");
        IntPtr desc = Msg(Msg(cls, "alloc"), "init");
        MsgVoid(desc, "setMinFilter:", 1UL);
        MsgVoid(desc, "setMagFilter:", 1UL);
        MsgVoid(desc, "setSAddressMode:", repeat ? 2UL : 0UL);
        MsgVoid(desc, "setTAddressMode:", repeat ? 2UL : 0UL);
        IntPtr sampler = CreateObjectWithPossibleError(_device, "newSamplerStateWithDescriptor:", desc, operation: "sampler state");
        ObjCRuntime.objc_release(desc);
        return sampler;
    }

    private IntPtr CreateDepthState(ulong compare, bool write)
    {
        IntPtr cls = ObjCRuntime.objc_getClass("MTLDepthStencilDescriptor");
        IntPtr desc = Msg(Msg(cls, "alloc"), "init");
        MsgVoid(desc, "setDepthCompareFunction:", compare);
        MsgVoid(desc, "setDepthWriteEnabled:", write);
        IntPtr state = Msg(_device, "newDepthStencilStateWithDescriptor:", desc);
        ObjCRuntime.objc_release(desc);
        if (state == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create Metal depth-stencil state.");
        return ObjCRuntime.objc_retain(state);
    }

    private IntPtr CreateRenderPipeline(IntPtr library, string vsName, string fsName, ulong color0Format, ulong color1Format, ulong color2Format, ulong depthFormat, BlendMode blendMode)
    {
        using var vsNs = new NSString(vsName);
        using var fsNs = new NSString(fsName);
        IntPtr vs = Msg(library, "newFunctionWithName:", vsNs.Handle);
        IntPtr fs = Msg(library, "newFunctionWithName:", fsNs.Handle);
        IntPtr descClass = ObjCRuntime.objc_getClass("MTLRenderPipelineDescriptor");
        IntPtr desc = Msg(Msg(descClass, "alloc"), "init");
        MsgVoid(desc, "setVertexFunction:", vs);
        MsgVoid(desc, "setFragmentFunction:", fs);
        IntPtr attachments = Msg(desc, "colorAttachments");
        IntPtr color0 = Msg(attachments, "objectAtIndexedSubscript:", 0UL);
        MsgVoid(color0, "setPixelFormat:", color0Format);
        ConfigureBlend(color0, blendMode);
        IntPtr color1 = Msg(attachments, "objectAtIndexedSubscript:", 1UL);
        MsgVoid(color1, "setPixelFormat:", color1Format);
        ConfigureBlend(color1, blendMode);
        IntPtr color2 = Msg(attachments, "objectAtIndexedSubscript:", 2UL);
        MsgVoid(color2, "setPixelFormat:", color2Format);
        ConfigureBlend(color2, blendMode);
        MsgVoid(desc, "setDepthAttachmentPixelFormat:", depthFormat);
        IntPtr pipeline = CreateRenderPipelineState(desc, $"pipeline {vsName}/{fsName}");
        ObjCRuntime.objc_release(desc);
        ObjCRuntime.objc_release(vs);
        ObjCRuntime.objc_release(fs);
        return pipeline;
    }

    private IntPtr CreateSingleColorPipeline(IntPtr library, string vsName, string fsName, ulong colorFormat, BlendMode blendMode)
    {
        using var vsNs = new NSString(vsName);
        using var fsNs = new NSString(fsName);
        IntPtr vs = Msg(library, "newFunctionWithName:", vsNs.Handle);
        IntPtr fs = Msg(library, "newFunctionWithName:", fsNs.Handle);
        IntPtr descClass = ObjCRuntime.objc_getClass("MTLRenderPipelineDescriptor");
        IntPtr desc = Msg(Msg(descClass, "alloc"), "init");
        MsgVoid(desc, "setVertexFunction:", vs);
        MsgVoid(desc, "setFragmentFunction:", fs);
        IntPtr attachments = Msg(desc, "colorAttachments");
        IntPtr color0 = Msg(attachments, "objectAtIndexedSubscript:", 0UL);
        MsgVoid(color0, "setPixelFormat:", colorFormat);
        ConfigureBlend(color0, blendMode);
        IntPtr pipeline = CreateRenderPipelineState(desc, $"pipeline {vsName}/{fsName}");
        ObjCRuntime.objc_release(desc);
        ObjCRuntime.objc_release(vs);
        ObjCRuntime.objc_release(fs);
        return pipeline;
    }

    private IntPtr CreateDepthOnlyPipeline(IntPtr library, string vsName, string fsName, ulong depthFormat)
    {
        using var vsNs = new NSString(vsName);
        using var fsNs = new NSString(fsName);
        IntPtr vs = Msg(library, "newFunctionWithName:", vsNs.Handle);
        IntPtr fs = Msg(library, "newFunctionWithName:", fsNs.Handle);
        IntPtr descClass = ObjCRuntime.objc_getClass("MTLRenderPipelineDescriptor");
        IntPtr desc = Msg(Msg(descClass, "alloc"), "init");
        MsgVoid(desc, "setVertexFunction:", vs);
        MsgVoid(desc, "setFragmentFunction:", fs);
        MsgVoid(desc, "setDepthAttachmentPixelFormat:", depthFormat);
        IntPtr pipeline = CreateRenderPipelineState(desc, $"pipeline {vsName}/{fsName}");
        ObjCRuntime.objc_release(desc);
        ObjCRuntime.objc_release(vs);
        ObjCRuntime.objc_release(fs);
        return pipeline;
    }

    private void ConfigureBlend(IntPtr attachment, BlendMode mode)
    {
        if (mode == BlendMode.Disabled)
        {
            MsgVoid(attachment, "setBlendingEnabled:", false);
            return;
        }

        MsgVoid(attachment, "setBlendingEnabled:", true);
        MsgVoid(attachment, "setRgbBlendOperation:", 0UL);
        MsgVoid(attachment, "setAlphaBlendOperation:", 0UL);
        if (mode == BlendMode.Alpha)
        {
            MsgVoid(attachment, "setSourceRGBBlendFactor:", 4UL);
            MsgVoid(attachment, "setDestinationRGBBlendFactor:", 5UL);
            MsgVoid(attachment, "setSourceAlphaBlendFactor:", 1UL);
            MsgVoid(attachment, "setDestinationAlphaBlendFactor:", 5UL);
        }
        else
        {
            MsgVoid(attachment, "setSourceRGBBlendFactor:", 1UL);
            MsgVoid(attachment, "setDestinationRGBBlendFactor:", 1UL);
            MsgVoid(attachment, "setSourceAlphaBlendFactor:", 1UL);
            MsgVoid(attachment, "setDestinationAlphaBlendFactor:", 1UL);
        }
    }

    private IntPtr CreateRenderPipelineState(IntPtr desc, string operation)
    {
        IntPtr errStorage = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(errStorage, IntPtr.Zero);
            IntPtr pipeline = Msg(_device, "newRenderPipelineStateWithDescriptor:error:", desc, errStorage);
            if (pipeline == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to create Metal {operation}: {GetNSErrorDescription(Marshal.ReadIntPtr(errStorage))}");
            return ObjCRuntime.objc_retain(pipeline);
        }
        finally
        {
            Marshal.FreeHGlobal(errStorage);
        }
    }

    private IntPtr CreateObjectWithPossibleError(IntPtr receiver, string selector, IntPtr arg, string operation)
    {
        IntPtr result = Msg(receiver, selector, arg);
        if (result == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to create Metal {operation}.");
        return ObjCRuntime.objc_retain(result);
    }

    private IntPtr CreateLibrary(string source)
    {
        using var ns = new NSString(source);
        IntPtr errStorage = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(errStorage, IntPtr.Zero);
            IntPtr lib = Msg(_device, "newLibraryWithSource:options:error:", ns.Handle, IntPtr.Zero, errStorage);
            if (lib == IntPtr.Zero)
                throw new InvalidOperationException($"Metal shader library compilation failed: {GetNSErrorDescription(Marshal.ReadIntPtr(errStorage))}");
            return lib;
        }
        finally
        {
            Marshal.FreeHGlobal(errStorage);
        }
    }

    private static string GetNSErrorDescription(IntPtr err)
    {
        if (err == IntPtr.Zero) return "unknown error";
        IntPtr desc = Msg(err, "localizedDescription");
        return GetNSStringUtf8(desc) ?? "unknown error";
    }

    private static string? GetNSStringUtf8(IntPtr str)
    {
        if (str == IntPtr.Zero) return null;
        IntPtr ptr = Msg(str, "UTF8String");
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }

    private static unsafe void SetFragmentBytes<T>(IntPtr encoder, ref T value, ulong index) where T : unmanaged
    {
        fixed (T* p = &value)
            MsgVoid(encoder, "setFragmentBytes:length:atIndex:", (IntPtr)p, (ulong)sizeof(T), index);
    }

    public void Dispose()
    {
        foreach (var tex in _textures.Values)
            if (tex != IntPtr.Zero && tex != _whiteTexture)
                ObjCRuntime.objc_release(tex);
        _textures.Clear();

        foreach (var model in _weaponModels.Values) ReleaseModel(model);
        _weaponModels.Clear();
        if (_rocketModel is not null) ReleaseModel(_rocketModel);

        ReleaseIfSet(ref _whiteTexture);
        ReleaseIfSet(ref _flatNormalTexture);
        ReleaseIfSet(ref _noiseTexture);
        ReleaseIfSet(ref _shadowTexture);
        ReleaseIfSet(ref _worldTriangleBuffer);
        ReleaseIfSet(ref _hdrTexture);
        ReleaseIfSet(ref _normalTexture);
        ReleaseIfSet(ref _albedoTexture);
        ReleaseIfSet(ref _depthTexture);
        ReleaseIfSet(ref _aoTexture);
        ReleaseIfSet(ref _aoBlurTexture);
        ReleaseIfSet(ref _hybridGiDenoiseTexture);
        for (int i = 0; i < 2; i++)
            ReleaseIfSet(ref _hybridGiTextures[i]);
        for (int i = 0; i < BloomMipCount; i++)
            ReleaseIfSet(ref _bloomTextures[i]);

        ReleaseIfSet(ref _vertexBuffer);

        ReleaseIfSet(ref _linearClampSampler);
        ReleaseIfSet(ref _linearRepeatSampler);

        ReleaseIfSet(ref _depthStateWriteLess);
        ReleaseIfSet(ref _depthStateReadLessEqual);
        ReleaseIfSet(ref _depthStateDisabled);

        ReleaseIfSet(ref _scenePipeline);
        ReleaseIfSet(ref _shadowPipeline);
        ReleaseIfSet(ref _postPipeline);
        ReleaseIfSet(ref _bloomThresholdPipeline);
        ReleaseIfSet(ref _bloomDownPipeline);
        ReleaseIfSet(ref _bloomUpPipeline);
        ReleaseIfSet(ref _ssaoPipeline);
        ReleaseIfSet(ref _ssaoBlurPipeline);
        ReleaseIfSet(ref _hybridGiPipeline);
        ReleaseIfSet(ref _hybridGiBlurPipeline);
        ReleaseIfSet(ref _alphaOverlayPipeline);
        ReleaseIfSet(ref _additiveOverlayPipeline);
        ReleaseIfSet(ref _uiPipeline);

        ReleaseIfSet(ref _queue);
        ReleaseIfSet(ref _layer);
        ReleaseIfSet(ref _device);
    }

    private void ReleaseModel(MetalModel model)
    {
        foreach (var prim in model.Primitives)
            if (prim.TextureHandle != IntPtr.Zero && prim.TextureHandle != _whiteTexture)
                ObjCRuntime.objc_release(prim.TextureHandle);
    }

    private static void ReleaseIfSet(ref IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        ObjCRuntime.objc_release(handle);
        handle = IntPtr.Zero;
    }

    private static Vector4 ConvertGlClipToMetal(Vector4 clip)
    {
        clip.Y = -clip.Y;
        clip.Z = 0.5f * (clip.Z + clip.W);
        return clip;
    }

    private static IntPtr Msg(IntPtr r, string s) => ObjCRuntime.IntPtr_objc_msgSend(r, ObjCRuntime.sel_registerName(s));
    private static IntPtr Msg(IntPtr r, string s, IntPtr a) => ObjCRuntime.IntPtr_objc_msgSend_IntPtr(r, ObjCRuntime.sel_registerName(s), a);
    private static IntPtr Msg(IntPtr r, string s, ulong a) => ObjCRuntime.IntPtr_objc_msgSend_UInt64(r, ObjCRuntime.sel_registerName(s), a);
    private static IntPtr Msg(IntPtr r, string s, ulong a, ulong b) => ObjCRuntime.IntPtr_objc_msgSend_UInt64_UInt64(r, ObjCRuntime.sel_registerName(s), a, b);
    private static IntPtr Msg(IntPtr r, string s, ulong a, ulong b, bool c) => ObjCRuntime.IntPtr_objc_msgSend_UInt64_UInt64_Bool(r, ObjCRuntime.sel_registerName(s), a, b, c);
    private static IntPtr Msg(IntPtr r, string s, IntPtr a, IntPtr b) => ObjCRuntime.IntPtr_objc_msgSend_IntPtr_IntPtr(r, ObjCRuntime.sel_registerName(s), a, b);
    private static IntPtr Msg(IntPtr r, string s, IntPtr a, IntPtr b, IntPtr c) => ObjCRuntime.IntPtr_objc_msgSend_IntPtr_IntPtr_IntPtr(r, ObjCRuntime.sel_registerName(s), a, b, c);
    private static IntPtr Msg(IntPtr r, string s, IntPtr a, ulong b, ulong c) => ObjCRuntime.IntPtr_objc_msgSend_IntPtr_UInt64_UInt64(r, ObjCRuntime.sel_registerName(s), a, b, c);
    private static IntPtr Msg(IntPtr r, string s, ulong a, ulong b, ulong c, bool d) => ObjCRuntime.IntPtr_objc_msgSend_UInt64_UInt64_UInt64_Bool(r, ObjCRuntime.sel_registerName(s), a, b, c, d);

    private static void MsgVoid(IntPtr r, string s) => ObjCRuntime.Void_objc_msgSend(r, ObjCRuntime.sel_registerName(s));
    private static void MsgVoid(IntPtr r, string s, IntPtr a) => ObjCRuntime.Void_objc_msgSend_IntPtr(r, ObjCRuntime.sel_registerName(s), a);
    private static void MsgVoid(IntPtr r, string s, IntPtr a, ulong b) => ObjCRuntime.Void_objc_msgSend_IntPtr_UInt64(r, ObjCRuntime.sel_registerName(s), a, b);
    private static void MsgVoid(IntPtr r, string s, IntPtr a, ulong b, ulong c) => ObjCRuntime.Void_objc_msgSend_IntPtr_UInt64_UInt64(r, ObjCRuntime.sel_registerName(s), a, b, c);
    private static void MsgVoid(IntPtr r, string s, ulong a) => ObjCRuntime.Void_objc_msgSend_UInt64(r, ObjCRuntime.sel_registerName(s), a);
    private static void MsgVoid(IntPtr r, string s, ulong a, ulong b, ulong c) => ObjCRuntime.Void_objc_msgSend_UInt64_UInt64_UInt64(r, ObjCRuntime.sel_registerName(s), a, b, c);
    private static void MsgVoid(IntPtr r, string s, bool a) => ObjCRuntime.Void_objc_msgSend_Bool(r, ObjCRuntime.sel_registerName(s), a);
    private static void MsgVoid(IntPtr r, string s, CGSize a) => ObjCRuntime.Void_objc_msgSend_CGSize(r, ObjCRuntime.sel_registerName(s), a);
    private static void MsgVoid(IntPtr r, string s, MTLClearColor a) => ObjCRuntime.Void_objc_msgSend_MTLClearColor(r, ObjCRuntime.sel_registerName(s), a);
    private static void MsgVoid(IntPtr r, string s, MTLRegion a, ulong b, IntPtr c, ulong d) => ObjCRuntime.Void_objc_msgSend_MTLRegion_UInt64_IntPtr_UInt64(r, ObjCRuntime.sel_registerName(s), a, b, c, d);
    private static void MsgVoid(IntPtr r, string s, double a) => ObjCRuntime.Void_objc_msgSend_Double(r, ObjCRuntime.sel_registerName(s), a);

    private sealed class AutoReleasePool : IDisposable
    {
        private readonly IntPtr _pool;
        public AutoReleasePool() { _pool = Msg(ObjCRuntime.objc_getClass("NSAutoreleasePool"), "new"); }
        public void Dispose() { if (_pool != IntPtr.Zero) MsgVoid(_pool, "drain"); }
    }

    private sealed class NSString : IDisposable
    {
        public IntPtr Handle { get; }
        public NSString(string value)
        {
            IntPtr cls = ObjCRuntime.objc_getClass("NSString");
            IntPtr alloc = Msg(cls, "alloc");
            byte[] utf8 = Encoding.UTF8.GetBytes(value + '\0');
            IntPtr ptr = Marshal.AllocHGlobal(utf8.Length);
            Marshal.Copy(utf8, 0, ptr, utf8.Length);
            Handle = Msg(alloc, "initWithUTF8String:", ptr);
            Marshal.FreeHGlobal(ptr);
        }
        public void Dispose() { if (Handle != IntPtr.Zero) ObjCRuntime.objc_release(Handle); }
    }

    private enum BlendMode { Disabled, Alpha, Additive }
    private enum OverlayMode { Flat = 0, Decal = 1, Scorch = 2, MuzzleFlash = 3, Particle = 4 }

    private readonly record struct FrameMatrices(Matrix4x4 View, Matrix4x4 Proj, Matrix4x4 InvProj, Matrix4x4 InvViewProj, Matrix4x4 ViewProj, Matrix4x4 LightSpace, Vector3 ToSunView);
    private readonly record struct WorldBatch(int StartVertex, int VertexCount, IntPtr BaseColorHandle, IntPtr NormalHandle, IntPtr RoughnessHandle, IntPtr AoHandle, Vector4 MaterialParams, Vector4 MaterialFx0, Vector4 MaterialFx1);
    private readonly record struct SceneBatch(int StartVertex, int VertexCount, IntPtr BaseColorHandle, IntPtr NormalHandle, IntPtr RoughnessHandle, IntPtr AoHandle, bool ReceiveShadows, bool WriteNormal, bool ViewSpaceLighting, Vector4 MaterialParams, Vector4 MaterialFx0, Vector4 MaterialFx1);
    private readonly record struct OverlayBatch(int StartVertex, int VertexCount, OverlayMode Mode, bool DepthTest);
    private readonly record struct UiBatch(int StartVertex, int VertexCount);
    private readonly record struct WorldSourceVertex(Vector3 WorldPos, Vector3 WorldNormal, Vector3 ViewNormal, Vector2 Uv, Vector3 BaseColor, IntPtr TextureHandle)
    {
        public WorldSourceVertex(Vector3 worldPos, Vector3 worldNormal, Vector2 uv, Vector3 baseColor, IntPtr textureHandle)
            : this(worldPos, worldNormal, worldNormal, uv, baseColor, textureHandle)
        { }
    }
    private readonly record struct LocalSourceVertex(Vector3 Position, Vector3 Normal, Vector2 Uv, Vector4 Color);
    private sealed record MetalPrimitive(LocalSourceVertex[] Vertices, IntPtr TextureHandle);
    private sealed record MetalModel(IReadOnlyList<MetalPrimitive> Primitives);

    [StructLayout(LayoutKind.Sequential)]
    private struct GpuVertex
    {
        public Vector4 Position;
        public Vector4 Color;
        public Vector4 WorldPos;
        public Vector4 WorldNormal;
        public Vector4 ViewNormal;
        public Vector2 Uv;
        public Vector2 Misc;

        public GpuVertex(Vector4 position, Vector4 color, Vector4 worldPos, Vector4 worldNormal, Vector4 viewNormal, Vector2 uv, Vector2 misc)
        {
            Position = position;
            Color = color;
            WorldPos = worldPos;
            WorldNormal = worldNormal;
            ViewNormal = viewNormal;
            Uv = uv;
            Misc = misc;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SceneFrameUniforms
    {
        public Vector4 SunDir;
        public Vector4 ToSunView;
        public Vector4 SunColor;
        public Vector4 SkyColor;
        public Vector4 GroundColor;
        public Vector4 CameraPos;
        public Vector4 FogColor;
        public Vector4 Params0;
        public Vector4 Params1;
        public Vector4 Time;
        public Matrix4x4 LightSpace;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SceneDrawUniforms
    {
        public uint ReceiveShadows;
        public uint WriteNormal;
        public uint ViewSpaceLighting;
        public uint Pad;
        public Vector4 MaterialParams;
        public Vector4 MaterialFx0;
        public Vector4 MaterialFx1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OverlayDrawUniforms
    {
        public uint Mode;
        public uint Pad0;
        public uint Pad1;
        public uint Pad2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PostUniforms
    {
        public Vector4 Params;
        public Vector4 Grade;
        public Vector4 Hybrid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BloomTexelUniforms
    {
        public Vector4 TexelSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HybridGiUniforms
    {
        public Matrix4x4 InvViewProj;
        public Vector4 CameraPos;
        public Vector4 ToSun;
        public Vector4 SkyZenith;
        public Vector4 SkyHorizon;
        public Vector4 GroundColor;
        public Vector4 Params0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct SsaoUniforms
    {
        public Vector4 NoiseScaleRadiusBias;
        public Matrix4x4 Proj;
        public Matrix4x4 InvProj;
        public fixed float Samples[SsaoKernelSize * 4];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SsaoBlurUniforms
    {
        public Vector4 TexelSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GpuTriangle
    {
        public Vector4 V0;
        public Vector4 V1;
        public Vector4 V2;
        public Vector4 N;

        public GpuTriangle(Vector4 v0, Vector4 v1, Vector4 v2, Vector4 n)
        {
            V0 = v0;
            V1 = v1;
            V2 = v2;
            N = n;
        }
    }
}