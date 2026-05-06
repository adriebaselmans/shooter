using Shooter.Game;
using Silk.NET.Windowing;

namespace Shooter.RenderSystem;

public interface IRenderBackend : IDisposable
{
    string Name { get; }

    void Initialize(IWindow window, GameWorld world, LightingEnvironment lighting);
    void Resize(int width, int height);
    void Render(double dt, RenderFrameData frame);
}
