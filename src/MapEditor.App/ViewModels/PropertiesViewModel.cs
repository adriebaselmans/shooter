using CommunityToolkit.Mvvm.ComponentModel;
using MapEditor.Core.Entities;
using System.Globalization;
using System.Numerics;

namespace MapEditor.App.ViewModels;

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

    public void PopulateFromBrush(Brush brush)
    {
        HasEntity  = true;
        CanEditTransform = true;
        EntityName = brush.Name;
        EntityKind = $"Brush ({brush.Primitive}, {brush.Operation})";
        PositionText = FormatVector3(brush.Transform.Position);
        RotationText = FormatVector3(brush.Transform.EulerDegrees);
        ScaleText = FormatVector3(brush.Transform.Scale);
    }

    public void PopulateFromLight(LightEntity light)
    {
        HasEntity = true;
        CanEditTransform = false;
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
        EntityName = string.Empty;
        EntityKind = string.Empty;
        PositionText = string.Empty;
        RotationText = string.Empty;
        ScaleText = string.Empty;
    }

    internal static string FormatVector3(Vector3 value) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}");

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
}
