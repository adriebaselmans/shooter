using System.Numerics;

namespace MapEditor.Rendering.Infrastructure;

public enum TextureMaterialKind
{
    Standard,
    Wood,
    Grass,
    Stone,
    Sky,
    Water,
    Lava,
    Metal,
    Tile,
    Brick,
    Effect
}

public sealed record TextureAnimationDescriptor(
    bool IsAnimated,
    float FramesPerSecond,
    int FrameCount,
    float FlowSpeed,
    float PulseStrength);

/// <summary>Resolved texture asset metadata used by the editor renderers.</summary>
public sealed record TextureAssetDescriptor(
    string Key,
    string FilePath,
    Vector4 AverageColor,
    TextureMaterialKind Kind = TextureMaterialKind.Standard,
    TextureAnimationDescriptor? Animation = null);

/// <summary>Provides texture file resolution and preview tint information for renderers.</summary>
public interface ITextureCatalog
{
    bool TryGetTexture(string key, out TextureAssetDescriptor texture);
}
