using MapEditor.App.Tools;
using MapEditor.Rendering.Infrastructure;
using Silk.NET.OpenGL;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Input;

namespace MapEditor.App.Views;

/// <summary>
/// WPF HwndHost that owns a Win32 child window with a dedicated WGL OpenGL context.
/// Hosts one of the four editor viewports.
/// </summary>
public sealed class GlViewportHost : HwndHost
{
    // ── Win32 P/Invoke ────────────────────────────────────────────────────────

    private const string ViewportWindowClassName = "MapEditor.GlViewportHost";
    private const int ERROR_CLASS_ALREADY_EXISTS = 1410;
    private const uint CS_OWNDC = 0x0020;
    private const uint WS_CHILD         = 0x40000000;
    private const uint WS_VISIBLE       = 0x10000000;
    private const uint WS_CLIPCHILDREN  = 0x02000000;
    private const uint WS_CLIPSIBLINGS  = 0x04000000;
    private const uint WM_MOUSEMOVE     = 0x0200;
    private const uint WM_LBUTTONDOWN   = 0x0201;
    private const uint WM_LBUTTONUP     = 0x0202;
    private const uint WM_RBUTTONDOWN   = 0x0204;
    private const uint WM_RBUTTONUP     = 0x0205;
    private const uint WM_MBUTTONDOWN   = 0x0207;
    private const uint WM_MBUTTONUP     = 0x0208;
    private const uint WM_MOUSEWHEEL    = 0x020A;
    private const uint WM_MOUSELEAVE    = 0x02A3;
    private const uint WM_KEYDOWN       = 0x0100;
    private const uint WM_SYSKEYDOWN    = 0x0104;

    private static readonly object WindowClassRegistrationLock = new();
    private static readonly WindowProcedure InitialWindowProcedure = DefWindowProc;
    private static bool _windowClassRegistered;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClass
    {
        public uint style;
        public WindowProcedure lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private delegate IntPtr WindowProcedure(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] private static extern IntPtr SetCapture(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WndClass wndClass);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ScreenToClient(IntPtr hWnd, ref NativePoint lpPoint);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("gdi32.dll")]  private static extern bool SwapBuffers(IntPtr hdc);

    [DllImport("opengl32.dll")] private static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hrc);
    [DllImport("opengl32.dll")] private static extern bool wglDeleteContext(IntPtr hrc);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    // ── Fields ────────────────────────────────────────────────────────────────

    private IntPtr _childHwnd;
    private IntPtr _hdc;
    private IntPtr _glContext;
    private GL?    _gl;
    private bool   _contextReady;
    private int    _pixelWidth;
    private int    _pixelHeight;

    /// <summary>Fired on the UI thread for each render frame. Subscriber renders into the GL context.</summary>
    public event EventHandler<GL>? RenderFrame;
    public event EventHandler<ViewportPointerEvent>? PointerInput;
    public event EventHandler<ViewportKeyEvent>? KeyInput;

    public GL? GL => _gl;
    public int PixelWidth  => _pixelWidth;
    public int PixelHeight => _pixelHeight;

    // ── HwndHost overrides ────────────────────────────────────────────────────

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        EnsureViewportWindowClassRegistered();

        _childHwnd = CreateWindowEx(
            0, ViewportWindowClassName, string.Empty,
            WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN,
            0, 0, 1, 1,
            hwndParent.Handle, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);

        if (_childHwnd == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Failed to create child HWND for GL viewport. Win32 error: {Marshal.GetLastWin32Error()}");

        _hdc       = GetDC(_childHwnd);
        _glContext = GlContextManager.Instance.CreateSharedViewportContext(_hdc);
        _gl        = Silk.NET.OpenGL.GL.GetApi(GlContextManager.GetOpenGlProcAddress);
        _contextReady = true;

        CompositionTarget.Rendering += OnCompositionTargetRendering;

        return new HandleRef(this, _childHwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        CompositionTarget.Rendering -= OnCompositionTargetRendering;

        if (_glContext != IntPtr.Zero)
        {
            wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            wglDeleteContext(_glContext);
            _glContext = IntPtr.Zero;
        }

        if (_hdc != IntPtr.Zero && _childHwnd != IntPtr.Zero)
        {
            ReleaseDC(_childHwnd, _hdc);
            _hdc = IntPtr.Zero;
        }

        if (hwnd.Handle != IntPtr.Zero)
            DestroyWindow(hwnd.Handle);
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        if (!_contextReady || _childHwnd == IntPtr.Zero) return;

        var presentation = PresentationSource.FromVisual(this);
        double dpiX = presentation?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = presentation?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        _pixelWidth  = (int)(ActualWidth  * dpiX);
        _pixelHeight = (int)(ActualHeight * dpiY);

        if (_pixelWidth <= 0 || _pixelHeight <= 0) return;

        MoveWindow(_childHwnd, 0, 0, _pixelWidth, _pixelHeight, true);
        wglMakeCurrent(_hdc, _glContext);

        RenderFrame?.Invoke(this, _gl!);

        SwapBuffers(_hdc);
        wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
    }

    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch ((uint)msg)
        {
            case WM_LBUTTONDOWN:
                SetFocus(hwnd);
                SetCapture(hwnd);
                RaisePointerInput(ViewportPointerAction.Down, ViewportPointerButton.Left, lParam, 0);
                handled = true;
                break;
            case WM_LBUTTONUP:
                ReleaseCapture();
                RaisePointerInput(ViewportPointerAction.Up, ViewportPointerButton.Left, lParam, 0);
                handled = true;
                break;
            case WM_RBUTTONDOWN:
                SetFocus(hwnd);
                SetCapture(hwnd);
                RaisePointerInput(ViewportPointerAction.Down, ViewportPointerButton.Right, lParam, 0);
                handled = true;
                break;
            case WM_RBUTTONUP:
                ReleaseCapture();
                RaisePointerInput(ViewportPointerAction.Up, ViewportPointerButton.Right, lParam, 0);
                handled = true;
                break;
            case WM_MBUTTONDOWN:
                SetFocus(hwnd);
                SetCapture(hwnd);
                RaisePointerInput(ViewportPointerAction.Down, ViewportPointerButton.Middle, lParam, 0);
                handled = true;
                break;
            case WM_MBUTTONUP:
                ReleaseCapture();
                RaisePointerInput(ViewportPointerAction.Up, ViewportPointerButton.Middle, lParam, 0);
                handled = true;
                break;
            case WM_MOUSEMOVE:
                RaisePointerInput(ViewportPointerAction.Move, ViewportPointerButton.None, lParam, 0);
                handled = false;
                break;
            case WM_MOUSEWHEEL:
                RaisePointerInput(
                    ViewportPointerAction.Wheel,
                    ViewportPointerButton.None,
                    GetClientPointFromScreenLParam(hwnd, lParam),
                    GetWheelDelta(wParam));
                handled = true;
                break;
            case WM_MOUSELEAVE:
                RaisePointerInput(ViewportPointerAction.Leave, ViewportPointerButton.None, lParam, 0);
                handled = false;
                break;
            case WM_KEYDOWN:
            case WM_SYSKEYDOWN:
                var keyEvent = new ViewportKeyEvent(
                    KeyInterop.KeyFromVirtualKey(unchecked((int)(long)wParam)),
                    Keyboard.Modifiers);
                KeyInput?.Invoke(this, keyEvent);
                handled = keyEvent.Handled;
                break;
        }

        return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
    }

    private static void EnsureViewportWindowClassRegistered()
    {
        if (_windowClassRegistered)
        {
            return;
        }

        lock (WindowClassRegistrationLock)
        {
            if (_windowClassRegistered)
            {
                return;
            }

            var windowClass = new WndClass
            {
                style = CS_OWNDC,
                lpfnWndProc = InitialWindowProcedure,
                hInstance = GetModuleHandle(null),
                lpszClassName = ViewportWindowClassName
            };

            ushort atom = RegisterClass(ref windowClass);
            int error = Marshal.GetLastWin32Error();
            if (atom == 0 && error != ERROR_CLASS_ALREADY_EXISTS)
            {
                throw new InvalidOperationException(
                    $"Failed to register GL viewport window class. Win32 error: {error}");
            }

            _windowClassRegistered = true;
        }
    }

    private void RaisePointerInput(ViewportPointerAction action, ViewportPointerButton button, IntPtr lParam, int wheelDelta) =>
        RaisePointerInput(action, button, GetPointLParam(lParam), wheelDelta);

    private void RaisePointerInput(ViewportPointerAction action, ViewportPointerButton button, Point position, int wheelDelta)
    {
        PointerInput?.Invoke(this, new ViewportPointerEvent(
            action,
            button,
            position,
            wheelDelta,
            Keyboard.Modifiers));
    }

    private static Point GetPointLParam(IntPtr lParam)
    {
        return NativeMessageDecoder.GetPoint(lParam);
    }

    private static Point GetClientPointFromScreenLParam(IntPtr hwnd, IntPtr lParam)
    {
        var screenPoint = GetPointLParam(lParam);
        var nativePoint = new NativePoint
        {
            X = (int)screenPoint.X,
            Y = (int)screenPoint.Y
        };

        if (!ScreenToClient(hwnd, ref nativePoint))
        {
            throw new InvalidOperationException(
                $"Failed to convert screen coordinates to client coordinates. Win32 error: {Marshal.GetLastWin32Error()}");
        }

        return new Point(nativePoint.X, nativePoint.Y);
    }

    private static int GetWheelDelta(IntPtr wParam)
        => NativeMessageDecoder.GetWheelDelta(wParam);
}
