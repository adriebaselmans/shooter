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
