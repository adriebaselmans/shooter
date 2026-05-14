using Shooter;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace Shooter.Render;

/// <summary>Small runtime texture cache for world-brush material maps.
/// Keyed by absolute base-color file path so multiple brushes sharing one material upload once.
/// Companion maps are resolved by filename convention beside the base color map. When no explicit
/// height map exists, an approximate fallback height texture is generated automatically.</summary>
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
            return new MaterialTextureSet(
                white.Handle,
                GetFlatNormal().Handle,
                white.Handle,
                white.Handle,
                white.Handle,
                false,
                false,
                false,
                false,
                1f / white.Width,
                1f / white.Height);
        }

        if (_materials.TryGetValue(filePath, out var set))
            return set;

        var baseColorImage = LoadImage(filePath);
        var baseColor = Upload(baseColorImage);
        string? normalPath = MaterialMapConventions.ResolveNormal(filePath);
        string? roughnessPath = MaterialMapConventions.ResolveRoughness(filePath);
        string? aoPath = MaterialMapConventions.ResolveAo(filePath);
        string? heightPath = MaterialMapConventions.ResolveHeight(filePath);

        var normal = LoadOptional(normalPath, GetFlatNormal());
        var rough = LoadOptional(roughnessPath, GetWhite());
        var ao = LoadOptional(aoPath, GetWhite());
        var height = LoadOrGenerateHeight(baseColorImage, normalPath, heightPath);

        set = new MaterialTextureSet(
            baseColor.Handle,
            normal.Texture.Handle,
            rough.Texture.Handle,
            ao.Texture.Handle,
            height.Texture.Handle,
            normal.FromFile,
            rough.FromFile,
            ao.FromFile,
            true,
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
        _white = UploadSolid([255, 255, 255, 255]);
        return _white;
    }

    private CachedTexture GetFlatNormal()
    {
        if (_flatNormal.Handle != 0) return _flatNormal;
        _flatNormal = UploadSolid([128, 128, 255, 255]);
        return _flatNormal;
    }

    private (CachedTexture Texture, bool FromFile) LoadOptional(string? filePath, CachedTexture fallback)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return (fallback, false);
        return (Upload(LoadImage(filePath)), true);
    }

    private (CachedTexture Texture, bool FromFile) LoadOrGenerateHeight(ImageResult baseColorImage, string? normalPath, string? explicitHeightPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitHeightPath) && File.Exists(explicitHeightPath))
            return (Upload(LoadImage(explicitHeightPath)), true);

        var generated = GenerateHeightPixels(baseColorImage, normalPath);
        return (Upload(baseColorImage.Width, baseColorImage.Height, generated), false);
    }

    private byte[] GenerateHeightPixels(ImageResult baseColorImage, string? normalPath)
    {
        byte[]? normalData = null;
        if (!string.IsNullOrWhiteSpace(normalPath) && File.Exists(normalPath))
            normalData = LoadImage(normalPath).Data;

        var pixels = new byte[baseColorImage.Width * baseColorImage.Height * 4];
        for (int i = 0; i < baseColorImage.Width * baseColorImage.Height; i++)
        {
            int src = i * 4;
            float r = baseColorImage.Data[src + 0] / 255f;
            float g = baseColorImage.Data[src + 1] / 255f;
            float b = baseColorImage.Data[src + 2] / 255f;
            float luminance = r * 0.2126f + g * 0.7152f + b * 0.0722f;
            float height = MathF.Pow(Math.Clamp(luminance, 0f, 1f), 1.55f);
            height = Math.Clamp((height - 0.5f) * 1.25f + 0.5f, 0f, 1f);

            if (normalData is not null)
            {
                float nx = normalData[src + 0] / 255f * 2f - 1f;
                float ny = normalData[src + 1] / 255f * 2f - 1f;
                float nz = normalData[src + 2] / 255f * 2f - 1f;
                float relief = Math.Clamp((MathF.Abs(nx) + MathF.Abs(ny)) * 0.52f + (1f - MathF.Max(0f, nz)) * 0.48f, 0f, 1f);
                height = Math.Clamp(height * 0.60f + relief * 0.40f, 0f, 1f);
            }

            byte value = (byte)Math.Clamp((int)MathF.Round(height * 255f), 0, 255);
            pixels[src + 0] = value;
            pixels[src + 1] = value;
            pixels[src + 2] = value;
            pixels[src + 3] = 255;
        }
        return pixels;
    }

    private ImageResult LoadImage(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
    }

    private CachedTexture Upload(ImageResult image) => Upload(image.Width, image.Height, image.Data);

    private CachedTexture Upload(int width, int height, byte[] rgba)
    {
        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        unsafe
        {
            fixed (byte* p = rgba)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                    (uint)width, (uint)height, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }
        }
        _gl.GenerateMipmap(TextureTarget.Texture2D);
        Configure();
        return new CachedTexture(handle, width, height);
    }

    private CachedTexture UploadSolid(byte[] rgba) => Upload(1, 1, rgba);

    private void Configure()
    {
        const TextureParameterName textureMaxAnisotropyExt = (TextureParameterName)0x84FE;
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, textureMaxAnisotropyExt, 16f);
    }

    public void Dispose()
    {
        var released = new HashSet<uint>();
        foreach (var material in _materials.Values)
        {
            released.Add(material.BaseColorHandle);
            if (material.HasNormalMap) released.Add(material.NormalHandle);
            if (material.HasRoughnessMap) released.Add(material.RoughnessHandle);
            if (material.HasAoMap) released.Add(material.AoHandle);
            released.Add(material.HeightHandle);
        }
        foreach (var handle in released) _gl.DeleteTexture(handle);
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
        uint HeightHandle,
        bool HasNormalMap,
        bool HasRoughnessMap,
        bool HasAoMap,
        bool HasHeightMap,
        float TexelSizeX,
        float TexelSizeY);

    private readonly record struct CachedTexture(uint Handle, int Width, int Height);
}
