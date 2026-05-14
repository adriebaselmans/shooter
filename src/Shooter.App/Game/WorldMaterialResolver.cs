using System.Numerics;

namespace Shooter.Game;

/// <summary>Resolves texture paths and fallback visual/material heuristics for world brushes.</summary>
internal static class WorldMaterialResolver
{
    public static string? ResolveTexturePath(string materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName)) return null;
        string ext = Path.GetExtension(materialName);
        if (!IsImageExtension(ext)) return null;

        if (Path.IsPathRooted(materialName) && File.Exists(materialName))
            return materialName;

        string assetRelative = Path.Combine(global::Shooter.AssetLocator.Root, materialName);
        if (File.Exists(assetRelative)) return assetRelative;

        string cwdRelative = Path.GetFullPath(materialName, Directory.GetCurrentDirectory());
        if (File.Exists(cwdRelative)) return cwdRelative;

        return null;
    }

    public static Vector3 ResolveTintColor(string materialName, string? texturePath)
    {
        if (!string.IsNullOrWhiteSpace(texturePath))
            return Vector3.One;
        if (string.IsNullOrEmpty(materialName) || materialName == "default")
            return new Vector3(0.78f, 0.78f, 0.82f);
        unchecked
        {
            int h = 17;
            foreach (var c in materialName) h = h * 31 + c;
            float r = 0.55f + ((h & 0xFF) / 255f) * 0.4f;
            float g = 0.55f + (((h >> 8) & 0xFF) / 255f) * 0.4f;
            float b = 0.55f + (((h >> 16) & 0xFF) / 255f) * 0.4f;
            return new Vector3(r, g, b);
        }
    }

    public static SurfaceVisuals InferSurfaceVisuals(string materialName, string? texturePath)
    {
        string key = $"{materialName} {texturePath}".ToLowerInvariant();
        var result = new SurfaceVisuals(0.82f, 0.05f, 0.0f);

        if (!string.IsNullOrWhiteSpace(texturePath))
            result = result with { DetailNormalStrength = 0.20f };

        if (key.Contains("water"))
            return new SurfaceVisuals(0.05f, 0.66f, 0.90f);
        if (key.Contains("sand"))
            return new SurfaceVisuals(0.98f, 0.02f, 0.10f);
        if (key.Contains("plaster"))
            return new SurfaceVisuals(0.88f, 0.05f, 0.18f);
        if (key.Contains("stone") || key.Contains("brick"))
            return new SurfaceVisuals(0.82f, 0.06f, 0.26f);
        if (key.Contains("concrete"))
            return new SurfaceVisuals(0.80f, 0.08f, 0.34f);
        if (key.Contains("wood") || key.Contains("plank") || key.Contains("trim"))
            return new SurfaceVisuals(0.68f, 0.12f, 0.20f);
        if (key.Contains("awning"))
            return new SurfaceVisuals(0.62f, 0.10f, 0.08f);
        return result;
    }

    private static bool IsImageExtension(string ext) =>
        ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
}
