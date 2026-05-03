namespace Shooter.RenderSystem;

public static class RenderBackendFactory
{
    public static IRenderBackend Create(BackendKind kind) => kind switch
    {
        BackendKind.OpenGL => new OpenGLRenderBackend(),
        BackendKind.Metal => new MetalBootstrapBackend(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static BackendKind Parse(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "gl" or "opengl" => BackendKind.OpenGL,
            "metal" => BackendKind.Metal,
            null or "" => BackendKind.OpenGL,
            _ => throw new ArgumentException($"Unknown backend '{value}'. Use gl or metal.")
        };
}
