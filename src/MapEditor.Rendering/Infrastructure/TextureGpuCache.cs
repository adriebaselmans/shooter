using Silk.NET.OpenGL;
using StbImageSharp;

namespace MapEditor.Rendering.Infrastructure;

internal sealed class TextureGpuCache : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, uint> _textureHandles = new(StringComparer.Ordinal);
    private uint _whiteTextureHandle;

    public TextureGpuCache(GL gl)
    {
        _gl = gl;
    }

    public uint GetTextureHandle(TextureAssetDescriptor? texture)
    {
        if (texture is null || string.IsNullOrWhiteSpace(texture.FilePath) || !File.Exists(texture.FilePath))
        {
            if (texture is not null && texture.Kind is TextureMaterialKind.Water or TextureMaterialKind.Lava)
            {
                return GetProceduralTextureHandle(texture);
            }

            return GetWhiteTextureHandle();
        }

        if (_textureHandles.TryGetValue(texture.Key, out var handle))
        {
            return handle;
        }

        handle = CreateTexture(texture.FilePath);
        _textureHandles[texture.Key] = handle;
        return handle;
    }

    private uint GetProceduralTextureHandle(TextureAssetDescriptor texture)
    {
        if (_textureHandles.TryGetValue(texture.Key, out var handle))
        {
            return handle;
        }

        handle = CreateProceduralTexture(texture);
        _textureHandles[texture.Key] = handle;
        return handle;
    }

    private uint GetWhiteTextureHandle()
    {
        if (_whiteTextureHandle != 0)
        {
            return _whiteTextureHandle;
        }

        _whiteTextureHandle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _whiteTextureHandle);

        byte[] pixel = [255, 255, 255, 255];
        unsafe
        {
            fixed (byte* pixelPtr = pixel)
            {
                _gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    (int)InternalFormat.Rgba8,
                    1,
                    1,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    pixelPtr);
            }
        }

        ConfigureTextureParameters();
        return _whiteTextureHandle;
    }

    private uint CreateTexture(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);

        unsafe
        {
            fixed (byte* pixelPtr = image.Data)
            {
                _gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    (int)InternalFormat.Rgba8,
                    (uint)image.Width,
                    (uint)image.Height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    pixelPtr);
            }
        }

        _gl.GenerateMipmap(TextureTarget.Texture2D);
        ConfigureTextureParameters();
        return handle;
    }

    private uint CreateProceduralTexture(TextureAssetDescriptor texture)
    {
        const int size = 64;
        byte[] pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float v = y / (float)(size - 1);
                float wave = MathF.Sin((u + v) * MathF.PI * 6f) * 0.5f + 0.5f;
                float vein = MathF.Sin((u * 11f - v * 7f) * MathF.PI) * 0.5f + 0.5f;
                var color = texture.Kind == TextureMaterialKind.Lava
                    ? Mix(new(0.34f, 0.04f, 0.02f), new(1.0f, 0.48f, 0.05f), MathF.Max(wave, vein * 0.75f))
                    : Mix(new(0.03f, 0.16f, 0.28f), new(0.20f, 0.68f, 0.92f), wave * 0.7f + vein * 0.3f);

                int offset = (y * size + x) * 4;
                pixels[offset] = ToByte(color.X);
                pixels[offset + 1] = ToByte(color.Y);
                pixels[offset + 2] = ToByte(color.Z);
                pixels[offset + 3] = 255;
            }
        }

        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        unsafe
        {
            fixed (byte* pixelPtr = pixels)
            {
                _gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    (int)InternalFormat.Rgba8,
                    size,
                    size,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    pixelPtr);
            }
        }

        _gl.GenerateMipmap(TextureTarget.Texture2D);
        ConfigureTextureParameters();
        return handle;
    }

    private static System.Numerics.Vector3 Mix(System.Numerics.Vector3 a, System.Numerics.Vector3 b, float amount) =>
        a + (b - a) * Math.Clamp(amount, 0f, 1f);

    private static byte ToByte(float value) =>
        (byte)(Math.Clamp(value, 0f, 1f) * 255f);

    private void ConfigureTextureParameters()
    {
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
    }

    public void Dispose()
    {
        foreach (var handle in _textureHandles.Values)
        {
            _gl.DeleteTexture(handle);
        }

        if (_whiteTextureHandle != 0)
        {
            _gl.DeleteTexture(_whiteTextureHandle);
        }
    }
}
