using Shooter;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace Shooter.Render;

/// <summary>Small runtime texture cache for world-brush material maps.
/// Keyed by absolute base-color file path so multiple brushes sharing one material upload once.
/// Companion maps are resolved by filename convention beside the base color map.</summary>
public sealed class TextureCache : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, MaterialTextureSet> _materials = new(StringComparer.OrdinalIgnoreCase);
    private CachedTexture _white;
    private CachedTexture _flatNormal;

    public TextureCache(GL gl)
    {
        _gl = gl;
    }

    public MaterialTextureSet GetMaterialSet(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            var white = GetWhite();
            return new MaterialTextureSet(white.Handle, GetFlatNormal().Handle, white.Handle, white.Handle, false, false, false, 1f / white.Width, 1f / white.Height);
        }
        if (_materials.TryGetValue(filePath, out var set))
            return set;

        var baseColor = Load(filePath);
        var normal = LoadOptional(MaterialMapConventions.ResolveNormal(filePath), GetFlatNormal());
        var rough = LoadOptional(MaterialMapConventions.ResolveRoughness(filePath), GetWhite());
        var ao = LoadOptional(MaterialMapConventions.ResolveAo(filePath), GetWhite());
        set = new MaterialTextureSet(
            baseColor.Handle,
            normal.Texture.Handle,
            rough.Texture.Handle,
            ao.Texture.Handle,
            normal.FromFile,
            rough.FromFile,
            ao.FromFile,
            1f / Math.Max(1, baseColor.Width),
            1f / Math.Max(1, baseColor.Height));
        _materials[filePath] = set;
        return set;
    }

    public uint GetOrWhite(string? filePath) => GetMaterialSet(filePath).BaseColorHandle;

    public bool HasTexture(string? filePath) =>
        !string.IsNullOrWhiteSpace(filePath);

    public (float X, float Y) GetTexelSize(string? filePath)
    {
        var info = GetMaterialSet(filePath);
        return (info.TexelSizeX, info.TexelSizeY);
    }

    private CachedTexture GetWhite()
    {
        if (_white.Handle != 0) return _white;
        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        byte[] pixel = [255, 255, 255, 255];
        unsafe
        {
            fixed (byte* p = pixel)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                    1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }
        }
        Configure();
        _white = new CachedTexture(handle, 1, 1);
        return _white;
    }

    private CachedTexture GetFlatNormal()
    {
        if (_flatNormal.Handle != 0) return _flatNormal;
        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        byte[] pixel = [128, 128, 255, 255];
        unsafe
        {
            fixed (byte* p = pixel)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                    1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }
        }
        Configure();
        _flatNormal = new CachedTexture(handle, 1, 1);
        return _flatNormal;
    }

    private (CachedTexture Texture, bool FromFile) LoadOptional(string? filePath, CachedTexture fallback)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return (fallback, false);
        return (Load(filePath), true);
    }

    private CachedTexture Load(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        unsafe
        {
            fixed (byte* p = image.Data)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                    (uint)image.Width, (uint)image.Height, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }
        }
        _gl.GenerateMipmap(TextureTarget.Texture2D);
        Configure();
        return new CachedTexture(handle, image.Width, image.Height);
    }

    private void Configure()
    {
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
    }

    public void Dispose()
    {
        var released = new HashSet<uint>();
        foreach (var m in _materials.Values)
        {
            released.Add(m.BaseColorHandle);
            if (m.HasNormalMap) released.Add(m.NormalHandle);
            if (m.HasRoughnessMap) released.Add(m.RoughnessHandle);
            if (m.HasAoMap) released.Add(m.AoHandle);
        }
        foreach (var h in released) _gl.DeleteTexture(h);
        _materials.Clear();
        if (_white.Handle != 0)
        {
            _gl.DeleteTexture(_white.Handle);
            _white = default;
        }
        if (_flatNormal.Handle != 0)
        {
            _gl.DeleteTexture(_flatNormal.Handle);
            _flatNormal = default;
        }
    }

    public readonly record struct MaterialTextureSet(
        uint BaseColorHandle,
        uint NormalHandle,
        uint RoughnessHandle,
        uint AoHandle,
        bool HasNormalMap,
        bool HasRoughnessMap,
        bool HasAoMap,
        float TexelSizeX,
        float TexelSizeY);

    private readonly record struct CachedTexture(uint Handle, int Width, int Height);
}
