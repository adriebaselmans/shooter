using MapEditor.Rendering.Infrastructure;
using Silk.NET.OpenGL;
using System.Runtime.InteropServices;

namespace MapEditor.Rendering.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlRenderCollectionDefinition : ICollectionFixture<GlRenderCollectionFixture>
{
    public const string Name = "gl-render";
}

public sealed class GlRenderCollectionFixture : IDisposable
{
    public GlRenderCollectionFixture()
    {
        GlContextManager.Initialise();
    }

    public void Dispose()
    {
        GlContextManager.Instance.Dispose();
    }
}

internal sealed class ViewportRenderTestSurface : IDisposable
{
    private const uint WsOverlapped = 0x00000000;
    private readonly IntPtr _hwnd;
    private readonly IntPtr _hdc;
    private readonly IntPtr _context;
    private readonly uint _framebuffer;
    private readonly uint _colorTexture;
    private readonly uint _depthStencilRenderbuffer;
    private bool _disposed;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("opengl32.dll")]
    private static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hrc);

    [DllImport("opengl32.dll")]
    private static extern bool wglDeleteContext(IntPtr hrc);

    [DllImport("opengl32.dll")]
    private static extern IntPtr wglGetProcAddress(string procName);

    public ViewportRenderTestSurface(int width = 256, int height = 256)
    {
        Width = width;
        Height = height;

        _hwnd = CreateWindowEx(
            0,
            "STATIC",
            "ViewportRenderTestSurface",
            WsOverlapped,
            0,
            0,
            width,
            height,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to create test render window. Win32 error: {Marshal.GetLastWin32Error()}.");
        }

        _hdc = GetDC(_hwnd);
        if (_hdc == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to get test render device context. Win32 error: {Marshal.GetLastWin32Error()}.");
        }

        _context = GlContextManager.Instance.CreateSharedViewportContext(_hdc);
        if (_context == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create OpenGL viewport test context.");
        }

        MakeCurrent();
        Gl = GL.GetApi(wglGetProcAddress);
        (_framebuffer, _colorTexture, _depthStencilRenderbuffer) = CreateFramebuffer();
    }

    public GL Gl { get; }
    public int Width { get; }
    public int Height { get; }

    public void MakeCurrent()
    {
        if (!wglMakeCurrent(_hdc, _context))
        {
            throw new InvalidOperationException($"Failed to make OpenGL test context current. Win32 error: {Marshal.GetLastWin32Error()}.");
        }
    }

    public PixelSnapshot Render(Action renderAction)
    {
        MakeCurrent();
        Gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        renderAction();
        Gl.Finish();
        var snapshot = Capture();
        Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return snapshot;
    }

    private unsafe PixelSnapshot Capture()
    {
        var pixels = new byte[Width * Height * 4];
        fixed (byte* ptr = pixels)
        {
            Gl.ReadPixels(0, 0, (uint)Width, (uint)Height, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        return new PixelSnapshot(Width, Height, pixels);
    }

    private unsafe (uint Framebuffer, uint ColorTexture, uint DepthStencilRenderbuffer) CreateFramebuffer()
    {
        uint framebuffer = Gl.GenFramebuffer();
        uint colorTexture = Gl.GenTexture();
        uint depthStencilRenderbuffer = Gl.GenRenderbuffer();

        Gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);

        Gl.BindTexture(TextureTarget.Texture2D, colorTexture);
        Gl.TexImage2D(
            TextureTarget.Texture2D,
            0,
            InternalFormat.Rgba8,
            (uint)Width,
            (uint)Height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            null);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        Gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            colorTexture,
            0);

        Gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthStencilRenderbuffer);
        Gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)Width, (uint)Height);
        Gl.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthStencilAttachment,
            RenderbufferTarget.Renderbuffer,
            depthStencilRenderbuffer);

        var status = Gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException($"Incomplete test framebuffer: {status}.");
        }

        Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return (framebuffer, colorTexture, depthStencilRenderbuffer);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);

        if (_context != IntPtr.Zero)
        {
            wglDeleteContext(_context);
        }

        if (_hdc != IntPtr.Zero)
        {
            ReleaseDC(_hwnd, _hdc);
        }

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
        }
        _disposed = true;
    }
}

internal readonly record struct PixelSnapshot(int Width, int Height, byte[] Rgba)
{
    public int CountWhere(Func<byte, byte, byte, byte, bool> predicate)
    {
        int count = 0;
        for (int index = 0; index < Rgba.Length; index += 4)
        {
            if (predicate(Rgba[index], Rgba[index + 1], Rgba[index + 2], Rgba[index + 3]))
            {
                count++;
            }
        }

        return count;
    }

    public int CountDifferentPixels(PixelSnapshot other, byte tolerance = 8)
    {
        if (Width != other.Width || Height != other.Height)
        {
            throw new InvalidOperationException("Pixel snapshots must have the same dimensions.");
        }

        int count = 0;
        for (int index = 0; index < Rgba.Length; index += 4)
        {
            if (Math.Abs(Rgba[index] - other.Rgba[index]) > tolerance ||
                Math.Abs(Rgba[index + 1] - other.Rgba[index + 1]) > tolerance ||
                Math.Abs(Rgba[index + 2] - other.Rgba[index + 2]) > tolerance ||
                Math.Abs(Rgba[index + 3] - other.Rgba[index + 3]) > tolerance)
            {
                count++;
            }
        }

        return count;
    }
}
