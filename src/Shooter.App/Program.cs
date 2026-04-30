using System.Numerics;
using MapEditor.Formats;
using Shooter.Game;
using Shooter.Input;
using Shooter.Physics;
using Shooter.Render;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Shooter;

internal static class Program
{
    private static IWindow? _window;
    private static GL? _gl;
    private static IInputContext? _input;

    private static GameWorld? _world;
    private static CollisionWorld? _col;
    private static Player? _player;
    private static WeaponSystem? _weapons;
    private static PickupSystem? _pickups;
    private static BulletHoleManager? _holes;
    private static TracerSystem? _tracers;
    private static RocketSystem? _rockets;
    private static MuzzleFlash? _muzzleFlash;
    private static ScorchManager? _scorches;
    private static WorldRenderer? _worldRen;
    private static DecalRenderer? _decalRen;
    private static TracerRenderer? _tracerRen;
    private static HudRenderer? _hudRen;
    private static WeaponViewmodelRenderer? _viewmodelRen;
    private static RocketRenderer? _rocketRen;
    private static MuzzleFlashRenderer? _muzzleFlashRen;
    private static ScorchRenderer? _scorchRen;
    private static InputState _inputState = new();
    private static IMouse? _mouse;
    private static Vector2 _lastMouse;
    private static bool _firstMouse = true;
    private static bool _showDebug;
    private static float _fpsAccum;
    private static int _fpsFrames;
    private static float _fpsValue;

    private static string _mapPath = "";

    private static int Main(string[] args)
    {
        _mapPath = args.Length > 0 ? args[0] : ResolveDefaultMap();

        var opts = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1280, 720),
            Title = "Shooter",
            VSync = true,
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
        };
        _window = Window.Create(opts);
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += sz => _gl?.Viewport(sz);
        _window.Closing += OnClosing;
        _window.Run();
        return 0;
    }

    private static string ResolveDefaultMap()
    {
        // Try common spots near the working directory.
        string[] candidates = ["map.shmap", "default.shmap", "../../../../map.shmap"];
        foreach (var c in candidates) if (File.Exists(c)) return c;
        return "";
    }

    private static void OnLoad()
    {
        _gl = GL.GetApi(_window!);
        _input = _window!.CreateInput();

        // Capture mouse for FPS look.
        if (_input.Mice.Count > 0)
        {
            _mouse = _input.Mice[0];
            _mouse.Cursor.CursorMode = CursorMode.Raw;
            _mouse.MouseMove += OnMouseMove;
            _mouse.MouseDown += (m, b) => { if (b == MouseButton.Left) _inputState.SetDown(InputKey.MouseLeft, true); };
            _mouse.MouseUp += (m, b) => { if (b == MouseButton.Left) _inputState.SetDown(InputKey.MouseLeft, false); };
            _mouse.Scroll += (m, s) => _inputState.ScrollDelta += s.Y;
        }
        foreach (var kb in _input.Keyboards)
        {
            kb.KeyDown += (k, key, _) => SetKey(key, true);
            kb.KeyUp += (k, key, _) => SetKey(key, false);
        }

        // Load map
        Console.WriteLine($"[Shooter] Loading map: {_mapPath}");
        var scene = string.IsNullOrEmpty(_mapPath) || !File.Exists(_mapPath)
            ? BuildFallbackScene()
            : new MapFileService().LoadAsync(_mapPath).GetAwaiter().GetResult();

        _world = GameWorld.FromScene(scene);
        _col = new CollisionWorld(_world);
        _holes = new BulletHoleManager();
        _tracers = new TracerSystem();
        _rockets = new RocketSystem();
        _muzzleFlash = new MuzzleFlash();
        _scorches = new ScorchManager();
        _weapons = new WeaponSystem();
        _pickups = new PickupSystem(_world);
        _player = new Player();

        // Spawn: pick a spawn point if one exists, otherwise use the world origin.
        // Then snap straight down to the closest floor so we never spawn mid-air.
        var spawnPoint = _world.PlayerSpawns.Count > 0
            ? _world.PlayerSpawns[Random.Shared.Next(_world.PlayerSpawns.Count)]
            : Vector3.Zero;
        _player.Position = SnapSpawnToFloor(spawnPoint, _col);

        // Renderers
        _worldRen = new WorldRenderer(_gl, _world);
        _decalRen = new DecalRenderer(_gl);
        _tracerRen = new TracerRenderer(_gl);
        _hudRen = new HudRenderer(_gl);
        _viewmodelRen = new WeaponViewmodelRenderer(_gl);
        _rocketRen = new RocketRenderer(_gl);
        _muzzleFlashRen = new MuzzleFlashRenderer(_gl);
        _scorchRen = new ScorchRenderer(_gl);

        var sky = _world.SkyColor;
        _gl.ClearColor(sky.X, sky.Y, sky.Z, 1f);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Viewport(_window!.FramebufferSize);
        Console.WriteLine($"[Shooter] World: {_world.Brushes.Count} brushes, {_world.AllTriangles.Count} tris, {_world.PlayerSpawns.Count} spawns, {_world.Pickups.Count} pickups.");
    }

    /// <summary>Casts straight down from <paramref name="spawnPoint"/> to find the floor and
    /// returns a player-center position that rests on it. The collider is a sphere of
    /// <see cref="Player.Radius"/>, so we sit the center exactly that far above the floor.</summary>
    private static Vector3 SnapSpawnToFloor(Vector3 spawnPoint, CollisionWorld col)
    {
        // Start a couple of metres above the spawn so the ray begins above any surface near it,
        // and cast far enough down to reach a floor in any reasonable level.
        var rayOrigin = spawnPoint + new Vector3(0f, 2f, 0f);
        var hit = col.RayCast(rayOrigin, -Vector3.UnitY);
        if (hit.Hit && hit.Distance < 200f)
        {
            // Sphere collider: center sits Radius + skin above the floor surface.
            return new Vector3(spawnPoint.X, hit.Point.Y + Player.Radius + 0.02f, spawnPoint.Z);
        }
        // No floor below: keep the spawn but lift slightly so the player isn't intersecting it.
        return spawnPoint + new Vector3(0f, Player.Radius + 0.05f, 0f);
    }

    private static MapEditor.Core.Scene BuildFallbackScene()
    {
        // Procedural arena with cover, ramps, two side platforms, a central
        // raised platform, and a perimeter wall. All brushes are axis-aligned boxes
        // (with a couple of pitched ramp brushes).
        var scene = new MapEditor.Core.Scene();

        static MapEditor.Core.Entities.Brush Box(string name, Vector3 center, Vector3 size, float pitchDeg = 0f)
        {
            return new MapEditor.Core.Entities.Brush(Guid.NewGuid(), name)
            {
                Transform = MapEditor.Core.Entities.Transform.Identity with
                {
                    Position = center,
                    Scale = size,
                    EulerDegrees = new Vector3(pitchDeg, 0f, 0f),
                },
            };
        }

        // Floor (60 x 60, top surface at y = 0)
        AddBrushPublic(scene, Box("Floor", new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60)));

        // Perimeter walls (top at y = 5)
        AddBrushPublic(scene, Box("WallN", new Vector3(0, 2.5f, -30f), new Vector3(60, 5, 1)));
        AddBrushPublic(scene, Box("WallS", new Vector3(0, 2.5f, 30f), new Vector3(60, 5, 1)));
        AddBrushPublic(scene, Box("WallE", new Vector3(30f, 2.5f, 0), new Vector3(1, 5, 60)));
        AddBrushPublic(scene, Box("WallW", new Vector3(-30f, 2.5f, 0), new Vector3(1, 5, 60)));

        // Central raised platform (top at y = 1.6) with two pitched ramps approaching it.
        AddBrushPublic(scene, Box("CentralPlatform", new Vector3(0, 0.8f, 0), new Vector3(8, 1.6f, 8)));
        AddBrushPublic(scene, Box("RampN", new Vector3(0, 0.8f, -7f), new Vector3(3, 0.4f, 5.4f), pitchDeg: -17f));
        AddBrushPublic(scene, Box("RampS", new Vector3(0, 0.8f, 7f), new Vector3(3, 0.4f, 5.4f), pitchDeg: 17f));

        // Side platforms (top at y = 2.0)
        AddBrushPublic(scene, Box("PlatformE", new Vector3(11, 1.0f, -10), new Vector3(5, 2.0f, 5)));
        AddBrushPublic(scene, Box("PlatformW", new Vector3(-11, 1.0f, 10), new Vector3(5, 2.0f, 5)));

        // Cover columns (~3 m tall)
        AddBrushPublic(scene, Box("ColumnA", new Vector3(8, 1.5f, 8), new Vector3(1.2f, 3, 1.2f)));
        AddBrushPublic(scene, Box("ColumnB", new Vector3(-8, 1.5f, 8), new Vector3(1.2f, 3, 1.2f)));
        AddBrushPublic(scene, Box("ColumnC", new Vector3(8, 1.5f, -8), new Vector3(1.2f, 3, 1.2f)));
        AddBrushPublic(scene, Box("ColumnD", new Vector3(-8, 1.5f, -8), new Vector3(1.2f, 3, 1.2f)));

        // Crates (chest height) for snap-cover
        AddBrushPublic(scene, Box("CrateE1", new Vector3(15, 0.6f, 0), new Vector3(2, 1.2f, 2)));
        AddBrushPublic(scene, Box("CrateE2", new Vector3(18, 0.6f, 4), new Vector3(2, 1.2f, 2)));
        AddBrushPublic(scene, Box("CrateW1", new Vector3(-15, 0.6f, 0), new Vector3(2, 1.2f, 2)));
        AddBrushPublic(scene, Box("CrateW2", new Vector3(-18, 0.6f, -4), new Vector3(2, 1.2f, 2)));

        // Half-walls forming a small alley near the south wall
        AddBrushPublic(scene, Box("AlleyN", new Vector3(-2.5f, 0.75f, 18), new Vector3(7, 1.5f, 0.6f)));
        AddBrushPublic(scene, Box("AlleyS", new Vector3(2.5f, 0.75f, 22), new Vector3(7, 1.5f, 0.6f)));

        // Spawn point on the floor near the south side, facing north into the arena.
        var sp = new MapEditor.Core.Entities.SpawnPoint(Guid.NewGuid(), "Spawn")
        {
            SpawnType = "player",
            Transform = MapEditor.Core.Entities.Transform.Identity with { Position = new Vector3(0, 0.1f, 20) },
        };
        AddSpawnPublic(scene, sp);

        // Pickups scattered around the arena.
        AddPickupPublic(scene, MapEditor.Core.Entities.PickupKind.HealthSmall, new Vector3(11, 2.3f, -10));   // east platform
        AddPickupPublic(scene, MapEditor.Core.Entities.PickupKind.HealthLarge, new Vector3(0, 1.9f, 0));       // top of central platform
        AddPickupPublic(scene, MapEditor.Core.Entities.PickupKind.AmmoAk47, new Vector3(15, 1.4f, 0));         // on a crate
        AddPickupPublic(scene, MapEditor.Core.Entities.PickupKind.AmmoShotgun, new Vector3(-15, 1.4f, 0));     // on a crate
        AddPickupPublic(scene, MapEditor.Core.Entities.PickupKind.AmmoRocket, new Vector3(-11, 2.3f, 10));     // west platform
        AddPickupPublic(scene, MapEditor.Core.Entities.PickupKind.WeaponShotgun, new Vector3(-2.5f, 1.6f, 18));// in alley
        AddPickupPublic(scene, MapEditor.Core.Entities.PickupKind.WeaponRocketLauncher, new Vector3(0, 0.5f, -22));

        return scene;
    }

    // Use the public command path so we don't need internal access.
    private static void AddBrushPublic(MapEditor.Core.Scene s, MapEditor.Core.Entities.Brush b)
        => new MapEditor.Core.Commands.CreateBrushCommand(s, b).Execute();
    private static void AddSpawnPublic(MapEditor.Core.Scene s, MapEditor.Core.Entities.SpawnPoint sp)
        => new MapEditor.Core.Commands.CreateSpawnPointCommand(s, sp).Execute();
    private static void AddPickupPublic(MapEditor.Core.Scene s, MapEditor.Core.Entities.PickupKind k, Vector3 pos)
        => new MapEditor.Core.Commands.CreatePickupCommand(s,
            new MapEditor.Core.Entities.PickupEntity(Guid.NewGuid(), k.ToString())
            {
                Kind = k,
                Transform = MapEditor.Core.Entities.Transform.Identity with { Position = pos }
            }).Execute();

    private static void OnMouseMove(IMouse m, Vector2 pos)
    {
        if (_firstMouse) { _lastMouse = pos; _firstMouse = false; return; }
        var d = pos - _lastMouse;
        _lastMouse = pos;
        _inputState.MouseDelta += d;
    }

    private static void SetKey(Key key, bool down)
    {
        InputKey? mapped = key switch
        {
            Key.W => InputKey.W,
            Key.A => InputKey.A,
            Key.S => InputKey.S,
            Key.D => InputKey.D,
            Key.Space => InputKey.Space,
            Key.ShiftLeft or Key.ShiftRight => InputKey.Shift,
            Key.ControlLeft or Key.ControlRight => InputKey.Ctrl,
            Key.Escape => InputKey.Esc,
            Key.F1 => InputKey.F1,
            Key.Number1 or Key.Keypad1 => InputKey.Num1,
            Key.Number2 or Key.Keypad2 => InputKey.Num2,
            Key.Number3 or Key.Keypad3 => InputKey.Num3,
            _ => null,
        };
        if (mapped is { } mk) _inputState.SetDown(mk, down);
    }

    private static void OnUpdate(double dt)
    {
        if (_player is null || _col is null || _weapons is null || _pickups is null || _holes is null || _tracers is null || _rockets is null || _muzzleFlash is null || _scorches is null) return;
        // Clamp frame dt: the first frame after asset upload (textures, shaders) can be hundreds
        // of ms, which would otherwise let gravity tunnel the player through the floor.
        float fdt = MathF.Min((float)dt, 1f / 30f);

        if (_inputState.WasPressed(InputKey.Esc)) { _window?.Close(); return; }
        if (_inputState.WasPressed(InputKey.F1)) _showDebug = !_showDebug;

        _player.Update(fdt, _inputState, _col);
        _weapons.Update(fdt);
        _weapons.HandleSelectInput(_inputState);
        _pickups.Update(fdt, _player, _weapons);
        _tracers.Update(fdt);
        _rockets.Update(fdt, _col);
        _muzzleFlash.Update(fdt);

        // On rocket detonation: leave one big scorch decal centred on the impact point.
        foreach (var det in _rockets.Detonations)
        {
            if (det.ImpactPoint is { } pt && det.ImpactNormal is { } n)
            {
                _scorches.Add(pt, n,
                det.SplashRadius);
            }
        }

        // Fire
        bool firePressed = _inputState.WasPressed(InputKey.MouseLeft);
        bool fireHeld = _inputState.IsDown(InputKey.MouseLeft);
        bool wantFire = _weapons.Current.Def.Automatic ? fireHeld : firePressed;
        if (wantFire)
        {
            var origin = _player.EyePosition;
            var fwd = _player.Forward();
            var right = _player.Right();
            var up = Vector3.Cross(right, fwd); // camera up
            // Convert the shared view-space muzzle anchor to world space so tracers / rockets
            // visibly leave the rendered barrel tip (FOVs are matched).
            var m = WeaponViewmodelRenderer.MuzzleViewOffset;
            var muzzle = origin + right * m.X + up * m.Y + fwd * (-m.Z);
            var result = _weapons.TryFire(origin, fwd, _col, triggerHeld: true);
            if (result.Fired)
            {
                // Per-weapon flash anchor (view-space) + scale. Tuned so each weapon's flash
                // sits over the visible nozzle.
                (Vector3 flashOffset, float flashScale) = _weapons.Current.Def.Kind switch
                {
                    WeaponKind.Ak47 => (new Vector3(0.27f, -0.16f, -0.80f), 1.05f),
                    WeaponKind.Shotgun => (new Vector3(0.20f, -0.16f, -0.62f), 1.20f),
                    WeaponKind.RocketLauncher => (new Vector3(0.20f, -0.13f, -0.78f), 2.80f),
                    _ => (new Vector3(0.20f, -0.16f, -0.61f), 1.0f),
                };
                _muzzleFlash.Trigger(flashOffset, flashScale);

                if (result.Projectile is { } proj)
                {
                    // Rocket: spawn a real projectile from the muzzle. No tracer or bullet hole;
                    // the rocket itself + scorch on detonation handle visual feedback.
                    _rockets.Spawn(muzzle, proj.Direction, proj.Speed, proj.Damage, proj.SplashRadius);
                }
                else
                {
                    // Hitscan: register decals at every hit point.
                    foreach (var h in result.Hits) _holes.Add(h.Point, h.Normal);
                }
            }
        }

        // FPS
        _fpsAccum += fdt; _fpsFrames++;
        if (_fpsAccum >= 0.5f) { _fpsValue = _fpsFrames / _fpsAccum; _fpsAccum = 0; _fpsFrames = 0; }

        _inputState.EndFrame();
    }

    private static void OnRender(double dt)
    {
        if (_gl is null || _player is null || _world is null || _worldRen is null || _hudRen is null || _decalRen is null || _tracerRen is null || _holes is null || _tracers is null || _weapons is null || _pickups is null || _rockets is null || _scorches is null) return;

        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var fb = _window!.FramebufferSize;
        float aspect = fb.Y > 0 ? (float)fb.X / fb.Y : 16f / 9f;
        var view = Matrix4x4.CreateLookAt(_player.EyePosition, _player.EyePosition + _player.Forward(), Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(WeaponViewmodelRenderer.FovYRadians, aspect, 0.05f, 1000f);
        var viewProj = view * proj;

        _worldRen.Draw(viewProj, _world, _pickups);
        _decalRen.Draw(viewProj, _holes);
        _scorchRen?.Draw(viewProj, _scorches);
        _tracerRen.Draw(viewProj, _tracers);
        _rocketRen?.Draw(viewProj, _rockets);
        _viewmodelRen?.Draw(fb.X, fb.Y, _weapons);
        if (_muzzleFlash is not null) _muzzleFlashRen?.Draw(fb.X, fb.Y, _muzzleFlash);
        _hudRen.Draw(fb.X, fb.Y, _player, _weapons);

        if (_showDebug)
        {
            Console.Title = $"Shooter | pos={_player.Position:F1} fps={_fpsValue:F0} tris={_world.AllTriangles.Count} holes={_holes.Count} tracers={_tracers.Active.Count}";
        }
    }

    private static void OnClosing()
    {
        _scorchRen?.Dispose();
        _muzzleFlashRen?.Dispose();
        _rocketRen?.Dispose();
        _viewmodelRen?.Dispose();
        _hudRen?.Dispose();
        _tracerRen?.Dispose();
        _decalRen?.Dispose();
        _worldRen?.Dispose();
        _input?.Dispose();
    }
}
