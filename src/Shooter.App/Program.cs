using System.Numerics;
using MapEditor.Formats;
using Shooter.Game;
using Shooter.Input;
using Shooter.RenderSystem;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Shooter;

internal static class Program
{
    private static IWindow? _window;
    private static IInputContext? _input;
    private static IRenderBackend? _renderBackend;
    private static GameSession? _session;
    private static InputState _inputState = new();
    private static IMouse? _mouse;
    private static Vector2 _lastMouse;
    private static bool _firstMouse = true;
    private static bool _backendInitFailed;

    private static string _mapPath = "";

    private static int Main(string[] args)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                _mapPath = arg;
        }
        if (string.IsNullOrEmpty(_mapPath)) _mapPath = ResolveDefaultMap();

        var api = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3));

        var opts = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1280, 720),
            Title = "Shooter (OpenGL)",
            VSync = true,
            API = api,
        };
        _window = Window.Create(opts);
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += sz => _renderBackend?.Resize(sz.X, sz.Y);
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

        Console.WriteLine($"[Shooter] Loading map: {_mapPath}");
        var scene = string.IsNullOrEmpty(_mapPath) || !File.Exists(_mapPath)
            ? FallbackSceneFactory.Build()
            : new MapFileService().LoadAsync(_mapPath).GetAwaiter().GetResult();

        _session = GameSession.Create(scene);
        _renderBackend = new OpenGLRenderBackend();
        try
        {
            _renderBackend.Initialize(_window!, _session.World, _session.Lighting);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Renderer] OpenGL initialization failed: {ex.Message}");
            _backendInitFailed = true;
            _window!.Title = "Shooter (OpenGL) - backend init failed";
            _window.Close();
            return;
        }

        Console.WriteLine($"[Shooter] World: {_session.World.Brushes.Count} brushes, {_session.World.AllTriangles.Count} tris, {_session.World.PlayerSpawns.Count} spawns, {_session.World.Pickups.Count} pickups.");
    }

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
        if (_backendInitFailed || _session is null) return;
        float fdt = MathF.Min((float)dt, 1f / 30f);

        if (_inputState.WasPressed(InputKey.Esc)) { _window?.Close(); return; }
        if (_inputState.WasPressed(InputKey.F1)) _session.ToggleDebug();

        _session.Update(fdt, _inputState);
    }

    private static void OnRender(double dt)
    {
        if (_backendInitFailed || _renderBackend is null || _session is null) return;
        _renderBackend.Render(dt, _session.CreateFrameData());
    }

    private static void OnClosing()
    {
        _renderBackend?.Dispose();
        _input?.Dispose();
    }
}
