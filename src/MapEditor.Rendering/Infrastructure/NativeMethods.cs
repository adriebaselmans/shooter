using System.Runtime.InteropServices;

namespace MapEditor.Rendering.Infrastructure;

/// <summary>
/// P/Invoke declarations for Win32 window creation and WGL (Windows OpenGL) context management.
/// All OpenGL integration with WPF goes through these native calls.
/// </summary>
internal static class NativeMethods
{
    // ── Win32 Window ─────────────────────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    internal static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    internal static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    // ── WGL / GDI Pixel Format ────────────────────────────────────────────────

    [DllImport("gdi32.dll")]
    internal static extern int ChoosePixelFormat(IntPtr hdc, ref PixelFormatDescriptor pfd);

    [DllImport("gdi32.dll")]
    internal static extern bool SetPixelFormat(IntPtr hdc, int format, ref PixelFormatDescriptor pfd);

    [DllImport("gdi32.dll")]
    internal static extern bool SwapBuffers(IntPtr hdc);

    // ── WGL Context ──────────────────────────────────────────────────────────

    [DllImport("opengl32.dll")]
    internal static extern IntPtr wglCreateContext(IntPtr hdc);

    [DllImport("opengl32.dll")]
    internal static extern bool wglDeleteContext(IntPtr hrc);

    [DllImport("opengl32.dll")]
    internal static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hrc);

    [DllImport("opengl32.dll")]
    internal static extern bool wglShareLists(IntPtr hrc1, IntPtr hrc2);

    [DllImport("opengl32.dll")]
    internal static extern IntPtr wglGetProcAddress(string procName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, EntryPoint = "GetProcAddress")]
    private static extern IntPtr GetProcAddressNative(IntPtr hModule, string procName);

    // ── WGL_ARB_create_context (OpenGL 4.5 core profile) ─────────────────────

    internal const int WGL_CONTEXT_MAJOR_VERSION_ARB = 0x2091;
    internal const int WGL_CONTEXT_MINOR_VERSION_ARB = 0x2092;
    internal const int WGL_CONTEXT_FLAGS_ARB         = 0x2094;
    internal const int WGL_CONTEXT_PROFILE_MASK_ARB  = 0x9126;
    internal const int WGL_CONTEXT_CORE_PROFILE_BIT_ARB = 0x0001;
    internal const int WGL_CONTEXT_DEBUG_BIT_ARB     = 0x0001;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate IntPtr WglCreateContextAttribsARBDelegate(
        IntPtr hDC, IntPtr hShareContext, int[] attribList);

    internal static WglCreateContextAttribsARBDelegate? wglCreateContextAttribsARB;

    internal static void LoadWglExtensions(IntPtr hdc)
    {
        // Create a temporary legacy context to bootstrap ARB extension loading
        var tempCtx = wglCreateContext(hdc);
        wglMakeCurrent(hdc, tempCtx);

        var ptr = wglGetProcAddress("wglCreateContextAttribsARB");
        if (ptr != IntPtr.Zero)
            wglCreateContextAttribsARB = Marshal.GetDelegateForFunctionPointer<WglCreateContextAttribsARBDelegate>(ptr);

        wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        wglDeleteContext(tempCtx);
    }

    internal static IntPtr GetOpenGlProcAddress(string procName)
    {
        var proc = wglGetProcAddress(procName);
        if (IsValidOpenGlProcAddress(proc))
        {
            return proc;
        }

        var module = GetModuleHandle("opengl32.dll");
        return module == IntPtr.Zero ? IntPtr.Zero : GetProcAddressNative(module, procName);
    }

    private static bool IsValidOpenGlProcAddress(IntPtr proc) =>
        proc != IntPtr.Zero &&
        proc != new IntPtr(1) &&
        proc != new IntPtr(2) &&
        proc != new IntPtr(3) &&
        proc != new IntPtr(-1);

    // ── Win32 constants ───────────────────────────────────────────────────────

    internal const uint WS_CHILD   = 0x40000000;
    internal const uint WS_OVERLAPPED = 0x00000000;
    internal const uint WS_VISIBLE = 0x10000000;
    internal const uint WS_CLIPCHILDREN  = 0x02000000;
    internal const uint WS_CLIPSIBLINGS  = 0x04000000;
}

/// <summary>GDI PIXELFORMATDESCRIPTOR structure.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PixelFormatDescriptor
{
    public ushort nSize;
    public ushort nVersion;
    public uint   dwFlags;
    public byte   iPixelType;
    public byte   cColorBits;
    public byte   cRedBits, cRedShift;
    public byte   cGreenBits, cGreenShift;
    public byte   cBlueBits, cBlueShift;
    public byte   cAlphaBits, cAlphaShift;
    public byte   cAccumBits, cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits;
    public byte   cDepthBits;
    public byte   cStencilBits;
    public byte   cAuxBuffers;
    public byte   iLayerType;
    public byte   bReserved;
    public uint   dwLayerMask, dwVisibleMask, dwDamageMask;

    internal const uint PFD_DRAW_TO_WINDOW = 0x00000004;
    internal const uint PFD_SUPPORT_OPENGL = 0x00000020;
    internal const uint PFD_DOUBLEBUFFER   = 0x00000001;
    internal const byte PFD_TYPE_RGBA      = 0;
    internal const byte PFD_MAIN_PLANE     = 0;

    public static PixelFormatDescriptor Default() => new()
    {
        nSize      = (ushort)Marshal.SizeOf<PixelFormatDescriptor>(),
        nVersion   = 1,
        dwFlags    = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
        iPixelType = PFD_TYPE_RGBA,
        cColorBits = 32,
        cDepthBits = 24,
        cStencilBits = 8,
        iLayerType = PFD_MAIN_PLANE
    };
}
