using System.Numerics;
using System.Text.Json;
using System.Runtime.InteropServices;
using Shooter;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace Shooter.Render;

/// <summary>Fast textured text renderer for HUD/menu text using a baked atlas.</summary>
public sealed class FontAtlasTextRenderer : IDisposable
{
    private const int FirstChar = 32;
    private const int LastChar = 126;
    private const int Columns = 16;
    private const int CellSizePx = 64;
    private const float AdvanceScale = 0.62f;
    private const float LineHeightScale = 1.18f;

    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly DynamicTextBuffer _buffer;
    private readonly uint _atlasTex;
    private readonly int _atlasWidth;
    private readonly int _atlasHeight;
    private int _viewportWidth;
    private int _viewportHeight;
    private readonly Dictionary<char, Glyph> _glyphs = new();
    private readonly List<float> _verts = new(16384);

    public FontAtlasTextRenderer(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Shaders.HudTextVert, Shaders.HudTextFrag);
        _buffer = new DynamicTextBuffer(gl);
        string atlasPath = AssetLocator.Combine("fonts", "menu_font.png");
        string metricsPath = AssetLocator.Combine("fonts", "menu_font.json");
        using var stream = File.OpenRead(atlasPath);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        _atlasWidth = image.Width;
        _atlasHeight = image.Height;
        _atlasTex = UploadAtlas(image.Data, image.Width, image.Height);
        BuildGlyphs(metricsPath);
    }

    public void BeginFrame(int viewportWidth, int viewportHeight)
    {
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
        _verts.Clear();
    }

    public void DrawText(string text, float clipX, float clipY, float pxSize, Vector4 color, bool shadow = true)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (shadow)
        {
            Vector4 outline = new(0f, 0f, 0f, color.W * 0.92f);
            AppendText(text, clipX + 0.0025f, clipY, pxSize, outline);
            AppendText(text, clipX - 0.0025f, clipY, pxSize, outline);
            AppendText(text, clipX, clipY + 0.0025f, pxSize, outline);
            AppendText(text, clipX, clipY - 0.0025f, pxSize, outline);
        }
        AppendText(text, clipX, clipY, pxSize, color);
    }

    public void Flush()
    {
        if (_verts.Count == 0) return;
        _shader.Use();
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _atlasTex);
        _gl.Uniform1(_shader.U("uAtlas"), 0);
        _buffer.Upload(CollectionsMarshal.AsSpan(_verts));
        _buffer.Bind();
        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(_verts.Count / _buffer.FloatsPerVertex));
        _gl.BindVertexArray(0);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    private void AppendText(string text, float clipX, float clipY, float pxSize, Vector4 color)
    {
        float xPx = (clipX + 1f) * 0.5f * _viewportWidth;
        float yPx = (1f - clipY) * 0.5f * _viewportHeight;
        float cursor = xPx;
        string upper = text.ToUpperInvariant();
        foreach (char ch in upper)
        {
            if (ch == '\n')
            {
                cursor = xPx;
                yPx += pxSize * LineHeightScale;
                continue;
            }
            if (!_glyphs.TryGetValue(ch, out var g))
                g = _glyphs['?'];
            AppendGlyph(cursor, yPx, pxSize, g, color);
            cursor += pxSize * AdvanceScale;
        }
    }

    private void AppendGlyph(float xPx, float yPx, float pxSize, Glyph g, Vector4 color)
    {
        float pxX = 2f / _viewportWidth;
        float pxY = 2f / _viewportHeight;

        float w = pxSize * AdvanceScale;
        float h = pxSize;
        float left = -1f + xPx * pxX;
        float top = 1f - yPx * pxY;
        float right = left + w * pxX;
        float bottom = top - h * pxY;

        float u0 = g.X / (float)_atlasWidth;
        float v0 = g.Y / (float)_atlasHeight;
        float u1 = (g.X + g.W) / (float)_atlasWidth;
        float v1 = (g.Y + g.H) / (float)_atlasHeight;

        AddQuad(left, top, right, bottom, u0, v0, u1, v1, color);
    }

    private void AddQuad(float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1, Vector4 color)
    {
        AddVertex(x0, y0, u0, v0, color);
        AddVertex(x1, y0, u1, v0, color);
        AddVertex(x1, y1, u1, v1, color);
        AddVertex(x0, y0, u0, v0, color);
        AddVertex(x1, y1, u1, v1, color);
        AddVertex(x0, y1, u0, v1, color);
    }

    private void AddVertex(float x, float y, float u, float v, Vector4 color)
    {
        _verts.Add(x);
        _verts.Add(y);
        _verts.Add(u);
        _verts.Add(v);
        _verts.Add(color.X);
        _verts.Add(color.Y);
        _verts.Add(color.Z);
        _verts.Add(color.W);
    }

    private void BuildGlyphs(string metricsPath)
    {
        if (File.Exists(metricsPath))
        {
            try
            {
                using var stream = File.OpenRead(metricsPath);
                var doc = JsonSerializer.Deserialize<FontMetrics>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (doc is not null)
                {
                    foreach (var kv in doc.Chars)
                        _glyphs[kv.Key[0]] = new Glyph(kv.Value.X, kv.Value.Y, kv.Value.W, kv.Value.H);
                    if (_glyphs.TryGetValue(' ', out var space) == false)
                        _glyphs[' '] = new Glyph(0, 0, CellSizePx, CellSizePx);
                    if (_glyphs.TryGetValue('?', out var q) == false)
                        _glyphs['?'] = _glyphs[' '];
                    return;
                }
            }
            catch
            {
                // fall through to generated grid mapping
            }
        }

        for (int i = FirstChar; i <= LastChar; i++)
        {
            int idx = i - FirstChar;
            int x = (idx % Columns) * CellSizePx;
            int y = (idx / Columns) * CellSizePx;
            _glyphs[(char)i] = new Glyph(x, y, CellSizePx, CellSizePx);
        }
        if (!_glyphs.ContainsKey('?')) _glyphs['?'] = new Glyph(0, 0, CellSizePx, CellSizePx);
    }

    private uint UploadAtlas(byte[] rgba, int width, int height)
    {
        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        unsafe
        {
            fixed (byte* p = rgba)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        return handle;
    }

    public void Dispose()
    {
        _gl.DeleteTexture(_atlasTex);
        _shader.Dispose();
        _buffer.Dispose();
    }

    private readonly record struct Glyph(int X, int Y, int W, int H);

    private sealed class FontMetrics
    {
        public Dictionary<string, GlyphMetric> Chars { get; set; } = new();
    }

    private sealed class GlyphMetric
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
    }
}

internal sealed class DynamicTextBuffer : IDisposable
{
    private readonly GL _gl;
    public uint Vao { get; }
    public uint Vbo { get; }
    private int _capacityFloats;
    public int FloatsPerVertex => 8;

    public unsafe DynamicTextBuffer(GL gl)
    {
        _gl = gl;
        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        gl.BindVertexArray(Vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        uint stride = (uint)(FloatsPerVertex * sizeof(float));
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)(4 * sizeof(float)));
        gl.BindVertexArray(0);
    }

    public unsafe void Upload(ReadOnlySpan<float> data)
    {
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        if (data.Length > _capacityFloats)
        {
            _capacityFloats = Math.Max(data.Length, _capacityFloats * 2);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_capacityFloats * sizeof(float)), null, BufferUsageARB.DynamicDraw);
        }
        fixed (float* p = data)
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(data.Length * sizeof(float)), p);
    }

    public void Bind() => _gl.BindVertexArray(Vao);

    public void Dispose()
    {
        _gl.DeleteBuffer(Vbo);
        _gl.DeleteVertexArray(Vao);
    }
}
