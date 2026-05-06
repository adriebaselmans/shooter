using System.Numerics;
using Shooter.Game;
using Shooter.Render;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Shooter.RenderSystem;

/// <summary>Feature-complete stable backend that wraps the existing OpenGL/HDR renderer stack.</summary>
public sealed class OpenGLRenderBackend : IRenderBackend
{
    private IWindow? _window;
    private GL? _gl;
    private WorldRenderer? _worldRen;
    private DecalRenderer? _decalRen;
    private TracerRenderer? _tracerRen;
    private HudRenderer? _hudRen;
    private WeaponViewmodelRenderer? _viewmodelRen;
    private RocketRenderer? _rocketRen;
    private MuzzleFlashRenderer? _muzzleFlashRen;
    private ScorchRenderer? _scorchRen;
    private ParticleRenderer? _particleRen;
    private HdrTarget? _hdr;
    private SkyRenderer? _skyRen;
    private IblProbe? _ibl;
    private ShadowMap? _shadow;
    private Bloom? _bloom;
    private SsaoPass? _ssao;
    private AutoExposure? _autoExp;
    private PostFx? _postFx;

    public string Name => "OpenGL";

    public void Initialize(IWindow window, GameWorld world, LightingEnvironment lighting)
    {
        _window = window;
        _gl = GL.GetApi(window);

        _worldRen = new WorldRenderer(_gl, world);
        _decalRen = new DecalRenderer(_gl);
        _tracerRen = new TracerRenderer(_gl);
        _hudRen = new HudRenderer(_gl);
        _viewmodelRen = new WeaponViewmodelRenderer(_gl);
        _rocketRen = new RocketRenderer(_gl);
        _muzzleFlashRen = new MuzzleFlashRenderer(_gl);
        _scorchRen = new ScorchRenderer(_gl);
        _particleRen = new ParticleRenderer(_gl);

        _hdr = new HdrTarget(_gl);
        _hdr.Resize(window.FramebufferSize.X, window.FramebufferSize.Y);
        _skyRen = new SkyRenderer(_gl);
        _ibl = new IblProbe(_gl);
        _ibl.Build(lighting);
        _shadow = new ShadowMap(_gl);
        _bloom = new Bloom(_gl);
        _bloom.Resize(window.FramebufferSize.X, window.FramebufferSize.Y);
        _ssao = new SsaoPass(_gl);
        _ssao.Resize(window.FramebufferSize.X, window.FramebufferSize.Y);
        _autoExp = new AutoExposure(_gl);
        _postFx = new PostFx(_gl);

        var sky = world.SkyColor;
        _gl.ClearColor(sky.X, sky.Y, sky.Z, 1f);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Viewport(window.FramebufferSize);
    }

    public void Resize(int width, int height)
    {
        if (_gl is null) return;
        _gl.Viewport(new Vector2D<int>(width, height));
        _hdr?.Resize(width, height);
        _bloom?.Resize(width, height);
        _ssao?.Resize(width, height);
    }

    public void Render(double dt, RenderFrameData frame)
    {
        if (_gl is null || _window is null || _worldRen is null || _hudRen is null || _decalRen is null || _tracerRen is null) return;
        if (_hdr is null || _skyRen is null || _ibl is null || _shadow is null || _bloom is null || _ssao is null || _autoExp is null || _postFx is null) return;

        var fb = _window.FramebufferSize;
        _hdr.Resize(fb.X, fb.Y);
        _bloom.Resize(fb.X, fb.Y);
        _ssao.Resize(fb.X, fb.Y);

        float aspect = fb.Y > 0 ? (float)fb.X / fb.Y : 16f / 9f;
        var view = Matrix4x4.CreateLookAt(frame.Player.EyePosition, frame.Player.EyePosition + frame.Player.Forward(), Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(WeaponViewmodelRenderer.FovYRadians, aspect, 0.05f, 1000f);
        var viewProj = view * proj;

        var lightSpace = _shadow.BuildLightSpace(frame.Player.Position, frame.Lighting);
        _shadow.RenderPass(frame.World.Brushes, (Dictionary<Guid, GlMesh>)_worldRen.BrushMeshes, lightSpace);

        _hdr.Bind();
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var viewNoTrans = view;
        viewNoTrans.M41 = 0; viewNoTrans.M42 = 0; viewNoTrans.M43 = 0;
        _skyRen.Draw(viewNoTrans, proj, frame.Lighting);

        _worldRen.Draw(view, viewProj, frame.World, frame.Pickups, frame.Lighting, _shadow, _ibl);
        _decalRen.Draw(viewProj, frame.Holes);
        _scorchRen?.Draw(viewProj, frame.Scorches);
        _tracerRen.Draw(viewProj, frame.Tracers);
        _rocketRen?.Draw(view, viewProj, frame.Rockets, frame.Lighting, _shadow, _ibl, _worldRen);
        _particleRen?.Draw(viewProj, frame.Player.Right(), Vector3.Cross(frame.Player.Right(), frame.Player.Forward()), frame.Particles);
        _viewmodelRen?.Draw(fb.X, fb.Y, frame.Weapons, frame.Lighting, _shadow, _ibl, _worldRen);
        if (frame.MuzzleFlash is not null) _muzzleFlashRen?.Draw(fb.X, fb.Y, frame.MuzzleFlash);

        _ssao.Run(_hdr.DepthTex, _hdr.NormalTex, proj, frame.Lighting.SsaoRadius, frame.Lighting.SsaoBias);
        _bloom.Run(_hdr.ColorTex);
        _autoExp.Run(_hdr.ColorTex, frame.Lighting, (float)dt);
        _postFx.Draw(_hdr.ColorTex, _bloom.OutputTex, _ssao.AoTex,
            frame.Lighting, frame.Lighting.SsaoStrength, _autoExp.CurrentExposure, fb.X, fb.Y);
        _hudRen.Draw(fb.X, fb.Y, frame.Player, frame.Weapons);

        if (frame.ShowDebug)
        {
            Console.Title = $"Shooter [{Name}] | pos={frame.Player.Position:F1} fps={frame.FpsValue:F0} tris={frame.World.AllTriangles.Count} holes={frame.Holes.Count} tracers={frame.Tracers.Active.Count}";
        }
    }

    public void Dispose()
    {
        _postFx?.Dispose();
        _autoExp?.Dispose();
        _ssao?.Dispose();
        _bloom?.Dispose();
        _shadow?.Dispose();
        _ibl?.Dispose();
        _skyRen?.Dispose();
        _hdr?.Dispose();
        _particleRen?.Dispose();
        _scorchRen?.Dispose();
        _muzzleFlashRen?.Dispose();
        _rocketRen?.Dispose();
        _viewmodelRen?.Dispose();
        _hudRen?.Dispose();
        _tracerRen?.Dispose();
        _decalRen?.Dispose();
        _worldRen?.Dispose();
    }
}
