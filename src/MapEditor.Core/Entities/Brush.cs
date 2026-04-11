using MapEditor.Core.Geometry;

namespace MapEditor.Core.Entities;

/// <summary>CSG brush primitive shape types.</summary>
public enum BrushPrimitive { Box, Cylinder, Cone, Wedge }

/// <summary>Determines whether a brush adds or subtracts from solid space.</summary>
public enum BrushOperation { Additive, Subtractive }

/// <summary>A CSG brush entity defining a geometric region in the scene.</summary>
public sealed class Brush : IEntity
{
    private readonly Dictionary<string, SurfaceMapping> _surfaceMappings = new(StringComparer.Ordinal);
    private BrushGeometry? _geometry;

    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Brush";
    public BrushOperation Operation { get; set; } = BrushOperation.Additive;
    public BrushPrimitive Primitive { get; set; } = BrushPrimitive.Box;
    public Transform Transform { get; set; } = Transform.Identity;
    public string MaterialName { get; set; } = "default";
    public IReadOnlyDictionary<string, SurfaceMapping> SurfaceMappings => _surfaceMappings;
    public int AppearanceVersion { get; private set; }
    public int GeometryVersion { get; private set; }
    public BrushGeometry? Geometry => _geometry?.Clone();
    public bool HasExplicitGeometry => _geometry is not null;

    public Brush() { }

    public Brush(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>Returns a shallow copy with a new identity.</summary>
    public Brush Clone()
    {
        var clone = new Brush(Guid.NewGuid(), Name)
        {
            Operation = Operation,
            Primitive = Primitive,
            Transform = Transform,
            MaterialName = MaterialName,
            AppearanceVersion = AppearanceVersion,
            GeometryVersion = GeometryVersion
        };

        if (_geometry is not null)
        {
            clone._geometry = _geometry.Clone();
        }

        foreach (var (surfaceId, mapping) in _surfaceMappings)
        {
            clone._surfaceMappings[surfaceId] = mapping;
        }

        return clone;
    }

    public SurfaceMapping GetEffectiveSurfaceMapping(string surfaceId) =>
        _surfaceMappings.TryGetValue(surfaceId, out var mapping)
            ? mapping
            : TryGetLogicalSurfaceMapping(surfaceId, out mapping)
                ? mapping
                : SurfaceMapping.Default(MaterialName);

    public void SetBrushTexture(string textureKey, bool clearSurfaceOverrides = true)
    {
        MaterialName = string.IsNullOrWhiteSpace(textureKey) ? "default" : textureKey;
        if (clearSurfaceOverrides)
        {
            _surfaceMappings.Clear();
        }

        TouchAppearance();
    }

    public void SetSurfaceMapping(string surfaceId, SurfaceMapping mapping)
    {
        if (!HasSurface(surfaceId))
        {
            throw new ArgumentOutOfRangeException(nameof(surfaceId), surfaceId, "Surface is not valid for this primitive.");
        }

        _surfaceMappings[surfaceId] = mapping with
        {
            TextureKey = string.IsNullOrWhiteSpace(mapping.TextureKey) ? MaterialName : mapping.TextureKey
        };
        TouchAppearance();
    }

    public void ClearSurfaceMapping(string surfaceId)
    {
        if (_surfaceMappings.Remove(surfaceId))
        {
            TouchAppearance();
        }
    }

    public void ReplaceSurfaceMappings(IEnumerable<KeyValuePair<string, SurfaceMapping>> mappings)
    {
        _surfaceMappings.Clear();
        foreach (var (surfaceId, mapping) in mappings)
        {
            if (HasSurface(surfaceId))
            {
                _surfaceMappings[surfaceId] = mapping;
            }
        }

        TouchAppearance();
    }

    public void TouchAppearance() => AppearanceVersion++;

    public IReadOnlyList<string> GetSurfaceIds() =>
        _geometry?.GetFaceIds() ?? BrushSurfaceIds.GetSurfaceIds(Primitive);

    public bool HasSurface(string surfaceId) =>
        GetSurfaceIds().Contains(surfaceId, StringComparer.Ordinal);

    public void SetGeometry(BrushGeometry? geometry)
    {
        _geometry = geometry?.Clone();
        GeometryVersion++;

        var validSurfaceIds = GetSurfaceIds().ToHashSet(StringComparer.Ordinal);
        var validMappings = _surfaceMappings
            .Where(pair => validSurfaceIds.Contains(pair.Key))
            .ToArray();
        _surfaceMappings.Clear();
        foreach (var (surfaceId, mapping) in validMappings)
        {
            _surfaceMappings[surfaceId] = mapping;
        }

        TouchAppearance();
    }

    private bool TryGetLogicalSurfaceMapping(string surfaceId, out SurfaceMapping mapping)
    {
        if (HasExplicitGeometry)
        {
            mapping = default;
            return false;
        }

        int separatorIndex = surfaceId.IndexOf('-');
        if (separatorIndex <= 0)
        {
            mapping = default;
            return false;
        }

        return _surfaceMappings.TryGetValue(surfaceId[..separatorIndex], out mapping);
    }
}
