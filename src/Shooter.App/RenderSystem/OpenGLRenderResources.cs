using Shooter.Game;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Shooter.RenderSystem;

/// <summary>Composes the OpenGL scene, lighting, and post-processing resource groups.</summary>
internal sealed class OpenGLRenderResources : IDisposable
{
    public IWindow Window { get; }
    public GL Gl { get; }
    public OpenGLSceneResources Scene { get; }
    public OpenGLLightingResources Lighting { get; }
    public OpenGLPostProcessResources Post { get; }

    private OpenGLRenderResources(IWindow window, GL gl, OpenGLSceneResources scene, OpenGLLightingResources lighting, OpenGLPostProcessResources post)
    {
        Window = window;
        Gl = gl;
        Scene = scene;
        Lighting = lighting;
        Post = post;
    }

    public static OpenGLRenderResources Create(IWindow window, GameWorld world, LightingEnvironment lighting)
    {
        var gl = GL.GetApi(window);
        var resources = new OpenGLRenderResources(
            window,
            gl,
            OpenGLSceneResources.Create(gl, world),
            OpenGLLightingResources.Create(gl, lighting),
            OpenGLPostProcessResources.Create(gl, window.FramebufferSize.X, window.FramebufferSize.Y));

        var sky = world.SkyColor;
        gl.ClearColor(sky.X, sky.Y, sky.Z, 1f);
        gl.Enable(EnableCap.DepthTest);
        gl.Viewport(window.FramebufferSize);
        return resources;
    }

    public void Resize(int width, int height)
    {
        Gl.Viewport(new Vector2D<int>(width, height));
        Post.Resize(width, height);
    }

    public void EnsureFramebufferSized()
    {
        var fb = Window.FramebufferSize;
        Resize(fb.X, fb.Y);
    }

    public void Dispose()
    {
        Post.Dispose();
        Lighting.Dispose();
        Scene.Dispose();
    }
}
