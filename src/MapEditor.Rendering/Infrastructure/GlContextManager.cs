using Silk.NET.OpenGL;
using System.Runtime.InteropServices;

namespace MapEditor.Rendering.Infrastructure;

/// <summary>
/// Manages the shared WGL OpenGL context used for GPU resource sharing across all viewports.
/// Must be initialised on the UI thread before any viewport is created.
/// </summary>
public sealed class GlContextManager : IDisposable
{
    private static GlContextManager? _instance;
    public static GlContextManager Instance => _instance ?? throw new InvalidOperationException(
        "GlContextManager has not been initialised. Call Initialise() first.");

    private IntPtr _resourceHwnd;
    private IntPtr _resourceHdc;
    private IntPtr _resourceHrc;
    private GL? _gl;

    public IntPtr SharedHrc => _resourceHrc;

    public static IntPtr GetOpenGlProcAddress(string procName) =>
        NativeMethods.GetOpenGlProcAddress(procName);

    /// <summary>Creates the shared resource context. Call once at application startup.</summary>
    public static void Initialise()
    {
        if (_instance != null) return;
        _instance = new GlContextManager();
        _instance.CreateResourceContext();
    }

    private void CreateResourceContext()
    {
        // Invisible helper window for the shared context
        _resourceHwnd = NativeMethods.CreateWindowEx(
            0, "STATIC", "GLResourceContext",
            NativeMethods.WS_OVERLAPPED,
            0, 0, 1, 1,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (_resourceHwnd == IntPtr.Zero)
        {
            throw CreateWin32Exception("Failed to create the shared OpenGL helper window.");
        }

        _resourceHdc = NativeMethods.GetDC(_resourceHwnd);
        if (_resourceHdc == IntPtr.Zero)
        {
            throw CreateWin32Exception("Failed to get a device context for the shared OpenGL helper window.");
        }

        var pfd = PixelFormatDescriptor.Default();
        int fmt = NativeMethods.ChoosePixelFormat(_resourceHdc, ref pfd);
        if (fmt == 0)
        {
            throw CreateWin32Exception("Failed to choose a pixel format for the shared OpenGL helper window.");
        }

        if (!NativeMethods.SetPixelFormat(_resourceHdc, fmt, ref pfd))
        {
            throw CreateWin32Exception("Failed to set a pixel format for the shared OpenGL helper window.");
        }

        NativeMethods.LoadWglExtensions(_resourceHdc);

        _resourceHrc = CreateCoreProfileContext(_resourceHdc, IntPtr.Zero);
        if (_resourceHrc == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create the shared OpenGL context.");
        }

        NativeMethods.wglMakeCurrent(_resourceHdc, _resourceHrc);

        _gl = GL.GetApi(NativeMethods.GetOpenGlProcAddress);

        NativeMethods.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Creates a new OpenGL 4.5 core-profile context that shares resources with the master context.</summary>
    public IntPtr CreateSharedViewportContext(IntPtr hdc)
    {
        if (hdc == IntPtr.Zero)
        {
            throw new InvalidOperationException("Cannot create a viewport OpenGL context with a null device context.");
        }

        var pfd = PixelFormatDescriptor.Default();
        int fmt = NativeMethods.ChoosePixelFormat(hdc, ref pfd);
        if (fmt == 0)
        {
            throw CreateWin32Exception("Failed to choose a pixel format for a viewport OpenGL context.");
        }

        if (!NativeMethods.SetPixelFormat(hdc, fmt, ref pfd))
        {
            throw CreateWin32Exception("Failed to set a pixel format for a viewport OpenGL context.");
        }

        var ctx = CreateCoreProfileContext(hdc, _resourceHrc);
        if (ctx == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create a shared viewport OpenGL context.");
        }

        return ctx;
    }

    /// <summary>Returns a Silk.NET GL instance bound to a context made current on the calling thread.</summary>
    public GL GetGl() => _gl ?? throw new InvalidOperationException("GL not initialised.");

    private static IntPtr CreateCoreProfileContext(IntPtr hdc, IntPtr shareContext)
    {
        if (NativeMethods.wglCreateContextAttribsARB != null)
        {
            int[] attribs =
            [
                NativeMethods.WGL_CONTEXT_MAJOR_VERSION_ARB, 4,
                NativeMethods.WGL_CONTEXT_MINOR_VERSION_ARB, 5,
                NativeMethods.WGL_CONTEXT_PROFILE_MASK_ARB, NativeMethods.WGL_CONTEXT_CORE_PROFILE_BIT_ARB,
                0
            ];
            var ctx = NativeMethods.wglCreateContextAttribsARB(hdc, shareContext, attribs);
            if (ctx != IntPtr.Zero) return ctx;
        }

        // Fallback: legacy context (no profile)
        var legacyCtx = NativeMethods.wglCreateContext(hdc);
        if (legacyCtx == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        if (shareContext != IntPtr.Zero)
        {
            if (!NativeMethods.wglShareLists(shareContext, legacyCtx))
            {
                NativeMethods.wglDeleteContext(legacyCtx);
                throw new InvalidOperationException("Failed to share OpenGL resources with the viewport context.");
            }
        }

        return legacyCtx;
    }

    private static InvalidOperationException CreateWin32Exception(string message)
    {
        var error = Marshal.GetLastWin32Error();
        return new InvalidOperationException($"{message} Win32 error: {error}.");
    }

    public void Dispose()
    {
        if (_resourceHrc != IntPtr.Zero)
        {
            NativeMethods.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            NativeMethods.wglDeleteContext(_resourceHrc);
            _resourceHrc = IntPtr.Zero;
        }
        if (_resourceHwnd != IntPtr.Zero)
        {
            NativeMethods.ReleaseDC(_resourceHwnd, _resourceHdc);
            NativeMethods.DestroyWindow(_resourceHwnd);
            _resourceHwnd = IntPtr.Zero;
        }
        _instance = null;
    }
}
