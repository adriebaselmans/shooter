namespace MapEditor.Core.Entities;

/// <summary>CSG brush primitive shape types.</summary>
public enum BrushPrimitive { Box, Cylinder, Cone, Wedge }

/// <summary>Determines whether a brush adds or subtracts from solid space.</summary>
public enum BrushOperation { Additive, Subtractive }

/// <summary>A CSG brush entity defining a geometric region in the scene.</summary>
public sealed class Brush : IEntity
{
    private readonly Dictionary<string, SurfaceMapping> _surfaceMappings = new(StringComparer.Ordinal);

    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Brush";
    public BrushOperation Operation { get; set; } = BrushOperation.Additive;
    public BrushPrimitive Primitive { get; set; } = BrushPrimitive.Box;
    public Transform Transform { get; set; } = Transform.Identity;
    public string MaterialName { get; set; } = "default";
    public IReadOnlyDictionary<string, SurfaceMapping> SurfaceMappings => _surfaceMappings;
    public int AppearanceVersion { get; private set; }

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
            AppearanceVersion = AppearanceVersion
        };

        foreach (var (surfaceId, mapping) in _surfaceMappings)
        {
            clone._surfaceMappings[surfaceId] = mapping;
        }

        return clone;
    }

    public SurfaceMapping GetEffectiveSurfaceMapping(string surfaceId) =>
        _surfaceMappings.TryGetValue(surfaceId, out var mapping)
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
        if (!BrushSurfaceIds.IsValid(Primitive, surfaceId))
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
            if (BrushSurfaceIds.IsValid(Primitive, surfaceId))
            {
                _surfaceMappings[surfaceId] = mapping;
            }
        }

        TouchAppearance();
    }

    public void TouchAppearance() => AppearanceVersion++;
}
