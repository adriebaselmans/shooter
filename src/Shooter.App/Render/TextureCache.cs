using Silk.NET.OpenGL;
using StbImageSharp;

namespace Shooter.Render;

/// <summary>Small runtime texture cache for world-brush base-color maps.
/// Keyed by absolute file path so multiple brushes sharing one image upload once.</summary>
public sealed class TextureCache : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, uint> _handles = new(StringComparer.OrdinalIgnoreCase);
    private uint _white;

    public TextureCache(GL gl)
    {
        _gl = gl;
    }

    public uint GetOrWhite(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return GetWhite();
        if (_handles.TryGetValue(filePath, out var handle))
            return handle;
        if (!File.Exists(filePath))
            return GetWhite();
        handle = Load(filePath);
        _handles[filePath] = handle;
        return handle;
    }

    public bool HasTexture(string? filePath) =>
        !string.IsNullOrWhiteSpace(filePath);

    private uint GetWhite()
    {
        if (_white != 0) return _white;
        _white = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _white);
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
        return _white;
    }

    private uint Load(string filePath)
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
        return handle;
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
        foreach (var h in _handles.Values) _gl.DeleteTexture(h);
        _handles.Clear();
        if (_white != 0)
        {
            _gl.DeleteTexture(_white);
            _white = 0;
        }
    }
}
