using System.Numerics;
using MapEditor.Core.Geometry;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace Shooter.Render;

/// <summary>RGBA8 GL_TEXTURE_2D wrapping a decoded PNG/JPEG.</summary>
public sealed class GlTexture : IDisposable
{
    private readonly GL _gl;
    public uint Handle { get; }

    public GlTexture(GL gl, uint handle) { _gl = gl; Handle = handle; }

    public static unsafe GlTexture? FromImageBytes(GL gl, byte[] imageBytes)
    {
        if (imageBytes.Length == 0) return null;
        ImageResult img;
        try { img = ImageResult.FromMemory(imageBytes, ColorComponents.RedGreenBlueAlpha); }
        catch (Exception ex) { Console.WriteLine($"[Tex] decode failed: {ex.Message}"); return null; }

        var handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, handle);
        fixed (byte* p = img.Data)
        {
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)img.Width, (uint)img.Height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        gl.GenerateMipmap(TextureTarget.Texture2D);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        return new GlTexture(gl, handle);
    }

    public void Bind(int unit = 0)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + unit);
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose() => _gl.DeleteTexture(Handle);
}

/// <summary>One textured primitive on the GPU.</summary>
public sealed class GpuPrimitive : IDisposable
{
    public required GlMesh Mesh { get; init; }
    public GlTexture? BaseColor { get; init; }
    public required Vector4 BaseColorFactor { get; init; }

    public void Dispose()
    {
        Mesh.Dispose();
        BaseColor?.Dispose();
    }
}

/// <summary>A whole model on the GPU, made of one or more textured primitives.</summary>
public sealed class GpuModel : IDisposable
{
    public required IReadOnlyList<GpuPrimitive> Primitives { get; init; }

    public static GpuModel Upload(GL gl, ModelData data)
    {
        var prims = new List<GpuPrimitive>(data.Primitives.Count);
        foreach (var p in data.Primitives)
        {
            var mesh = new Mesh(p.Vertices, p.Indices);
            var glMesh = new GlMesh(gl, mesh);
            GlTexture? tex = p.BaseColorImage is { Length: > 0 }
                ? GlTexture.FromImageBytes(gl, p.BaseColorImage)
                : null;
            prims.Add(new GpuPrimitive
            {
                Mesh = glMesh,
                BaseColor = tex,
                BaseColorFactor = p.BaseColorFactor,
            });
        }
        return new GpuModel { Primitives = prims };
    }

    public void Dispose()
    {
        foreach (var p in Primitives) p.Dispose();
    }
}
