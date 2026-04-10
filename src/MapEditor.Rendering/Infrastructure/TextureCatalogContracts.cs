using System.Numerics;

namespace MapEditor.Rendering.Infrastructure;

/// <summary>Resolved texture asset metadata used by the editor renderers.</summary>
public sealed record TextureAssetDescriptor(string Key, string FilePath, Vector4 AverageColor);

/// <summary>Provides texture file resolution and preview tint information for renderers.</summary>
public interface ITextureCatalog
{
    bool TryGetTexture(string key, out TextureAssetDescriptor texture);
}
