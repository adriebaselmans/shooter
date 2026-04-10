using CommunityToolkit.Mvvm.ComponentModel;
using MapEditor.Core.Entities;
using System.Globalization;
using System.Numerics;

namespace MapEditor.App.ViewModels;

public sealed record SurfaceSelectionChip(string SurfaceId, string DisplayName, bool IsSelected);

/// <summary>Drives the properties panel. Populates when an entity is selected.</summary>
public sealed partial class PropertiesViewModel : ObservableObject
{
    [ObservableProperty] private string  _entityName    = string.Empty;
    [ObservableProperty] private string  _entityKind    = string.Empty;
    [ObservableProperty] private bool    _hasEntity;
    [ObservableProperty] private bool    _canEditTransform;
    [ObservableProperty] private string  _positionText  = string.Empty;
    [ObservableProperty] private string  _rotationText  = string.Empty;
    [ObservableProperty] private string  _scaleText     = string.Empty;
    [ObservableProperty] private bool    _canEditTexturing;
    [ObservableProperty] private bool    _canEditSurfaceMapping;
    [ObservableProperty] private string  _brushTextureKey = string.Empty;
    [ObservableProperty] private string  _selectedSurfaceSummary = "No surfaces selected";
    [ObservableProperty] private string  _surfaceOffsetUText = string.Empty;
    [ObservableProperty] private string  _surfaceOffsetVText = string.Empty;
    [ObservableProperty] private string  _surfaceScaleUText = string.Empty;
    [ObservableProperty] private string  _surfaceScaleVText = string.Empty;
    [ObservableProperty] private string  _surfaceRotationText = string.Empty;
    [ObservableProperty] private bool    _textureLockEnabled = true;
    [ObservableProperty] private IReadOnlyList<SurfaceSelectionChip> _surfaceChips = Array.Empty<SurfaceSelectionChip>();

    public void PopulateFromBrush(Brush brush)
    {
        HasEntity  = true;
        CanEditTransform = true;
        CanEditTexturing = true;
        EntityName = brush.Name;
        EntityKind = $"Brush ({brush.Primitive}, {brush.Operation})";
        PositionText = FormatVector3(brush.Transform.Position);
        RotationText = FormatVector3(brush.Transform.EulerDegrees);
        ScaleText = FormatVector3(brush.Transform.Scale);
        BrushTextureKey = brush.MaterialName;
        SurfaceChips = BuildSurfaceChips(brush.Primitive, []);
        SelectedSurfaceSummary = "No surfaces selected";
        SurfaceOffsetUText = string.Empty;
        SurfaceOffsetVText = string.Empty;
        SurfaceScaleUText = string.Empty;
        SurfaceScaleVText = string.Empty;
        SurfaceRotationText = string.Empty;
        TextureLockEnabled = true;
        CanEditSurfaceMapping = false;
    }

    public void PopulateFromLight(LightEntity light)
    {
        HasEntity = true;
        CanEditTransform = false;
        CanEditTexturing = false;
        EntityName = light.Name;
        EntityKind = $"Light ({light.LightType})";
        PositionText = FormatVector3(light.Transform.Position);
        RotationText = FormatVector3(light.Transform.EulerDegrees);
        ScaleText = FormatVector3(light.Transform.Scale);
    }

    public void PopulateFromSpawnPoint(SpawnPoint spawnPoint)
    {
        HasEntity = true;
        CanEditTransform = false;
        CanEditTexturing = false;
        EntityName = spawnPoint.Name;
        EntityKind = "Spawn Point";
        PositionText = FormatVector3(spawnPoint.Transform.Position);
        RotationText = FormatVector3(spawnPoint.Transform.EulerDegrees);
        ScaleText = FormatVector3(spawnPoint.Transform.Scale);
    }

    public void Clear()
    {
        HasEntity  = false;
        CanEditTransform = false;
        CanEditTexturing = false;
        CanEditSurfaceMapping = false;
        EntityName = string.Empty;
        EntityKind = string.Empty;
        PositionText = string.Empty;
        RotationText = string.Empty;
        ScaleText = string.Empty;
        BrushTextureKey = string.Empty;
        SelectedSurfaceSummary = "No surfaces selected";
        SurfaceOffsetUText = string.Empty;
        SurfaceOffsetVText = string.Empty;
        SurfaceScaleUText = string.Empty;
        SurfaceScaleVText = string.Empty;
        SurfaceRotationText = string.Empty;
        TextureLockEnabled = true;
        SurfaceChips = Array.Empty<SurfaceSelectionChip>();
    }

    public void PopulateSurfaceMapping(Brush brush, IReadOnlyCollection<string> selectedSurfaceIds)
    {
        BrushTextureKey = brush.MaterialName;
        SurfaceChips = BuildSurfaceChips(brush.Primitive, selectedSurfaceIds);
        CanEditSurfaceMapping = selectedSurfaceIds.Count > 0;
        SelectedSurfaceSummary = selectedSurfaceIds.Count switch
        {
            0 => "No surfaces selected",
            1 => selectedSurfaceIds.First(),
            _ => $"{selectedSurfaceIds.Count} surfaces selected"
        };

        if (selectedSurfaceIds.Count == 0)
        {
            SurfaceOffsetUText = string.Empty;
            SurfaceOffsetVText = string.Empty;
            SurfaceScaleUText = string.Empty;
            SurfaceScaleVText = string.Empty;
            SurfaceRotationText = string.Empty;
            TextureLockEnabled = true;
            return;
        }

        var mapping = brush.GetEffectiveSurfaceMapping(selectedSurfaceIds.First());
        SurfaceOffsetUText = FormatFloat(mapping.Offset.X);
        SurfaceOffsetVText = FormatFloat(mapping.Offset.Y);
        SurfaceScaleUText = FormatFloat(mapping.Scale.X);
        SurfaceScaleVText = FormatFloat(mapping.Scale.Y);
        SurfaceRotationText = mapping.RotationDegrees.ToString("0.###", CultureInfo.InvariantCulture);
        TextureLockEnabled = mapping.TextureLocked;
    }

    internal static string FormatVector3(Vector3 value) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}");

    internal static string FormatVector2(Vector2 value) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{value.X:0.###}, {value.Y:0.###}");

    internal static string FormatFloat(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    internal static bool TryParseVector3(string text, out Vector3 value)
    {
        var parts = text
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 3 &&
            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
        {
            value = new Vector3(x, y, z);
            return true;
        }

        value = default;
        return false;
    }

    internal static bool TryParseVector2(string text, out Vector2 value)
    {
        var parts = text
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 2 &&
            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
        {
            value = new Vector2(x, y);
            return true;
        }

        value = default;
        return false;
    }

    internal static bool TryParseFloat(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static IReadOnlyList<SurfaceSelectionChip> BuildSurfaceChips(
        BrushPrimitive primitive,
        IReadOnlyCollection<string> selectedSurfaceIds)
    {
        var selected = selectedSurfaceIds.ToHashSet(StringComparer.Ordinal);
        return BrushSurfaceIds.GetSurfaceIds(primitive)
            .Select(surfaceId => new SurfaceSelectionChip(
                surfaceId,
                surfaceId[..1].ToUpperInvariant() + surfaceId[1..],
                selected.Contains(surfaceId)))
            .ToArray();
    }
}
