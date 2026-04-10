using MapEditor.Rendering.Infrastructure;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace MapEditor.App.Services;

/// <summary>Texture entry exposed to the editor UI.</summary>
public sealed record TextureLibraryEntry(
    string Key,
    string DisplayName,
    string FilePath,
    string PreviewPath,
    string AverageColorHex);

/// <summary>Scans project-local texture assets and exposes them to the UI and renderers.</summary>
public sealed class TextureLibraryService : ITextureCatalog
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".bmp"];
    private readonly ReadOnlyCollection<TextureLibraryEntry> _entries;
    private readonly Dictionary<string, TextureAssetDescriptor> _textures;

    public TextureLibraryService()
        : this(Path.Combine(AppContext.BaseDirectory, "Assets", "Textures"))
    {
    }

    internal TextureLibraryService(string assetDirectory)
    {
        _textures = new Dictionary<string, TextureAssetDescriptor>(StringComparer.OrdinalIgnoreCase);
        _entries = new ReadOnlyCollection<TextureLibraryEntry>(LoadEntries(assetDirectory));
    }

    public IReadOnlyList<TextureLibraryEntry> Entries => _entries;

    public bool TryGetTexture(string key, out TextureAssetDescriptor texture) =>
        _textures.TryGetValue(key, out texture!);

    private List<TextureLibraryEntry> LoadEntries(string assetDirectory)
    {
        var entries = new List<TextureLibraryEntry>();
        if (!Directory.Exists(assetDirectory))
        {
            return entries;
        }

        foreach (var filePath in Directory.EnumerateFiles(assetDirectory, "*.*", SearchOption.AllDirectories)
                     .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(assetDirectory, filePath);
            string key = relativePath.Replace('\\', '/');
            string displayName = Path.GetFileNameWithoutExtension(filePath);
            var averageColor = ComputeAverageColor(filePath);
            var descriptor = new TextureAssetDescriptor(key, filePath, averageColor);
            _textures[key] = descriptor;
            entries.Add(new TextureLibraryEntry(
                key,
                displayName,
                filePath,
                filePath,
                $"#{(byte)(averageColor.X * 255):X2}{(byte)(averageColor.Y * 255):X2}{(byte)(averageColor.Z * 255):X2}"));
        }

        return entries;
    }

    private static System.Numerics.Vector4 ComputeAverageColor(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var formatted = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);

        int stride = formatted.PixelWidth * 4;
        byte[] pixels = new byte[stride * formatted.PixelHeight];
        formatted.CopyPixels(pixels, stride, 0);

        double r = 0;
        double g = 0;
        double b = 0;
        int sampleCount = 0;
        int step = Math.Max(1, pixels.Length / 4096);
        step -= step % 4;
        if (step <= 0)
        {
            step = 4;
        }

        for (int i = 0; i <= pixels.Length - 4; i += step)
        {
            b += pixels[i];
            g += pixels[i + 1];
            r += pixels[i + 2];
            sampleCount++;
        }

        if (sampleCount == 0)
        {
            return new System.Numerics.Vector4(1f, 1f, 1f, 1f);
        }

        return new System.Numerics.Vector4(
            (float)(r / sampleCount / 255d),
            (float)(g / sampleCount / 255d),
            (float)(b / sampleCount / 255d),
            1f);
    }
}
