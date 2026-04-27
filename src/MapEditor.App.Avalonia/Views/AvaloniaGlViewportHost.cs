using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;

namespace MapEditor.App.Avalonia.Views;

public sealed class AvaloniaGlViewportHost : OpenGlControlBase
{
    private GL? _gl;
    private int _pixelWidth;
    private int _pixelHeight;

    public event EventHandler<GL>? RenderFrame;
    public event EventHandler? OpenGlDeinitializing;
    public int PixelWidth => _pixelWidth;
    public int PixelHeight => _pixelHeight;

    public AvaloniaGlViewportHost()
    {
        Focusable = true;
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        _gl = GL.GetApi(gl.GetProcAddress);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl is null)
        {
            return;
        }

        var scale = (VisualRoot as global::Avalonia.Controls.Window)?.RenderScaling ?? 1d;
        _pixelWidth = Math.Max(1, (int)(Bounds.Width * scale));
        _pixelHeight = Math.Max(1, (int)(Bounds.Height * scale));
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)fb);
        RenderFrame?.Invoke(this, _gl);
        RequestNextFrameRendering();
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        OpenGlDeinitializing?.Invoke(this, EventArgs.Empty);
        _gl = null;
    }

}
