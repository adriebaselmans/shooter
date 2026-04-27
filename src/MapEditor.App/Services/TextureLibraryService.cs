using MapEditor.Rendering.Infrastructure;
using System.Numerics;
using System.Text.Json;

namespace MapEditor.App.Services;

/// <summary>Texture entry exposed to the editor UI.</summary>
public sealed record TextureLibraryEntry(
    string Key,
    string DisplayName,
    string Category,
    string ProviderId,
    string ProviderName,
    TextureMaterialKind Kind,
    string FilePath,
    string PreviewPath,
    string AverageColorHex,
    bool IsAnimated,
    float FramesPerSecond,
    int FrameCount,
    string UsageHint)
{
    public string KindLabel => Kind.ToString();
    public string AnimationLabel => IsAnimated ? $"{FrameCount} frames @ {FramesPerSecond:0.#} fps" : "Static";
    public string SourceLabel => ProviderName;
}

public sealed record TextureAssetProviderDescriptor(
    string Id,
    string DisplayName,
    string UsageSummary);

public sealed record ExternalTextureSearchResult(
    string ProviderId,
    string ProviderName,
    string AssetId,
    string DisplayName,
    string Category,
    string PreviewUrl,
    string DownloadUrl,
    string LicenseLabel,
    string SourceUrl,
    string UsageHint)
{
    public string SourceLabel => $"{ProviderName} - {LicenseLabel}";
}

/// <summary>Own built-in material catalog for the editor and renderers.</summary>
public sealed class TextureLibraryService : ITextureCatalog
{
    private const string BuiltInProviderId = "builtin";
    private const string BuiltInProviderName = "Built-in Game Pack";
    private const string AmbientCgProviderId = "ambientcg";
    private const string AmbientCgProviderName = "ambientCG";
    private const string PolyHavenProviderId = "polyhaven";
    private const string PolyHavenProviderName = "Poly Haven";
    private const int TextureSize = 128;
    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly List<TextureLibraryEntry> _entries;
    private readonly Dictionary<string, TextureAssetDescriptor> _textures;
    private readonly string _builtInPackDirectory;
    private readonly string _importedPackDirectory;
    private readonly string _previewCacheDirectory;

    private static readonly TextureAssetProviderDescriptor[] OnlineProviders =
    [
        new(AmbientCgProviderId, AmbientCgProviderName, "CC0 material library with public JSON metadata and preview/color map links."),
        new(PolyHavenProviderId, PolyHavenProviderName, "CC0 texture library with public asset and file APIs.")
    ];

    private static readonly BuiltInTextureSpec[] BuiltInTexturePack =
    [
        new("wood/oak_weathered", "Oak Weathered", "Wood", TextureMaterialKind.Wood, new(0.48f, 0.31f, 0.16f, 1f), new(0.76f, 0.55f, 0.32f, 1f), TexturePattern.Wood, null, "Aged outdoor planks, fences, crates, and weathered timber structures."),
        new("wood/pine_polished", "Pine Polished", "Wood", TextureMaterialKind.Wood, new(0.66f, 0.47f, 0.24f, 1f), new(0.91f, 0.72f, 0.43f, 1f), TexturePattern.Wood, null, "Clean interior beams, trim, floors, and light construction lumber."),
        new("wood/walnut_dark", "Walnut Dark", "Wood", TextureMaterialKind.Wood, new(0.24f, 0.13f, 0.08f, 1f), new(0.54f, 0.31f, 0.18f, 1f), TexturePattern.Wood, null, "Dark doors, premium trim, furniture, and dramatic interior panels."),

        new("grass/field_lush", "Field Lush", "Grass", TextureMaterialKind.Grass, new(0.12f, 0.35f, 0.08f, 1f), new(0.36f, 0.70f, 0.20f, 1f), TexturePattern.Grass, null, "Primary outdoor terrain for readable green play spaces."),
        new("grass/field_dry", "Field Dry", "Grass", TextureMaterialKind.Grass, new(0.36f, 0.31f, 0.12f, 1f), new(0.68f, 0.57f, 0.26f, 1f), TexturePattern.Grass, null, "Dry fields, wasteland edges, and sun-baked outdoor ground."),
        new("grass/moss_green", "Moss Green", "Grass", TextureMaterialKind.Grass, new(0.08f, 0.25f, 0.12f, 1f), new(0.24f, 0.52f, 0.26f, 1f), TexturePattern.Grass, null, "Damp caves, shaded stone, old walls, and forest detail surfaces."),

        new("stone/granite_grey", "Granite Grey", "Stone", TextureMaterialKind.Stone, new(0.32f, 0.33f, 0.34f, 1f), new(0.62f, 0.63f, 0.62f, 1f), TexturePattern.Stone, null, "General rock, cliffs, cave walls, and neutral blockout structure."),
        new("stone/slate_blue", "Slate Blue", "Stone", TextureMaterialKind.Stone, new(0.18f, 0.24f, 0.32f, 1f), new(0.42f, 0.52f, 0.62f, 1f), TexturePattern.Stone, null, "Cool cliffs, dramatic ruins, and moody interior stone."),
        new("stone/cobble_brown", "Cobble Brown", "Stone", TextureMaterialKind.Stone, new(0.27f, 0.20f, 0.14f, 1f), new(0.55f, 0.43f, 0.30f, 1f), TexturePattern.Cobble, null, "Pathways, village streets, dungeon floors, and worn courtyards."),

        new("sky/clear_blue", "Clear Blue", "Sky", TextureMaterialKind.Sky, new(0.18f, 0.48f, 0.85f, 1f), new(0.74f, 0.90f, 1.00f, 1f), TexturePattern.Sky, null, "Surface sky material for sky brushes; not an HDR skybox."),
        new("sky/overcast_grey", "Overcast Grey", "Sky", TextureMaterialKind.Sky, new(0.38f, 0.43f, 0.48f, 1f), new(0.72f, 0.76f, 0.80f, 1f), TexturePattern.Sky, null, "Clouded surface sky material for sky brushes; not an HDR skybox."),
        new("sky/sunset_orange", "Sunset Orange", "Sky", TextureMaterialKind.Sky, new(0.78f, 0.32f, 0.16f, 1f), new(0.98f, 0.70f, 0.36f, 1f), TexturePattern.Sky, null, "Warm surface sky material for sky brushes; not an HDR skybox."),

        new("water/shallow_clear", "Shallow Clear", "Water", TextureMaterialKind.Water, new(0.05f, 0.34f, 0.48f, 1f), new(0.28f, 0.78f, 0.94f, 1f), TexturePattern.Water, new(true, 8f, 16, 0.32f, 0.18f), "Animated clear water for pools, channels, and shallow streams."),
        new("water/deep_ocean", "Deep Ocean", "Water", TextureMaterialKind.Water, new(0.02f, 0.10f, 0.30f, 1f), new(0.08f, 0.36f, 0.72f, 1f), TexturePattern.Water, new(true, 6f, 12, 0.45f, 0.12f), "Animated dark water for large reservoirs and exterior ocean edges."),
        new("water/murky_swamp", "Murky Swamp", "Water", TextureMaterialKind.Water, new(0.12f, 0.22f, 0.14f, 1f), new(0.36f, 0.46f, 0.20f, 1f), TexturePattern.Water, new(true, 4f, 8, 0.18f, 0.08f), "Animated slow water for swamps, sewers, and stagnant pools."),

        new("lava/molten_hot", "Molten Hot", "Lava", TextureMaterialKind.Lava, new(0.42f, 0.04f, 0.01f, 1f), new(1.00f, 0.44f, 0.04f, 1f), TexturePattern.Lava, new(true, 10f, 20, 0.18f, 0.42f), "Animated hazard lava for damage floors and molten pits."),
        new("lava/cooling_crust", "Cooling Crust", "Lava", TextureMaterialKind.Lava, new(0.10f, 0.08f, 0.07f, 1f), new(0.92f, 0.22f, 0.04f, 1f), TexturePattern.Lava, new(true, 5f, 10, 0.08f, 0.25f), "Animated cooling lava with crust for slower volcanic surfaces."),
        new("lava/bright_magma", "Bright Magma", "Lava", TextureMaterialKind.Lava, new(0.62f, 0.08f, 0.00f, 1f), new(1.00f, 0.76f, 0.06f, 1f), TexturePattern.Lava, new(true, 12f, 24, 0.25f, 0.55f), "High-energy animated magma for intense hazard zones."),

        new("metal/steel_brushed", "Steel Brushed", "Metal", TextureMaterialKind.Metal, new(0.34f, 0.36f, 0.38f, 1f), new(0.72f, 0.75f, 0.76f, 1f), TexturePattern.Metal, null, "Industrial panels, machinery, vents, and hard-surface trim."),
        new("metal/rust_corroded", "Rust Corroded", "Metal", TextureMaterialKind.Metal, new(0.32f, 0.12f, 0.05f, 1f), new(0.70f, 0.31f, 0.10f, 1f), TexturePattern.Metal, null, "Weathered metal, abandoned machinery, and decay detail."),
        new("metal/chrome_shiny", "Chrome Shiny", "Metal", TextureMaterialKind.Metal, new(0.52f, 0.56f, 0.58f, 1f), new(0.92f, 0.96f, 0.98f, 1f), TexturePattern.Metal, null, "High-tech accents, clean trim, and reflective-looking panels."),

        new("tile/ceramic_white", "Ceramic White", "Tile", TextureMaterialKind.Tile, new(0.72f, 0.72f, 0.68f, 1f), new(0.96f, 0.95f, 0.88f, 1f), TexturePattern.Tile, null, "Clean interiors, kitchens, labs, bathrooms, and bright floors."),
        new("tile/terracotta_warm", "Terracotta Warm", "Tile", TextureMaterialKind.Tile, new(0.56f, 0.24f, 0.12f, 1f), new(0.82f, 0.42f, 0.20f, 1f), TexturePattern.Tile, null, "Warm floors, courtyards, plazas, and rustic interiors."),
        new("tile/marble_black", "Marble Black", "Tile", TextureMaterialKind.Tile, new(0.04f, 0.04f, 0.05f, 1f), new(0.28f, 0.28f, 0.32f, 1f), TexturePattern.Tile, null, "Premium interiors, dramatic floors, and readable dark trim."),

        new("brick/red_standard", "Red Standard", "Brick", TextureMaterialKind.Brick, new(0.42f, 0.10f, 0.06f, 1f), new(0.76f, 0.24f, 0.14f, 1f), TexturePattern.Brick, null, "Common masonry walls, buildings, and blocky urban structures."),
        new("brick/clay_earth", "Clay Earth", "Brick", TextureMaterialKind.Brick, new(0.32f, 0.20f, 0.12f, 1f), new(0.62f, 0.40f, 0.22f, 1f), TexturePattern.Brick, null, "Warm structural bricks, old ruins, and earthy architecture."),
        new("brick/fireproof_grey", "Fireproof Grey", "Brick", TextureMaterialKind.Brick, new(0.28f, 0.28f, 0.26f, 1f), new(0.58f, 0.58f, 0.54f, 1f), TexturePattern.Brick, null, "Industrial walls, furnace rooms, and neutral masonry."),

        new("effects/forcefield_purple", "Forcefield Purple", "Effects", TextureMaterialKind.Effect, new(0.18f, 0.05f, 0.38f, 1f), new(0.64f, 0.26f, 1.00f, 1f), TexturePattern.Effect, new(true, 8f, 16, 0.10f, 0.30f), "Animated special-purpose surface for barriers and trigger volumes."),
        new("effects/lava_glow_yellow", "Lava Glow Yellow", "Effects", TextureMaterialKind.Effect, new(0.62f, 0.30f, 0.02f, 1f), new(1.00f, 0.92f, 0.16f, 1f), TexturePattern.Effect, new(true, 10f, 20, 0.12f, 0.40f), "Animated glow material for warning trims and hot decals."),
        new("effects/void_black", "Void Black", "Effects", TextureMaterialKind.Effect, new(0.01f, 0.01f, 0.02f, 1f), new(0.08f, 0.08f, 0.12f, 1f), TexturePattern.Effect, null, "Deep void, kill-plane, and non-playable boundary surfaces.")
    ];

    public TextureLibraryService()
        : this(Path.Combine(AppContext.BaseDirectory, "Assets", "Textures"))
    {
    }

    internal TextureLibraryService(string assetDirectory)
    {
        _textures = new Dictionary<string, TextureAssetDescriptor>(StringComparer.OrdinalIgnoreCase);
        _entries = [];
        _builtInPackDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MapEditor",
            "GeneratedTexturePack");
        _importedPackDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MapEditor",
            "ImportedTexturePack");
        _previewCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MapEditor",
            "OnlineTexturePreviewCache");
        ReloadEntries();
    }

    public event EventHandler? LibraryChanged;

    public IReadOnlyList<TextureLibraryEntry> Entries => _entries;
    public IReadOnlyList<TextureAssetProviderDescriptor> TextureAssetProviders => OnlineProviders;

    public bool TryGetTexture(string key, out TextureAssetDescriptor texture) =>
        _textures.TryGetValue(key, out texture!);

    private void ReloadEntries()
    {
        _textures.Clear();
        _entries.Clear();
        Directory.CreateDirectory(_builtInPackDirectory);

        foreach (var spec in BuiltInTexturePack)
        {
            var filePath = EnsureBuiltInTextureFile(spec);
            var averageColor = Mix(spec.PrimaryColor, spec.SecondaryColor, 0.5f);
            var descriptor = new TextureAssetDescriptor(spec.Key, filePath, averageColor, spec.Kind, spec.Animation);
            _textures[spec.Key] = descriptor;
            _entries.Add(CreateEntry(
                spec.Key,
                spec.DisplayName,
                spec.Category,
                BuiltInProviderId,
                BuiltInProviderName,
                spec.Kind,
                filePath,
                filePath,
                averageColor,
                spec.Animation,
                spec.UsageHint));
        }

        LoadImportedEntries();

        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<IReadOnlyList<ExternalTextureSearchResult>> SearchProviderAsync(string providerId, string query, CancellationToken cancellationToken = default)
    {
        providerId = string.IsNullOrWhiteSpace(providerId) ? PolyHavenProviderId : providerId;
        return providerId.ToLowerInvariant() switch
        {
            AmbientCgProviderId => await SearchAmbientCgAsync(query, cancellationToken),
            PolyHavenProviderId => await SearchPolyHavenAsync(query, cancellationToken),
            _ => []
        };
    }

    public async Task<TextureLibraryEntry> ImportExternalTextureAsync(ExternalTextureSearchResult result, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(result.ProviderId) || string.IsNullOrWhiteSpace(result.AssetId))
        {
            throw new InvalidOperationException("Select a valid online material to import.");
        }

        var providerDirectory = Path.Combine(_importedPackDirectory, SanitizePathPart(result.ProviderId), SanitizePathPart(result.AssetId));
        Directory.CreateDirectory(providerDirectory);

        var downloadUrl = result.ProviderId.Equals(PolyHavenProviderId, StringComparison.OrdinalIgnoreCase)
            ? await ResolvePolyHavenDiffuseUrlAsync(result.AssetId, cancellationToken) ?? result.DownloadUrl
            : result.DownloadUrl;
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            downloadUrl = result.PreviewUrl;
        }

        var extension = GetKnownImageExtension(downloadUrl);
        var texturePath = Path.Combine(providerDirectory, $"{SanitizePathPart(result.AssetId)}_color{extension}");
        await DownloadFileAsync(downloadUrl, texturePath, cancellationToken);

        var previewPath = texturePath;
        if (!string.IsNullOrWhiteSpace(result.PreviewUrl) && !string.Equals(result.PreviewUrl, downloadUrl, StringComparison.OrdinalIgnoreCase))
        {
            var previewExtension = GetKnownImageExtension(result.PreviewUrl);
            previewPath = Path.Combine(providerDirectory, $"{SanitizePathPart(result.AssetId)}_preview{previewExtension}");
            await DownloadFileAsync(result.PreviewUrl, previewPath, cancellationToken);
        }

        var kind = GuessKind(result.Category, result.DisplayName, result.UsageHint);
        var averageColor = EstimateAverageColor(kind);
        var importedRecord = new ImportedTextureRecord(
            $"external/{result.ProviderId}/{result.AssetId}",
            result.ProviderId,
            result.ProviderName,
            result.AssetId,
            result.DisplayName,
            string.IsNullOrWhiteSpace(result.Category) ? "Imported" : result.Category,
            kind,
            texturePath,
            previewPath,
            ToHex(averageColor),
            $"Imported from {result.ProviderName}. {result.UsageHint}",
            result.LicenseLabel,
            result.SourceUrl);

        var metadataPath = Path.Combine(providerDirectory, "material.json");
        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(importedRecord, JsonOptions), cancellationToken);
        ReloadEntries();

        return _entries.First(entry => string.Equals(entry.Key, importedRecord.Key, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<ExternalTextureSearchResult>> SearchAmbientCgAsync(string query, CancellationToken cancellationToken)
    {
        var encodedQuery = Uri.EscapeDataString(query ?? string.Empty);
        var querySegment = string.IsNullOrWhiteSpace(encodedQuery) ? string.Empty : $"&q={encodedQuery}";
        var endpoint = $"https://ambientcg.com/api/v2/full_json?type=Material{querySegment}&limit=48&include=previewData";
        using var document = await GetJsonDocumentAsync(endpoint, cancellationToken);
        if (!document.RootElement.TryGetProperty("foundAssets", out var foundAssets) || foundAssets.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<ExternalTextureSearchResult>();
        foreach (var asset in foundAssets.EnumerateArray())
        {
            var assetId = GetString(asset, "assetId");
            if (string.IsNullOrWhiteSpace(assetId))
            {
                continue;
            }

            var displayName = GetString(asset, "displayName") ?? assetId;
            var category = GetString(asset, "displayCategory") ?? "Material";
            var description = GetString(asset, "description");
            var previewUrl = GetAmbientPreviewUrl(asset) ?? string.Empty;
            var colorUrl = GetAmbientColorUrl(asset) ?? previewUrl;
            var sourceUrl = GetString(asset, "shortLink") ?? $"https://ambientcg.com/a/{assetId}";
            results.Add(new ExternalTextureSearchResult(
                AmbientCgProviderId,
                AmbientCgProviderName,
                assetId,
                displayName,
                NormalizeCategory(category),
                previewUrl,
                colorUrl,
                "CC0",
                sourceUrl,
                string.IsNullOrWhiteSpace(description) ? $"ambientCG {category} material." : description));
        }

        return await CacheSearchPreviewsAsync(results, cancellationToken);
    }

    private async Task<IReadOnlyList<ExternalTextureSearchResult>> SearchPolyHavenAsync(string query, CancellationToken cancellationToken)
    {
        using var document = await GetJsonDocumentAsync("https://api.polyhaven.com/assets?t=textures", cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var normalizedQuery = query?.Trim() ?? string.Empty;
        var results = new List<(ExternalTextureSearchResult Result, int Downloads)>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var asset = property.Value;
            var name = GetString(asset, "name") ?? property.Name.Replace('_', ' ');
            var categories = GetArrayStrings(asset, "categories");
            var tags = GetArrayStrings(asset, "tags");
            var description = GetString(asset, "description") ?? string.Empty;
            if (!MatchesQuery(normalizedQuery, property.Name, name, description, categories, tags))
            {
                continue;
            }

            var category = NormalizeCategory(categories.FirstOrDefault() ?? "Material");
            var previewUrl = GetString(asset, "thumbnail_url") ?? string.Empty;
            results.Add((new ExternalTextureSearchResult(
                PolyHavenProviderId,
                PolyHavenProviderName,
                property.Name,
                name,
                category,
                previewUrl,
                string.Empty,
                "CC0",
                $"https://polyhaven.com/a/{property.Name}",
                string.IsNullOrWhiteSpace(description) ? $"Poly Haven {category} texture." : description),
                GetInt(asset, "download_count")));
        }

        var orderedResults = results
            .OrderByDescending(result => result.Downloads)
            .Take(48)
            .Select(result => result.Result)
            .ToArray();
        return await CacheSearchPreviewsAsync(orderedResults, cancellationToken);
    }

    private async Task<IReadOnlyList<ExternalTextureSearchResult>> CacheSearchPreviewsAsync(IReadOnlyList<ExternalTextureSearchResult> results, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_previewCacheDirectory);
        var cachedResults = new List<ExternalTextureSearchResult>(results.Count);
        foreach (var result in results)
        {
            if (cachedResults.Count >= 24 || string.IsNullOrWhiteSpace(result.PreviewUrl))
            {
                cachedResults.Add(result);
                continue;
            }

            try
            {
                var extension = GetKnownImageExtension(result.PreviewUrl);
                var providerDirectory = Path.Combine(_previewCacheDirectory, SanitizePathPart(result.ProviderId));
                Directory.CreateDirectory(providerDirectory);
                var previewPath = Path.Combine(providerDirectory, $"{SanitizePathPart(result.AssetId)}{extension}");
                if (!File.Exists(previewPath))
                {
                    await DownloadFileAsync(result.PreviewUrl, previewPath, cancellationToken);
                }

                cachedResults.Add(result with { PreviewUrl = previewPath });
            }
            catch
            {
                cachedResults.Add(result);
            }
        }

        return cachedResults;
    }

    private async Task<string?> ResolvePolyHavenDiffuseUrlAsync(string assetId, CancellationToken cancellationToken)
    {
        using var document = await GetJsonDocumentAsync($"https://api.polyhaven.com/files/{Uri.EscapeDataString(assetId)}", cancellationToken);
        return TryFindPolyHavenMapUrl(document.RootElement, "diff") ??
               TryFindPolyHavenMapUrl(document.RootElement, "color") ??
               TryFindPolyHavenMapUrl(document.RootElement, "albedo");
    }

    private void LoadImportedEntries()
    {
        if (!Directory.Exists(_importedPackDirectory))
        {
            return;
        }

        foreach (var metadataPath in Directory.EnumerateFiles(_importedPackDirectory, "material.json", SearchOption.AllDirectories))
        {
            try
            {
                var record = JsonSerializer.Deserialize<ImportedTextureRecord>(File.ReadAllText(metadataPath), JsonOptions);
                if (record is null || string.IsNullOrWhiteSpace(record.Key) || !File.Exists(record.FilePath))
                {
                    continue;
                }

                var averageColor = ParseHexColor(record.AverageColorHex) ?? EstimateAverageColor(record.Kind);
                _textures[record.Key] = new TextureAssetDescriptor(record.Key, record.FilePath, averageColor, record.Kind);
                _entries.Add(new TextureLibraryEntry(
                    record.Key,
                    record.DisplayName,
                    record.Category,
                    record.ProviderId,
                    record.ProviderName,
                    record.Kind,
                    record.FilePath,
                    File.Exists(record.PreviewPath) ? record.PreviewPath : record.FilePath,
                    ToHex(averageColor),
                    false,
                    0f,
                    1,
                    record.UsageHint));
            }
            catch
            {
                // Ignore corrupt import metadata and keep the rest of the library usable.
            }
        }
    }

    private string EnsureBuiltInTextureFile(BuiltInTextureSpec spec)
    {
        var filePath = Path.Combine(_builtInPackDirectory, $"{spec.Key}.bmp");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        if (!File.Exists(filePath))
        {
            WriteBmp(filePath, GenerateTexturePixels(spec));
        }

        return filePath;
    }

    private static TextureLibraryEntry CreateEntry(
        string key,
        string displayName,
        string category,
        string providerId,
        string providerName,
        TextureMaterialKind kind,
        string filePath,
        string previewPath,
        Vector4 averageColor,
        TextureAnimationDescriptor? animation,
        string usageHint) =>
        new(
            key,
            displayName,
            category,
            providerId,
            providerName,
            kind,
            filePath,
            previewPath,
            $"#{(byte)(Math.Clamp(averageColor.X, 0f, 1f) * 255):X2}{(byte)(Math.Clamp(averageColor.Y, 0f, 1f) * 255):X2}{(byte)(Math.Clamp(averageColor.Z, 0f, 1f) * 255):X2}",
            animation?.IsAnimated == true,
            animation?.FramesPerSecond ?? 0f,
            animation?.FrameCount ?? 1,
            usageHint);

    private static byte[] GenerateTexturePixels(BuiltInTextureSpec spec)
    {
        byte[] pixels = new byte[TextureSize * TextureSize * 4];
        int seed = spec.Key.Aggregate(17, (current, character) => current * 31 + character);
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)(TextureSize - 1);
                float v = y / (float)(TextureSize - 1);
                var color = spec.Pattern switch
                {
                    TexturePattern.Wood => Wood(spec, u, v, seed),
                    TexturePattern.Grass => Speckle(spec, u, v, seed, 0.55f),
                    TexturePattern.Stone => Speckle(spec, u, v, seed, 0.30f),
                    TexturePattern.Cobble => Cobble(spec, u, v, seed),
                    TexturePattern.Sky => Sky(spec, u, v, seed),
                    TexturePattern.Water => Water(spec, u, v, seed),
                    TexturePattern.Lava => Lava(spec, u, v, seed),
                    TexturePattern.Metal => Metal(spec, u, v, seed),
                    TexturePattern.Tile => Tile(spec, u, v, seed),
                    TexturePattern.Brick => Brick(spec, u, v, seed),
                    TexturePattern.Effect => Effect(spec, u, v, seed),
                    _ => Mix(spec.PrimaryColor, spec.SecondaryColor, Noise(u, v, seed))
                };

                int offset = (y * TextureSize + x) * 4;
                pixels[offset] = ToByte(color.X);
                pixels[offset + 1] = ToByte(color.Y);
                pixels[offset + 2] = ToByte(color.Z);
                pixels[offset + 3] = 255;
            }
        }

        return pixels;
    }

    private static Vector4 Wood(BuiltInTextureSpec spec, float u, float v, int seed)
    {
        var grain = MathF.Sin((u * 18f + Noise(u, v, seed) * 2.8f) * MathF.PI) * 0.5f + 0.5f;
        var streak = MathF.Sin((u * 4f + v * 0.8f) * MathF.PI) * 0.5f + 0.5f;
        return Mix(spec.PrimaryColor, spec.SecondaryColor, grain * 0.65f + streak * 0.2f);
    }

    private static Vector4 Speckle(BuiltInTextureSpec spec, float u, float v, int seed, float density)
    {
        var noise = Noise(u * 4f, v * 4f, seed);
        var blades = MathF.Sin((u * 38f + noise * 5f) * MathF.PI) * 0.5f + 0.5f;
        return Mix(spec.PrimaryColor, spec.SecondaryColor, noise * density + blades * 0.18f);
    }

    private static Vector4 Cobble(BuiltInTextureSpec spec, float u, float v, int seed)
    {
        float cellU = Frac(u * 5f);
        float cellV = Frac(v * 5f);
        float mortar = cellU < 0.08f || cellV < 0.08f ? 0.05f : 1f;
        return Mix(new Vector4(0.11f, 0.10f, 0.09f, 1f), Mix(spec.PrimaryColor, spec.SecondaryColor, Noise(u, v, seed)), mortar);
    }

    private static Vector4 Sky(BuiltInTextureSpec spec, float u, float v, int seed)
    {
        var horizon = Math.Clamp(1f - v * 0.75f, 0f, 1f);
        var cloud = SmoothStep(0.58f, 0.95f, Noise(u * 3f, v * 2f, seed));
        return Mix(Mix(spec.PrimaryColor, spec.SecondaryColor, horizon), new Vector4(0.96f, 0.97f, 0.98f, 1f), cloud * 0.28f);
    }

    private static Vector4 Water(BuiltInTextureSpec spec, float u, float v, int seed)
    {
        var wave = MathF.Sin((u + v * 0.6f) * MathF.PI * 10f) * 0.5f + 0.5f;
        return Mix(spec.PrimaryColor, spec.SecondaryColor, wave * 0.65f + Noise(u, v, seed) * 0.18f);
    }

    private static Vector4 Lava(BuiltInTextureSpec spec, float u, float v, int seed)
    {
        var vein = MathF.Sin((u * 7f - v * 5f + Noise(u, v, seed) * 2f) * MathF.PI) * 0.5f + 0.5f;
        return Mix(new Vector4(0.08f, 0.04f, 0.03f, 1f), Mix(spec.PrimaryColor, spec.SecondaryColor, vein), SmoothStep(0.35f, 1f, vein));
    }

    private static Vector4 Metal(BuiltInTextureSpec spec, float u, float v, int seed)
    {
        var brush = MathF.Sin(v * MathF.PI * 72f) * 0.5f + 0.5f;
        return Mix(spec.PrimaryColor, spec.SecondaryColor, brush * 0.32f + Noise(u, v, seed) * 0.18f);
    }

    private static Vector4 Tile(BuiltInTextureSpec spec, float u, float v, int seed)
    {
        float line = Frac(u * 4f) < 0.035f || Frac(v * 4f) < 0.035f ? 0.12f : 1f;
        return Mix(new Vector4(0.10f, 0.10f, 0.10f, 1f), Mix(spec.PrimaryColor, spec.SecondaryColor, Noise(u, v, seed) * 0.18f), line);
    }

    private static Vector4 Brick(BuiltInTextureSpec spec, float u, float v, int seed)
    {
        float row = MathF.Floor(v * 6f);
        float offsetU = row % 2f < 1f ? u : u + 0.125f;
        float mortar = Frac(offsetU * 4f) < 0.035f || Frac(v * 6f) < 0.045f ? 0.08f : 1f;
        return Mix(new Vector4(0.08f, 0.07f, 0.06f, 1f), Mix(spec.PrimaryColor, spec.SecondaryColor, Noise(u, v, seed) * 0.28f), mortar);
    }

    private static Vector4 Effect(BuiltInTextureSpec spec, float u, float v, int seed)
    {
        var ring = MathF.Sin((u + v) * MathF.PI * 14f) * 0.5f + 0.5f;
        var pulse = SmoothStep(0.30f, 1f, ring);
        return Mix(spec.PrimaryColor, spec.SecondaryColor, pulse * 0.85f + Noise(u, v, seed) * 0.12f);
    }

    private static void WriteBmp(string filePath, byte[] rgbaPixels)
    {
        int rowStride = TextureSize * 3;
        int paddedStride = (rowStride + 3) & ~3;
        int pixelDataSize = paddedStride * TextureSize;
        int fileSize = 54 + pixelDataSize;

        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(TextureSize);
        writer.Write(TextureSize);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(pixelDataSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        byte[] padding = new byte[paddedStride - rowStride];
        for (int y = TextureSize - 1; y >= 0; y--)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                int offset = (y * TextureSize + x) * 4;
                writer.Write(rgbaPixels[offset + 2]);
                writer.Write(rgbaPixels[offset + 1]);
                writer.Write(rgbaPixels[offset]);
            }

            writer.Write(padding);
        }
    }

    private static float Noise(float u, float v, int seed)
    {
        var value = MathF.Sin((u * 127.1f + v * 311.7f + seed * 0.017f) * 12.9898f) * 43758.5453f;
        return Frac(value);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Frac(float value) => value - MathF.Floor(value);

    private static Vector4 Mix(Vector4 a, Vector4 b, float amount) =>
        a + (b - a) * Math.Clamp(amount, 0f, 1f);

    private static byte ToByte(float value) =>
        (byte)(Math.Clamp(value, 0f, 1f) * 255f);

    private static async Task<JsonDocument> GetJsonDocumentAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static async Task DownloadFileAsync(string url, string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("The selected source did not provide a downloadable texture URL.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme == Uri.UriSchemeFile)
        {
            var sourcePath = uri?.LocalPath ?? url;
            File.Copy(sourcePath, filePath, true);
            return;
        }

        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(filePath);
        await input.CopyToAsync(output, cancellationToken);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return 0;
        }

        return property.TryGetInt32(out var value) ? value : 0;
    }

    private static IReadOnlyList<string> GetArrayStrings(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
    }

    private static bool MatchesQuery(string query, string assetId, string name, string description, IReadOnlyList<string> categories, IReadOnlyList<string> tags)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return assetId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               categories.Any(category => category.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
               tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetAmbientPreviewUrl(JsonElement asset)
    {
        if (!asset.TryGetProperty("previewImage", out var previewImage) || previewImage.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(previewImage, "512-PNG") ??
               GetString(previewImage, "256-PNG") ??
               GetString(previewImage, "1024-PNG") ??
               previewImage.EnumerateObject()
                   .Select(property => property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null)
                   .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? GetAmbientColorUrl(JsonElement asset)
    {
        if (!asset.TryGetProperty("previewLinks", out var previewLinks) || previewLinks.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var link in previewLinks.EnumerateArray())
        {
            var url = GetString(link, "url");
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            foreach (var fragmentPart in uri.Fragment.TrimStart('#').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                const string colorPrefix = "color_url=";
                if (fragmentPart.StartsWith(colorPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(fragmentPart[colorPrefix.Length..]);
                }
            }
        }

        return null;
    }

    private static string? TryFindPolyHavenMapUrl(JsonElement element, string nameFragment)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("url", out var urlProperty) && urlProperty.ValueKind == JsonValueKind.String)
            {
                var url = urlProperty.GetString();
                if (!string.IsNullOrWhiteSpace(url) && url.Contains(nameFragment, StringComparison.OrdinalIgnoreCase))
                {
                    return url;
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                var nested = TryFindPolyHavenMapUrl(property.Value, nameFragment);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = TryFindPolyHavenMapUrl(item, nameFragment);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static TextureMaterialKind GuessKind(string category, string displayName, string usageHint)
    {
        var haystack = $"{category} {displayName} {usageHint}";
        if (haystack.Contains("wood", StringComparison.OrdinalIgnoreCase) || haystack.Contains("bark", StringComparison.OrdinalIgnoreCase)) return TextureMaterialKind.Wood;
        if (haystack.Contains("grass", StringComparison.OrdinalIgnoreCase) || haystack.Contains("moss", StringComparison.OrdinalIgnoreCase)) return TextureMaterialKind.Grass;
        if (haystack.Contains("metal", StringComparison.OrdinalIgnoreCase) || haystack.Contains("rust", StringComparison.OrdinalIgnoreCase)) return TextureMaterialKind.Metal;
        if (haystack.Contains("brick", StringComparison.OrdinalIgnoreCase)) return TextureMaterialKind.Brick;
        if (haystack.Contains("tile", StringComparison.OrdinalIgnoreCase) || haystack.Contains("ceramic", StringComparison.OrdinalIgnoreCase)) return TextureMaterialKind.Tile;
        if (haystack.Contains("stone", StringComparison.OrdinalIgnoreCase) || haystack.Contains("rock", StringComparison.OrdinalIgnoreCase) || haystack.Contains("concrete", StringComparison.OrdinalIgnoreCase)) return TextureMaterialKind.Stone;
        return TextureMaterialKind.Standard;
    }

    private static Vector4 EstimateAverageColor(TextureMaterialKind kind) => kind switch
    {
        TextureMaterialKind.Wood => new(0.48f, 0.31f, 0.18f, 1f),
        TextureMaterialKind.Grass => new(0.20f, 0.42f, 0.16f, 1f),
        TextureMaterialKind.Stone => new(0.45f, 0.45f, 0.42f, 1f),
        TextureMaterialKind.Metal => new(0.46f, 0.48f, 0.48f, 1f),
        TextureMaterialKind.Tile => new(0.58f, 0.55f, 0.50f, 1f),
        TextureMaterialKind.Brick => new(0.56f, 0.24f, 0.16f, 1f),
        _ => new(0.50f, 0.50f, 0.50f, 1f)
    };

    private static string NormalizeCategory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Imported";
        }

        return string.Join(' ', value.Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    private static string SanitizePathPart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(character => invalid.Contains(character) || character is '/' or '\\' ? '_' : character).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "asset" : sanitized;
    }

    private static string GetKnownImageExtension(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (extension is ".jpg" or ".jpeg" or ".png" or ".bmp")
            {
                return extension;
            }
        }

        var localExtension = Path.GetExtension(url).ToLowerInvariant();
        if (localExtension is ".jpg" or ".jpeg" or ".png" or ".bmp")
        {
            return localExtension;
        }

        return ".jpg";
    }

    private static string ToHex(Vector4 color) =>
        $"#{(byte)(Math.Clamp(color.X, 0f, 1f) * 255):X2}{(byte)(Math.Clamp(color.Y, 0f, 1f) * 255):X2}{(byte)(Math.Clamp(color.Z, 0f, 1f) * 255):X2}";

    private static Vector4? ParseHexColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#')
        {
            return null;
        }

        return byte.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var r) &&
               byte.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
               byte.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)
            ? new Vector4(r / 255f, g / 255f, b / 255f, 1f)
            : null;
    }

    private sealed record ImportedTextureRecord(
        string Key,
        string ProviderId,
        string ProviderName,
        string AssetId,
        string DisplayName,
        string Category,
        TextureMaterialKind Kind,
        string FilePath,
        string PreviewPath,
        string AverageColorHex,
        string UsageHint,
        string LicenseLabel,
        string SourceUrl);

    private enum TexturePattern
    {
        Wood,
        Grass,
        Stone,
        Cobble,
        Sky,
        Water,
        Lava,
        Metal,
        Tile,
        Brick,
        Effect
    }

    private sealed record BuiltInTextureSpec(
        string Key,
        string DisplayName,
        string Category,
        TextureMaterialKind Kind,
        Vector4 PrimaryColor,
        Vector4 SecondaryColor,
        TexturePattern Pattern,
        TextureAnimationDescriptor? Animation,
        string UsageHint);
}
