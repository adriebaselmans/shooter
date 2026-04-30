namespace Shooter;

/// <summary>Locates the repo's assets/ folder by walking upward from the binary location.</summary>
public static class AssetLocator
{
    private static string? _root;
    public static string Root => _root ??= Resolve();

    private static string Resolve()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = System.IO.Path.Combine(dir, "assets");
            if (Directory.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        // Fallback: cwd/assets
        return System.IO.Path.Combine(Directory.GetCurrentDirectory(), "assets");
    }

    public static string Combine(params string[] parts) =>
        System.IO.Path.Combine(new[] { Root }.Concat(parts).ToArray());
}
