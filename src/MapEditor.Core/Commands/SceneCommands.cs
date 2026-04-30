using MapEditor.Core;
using MapEditor.Core.Entities;
using System.Numerics;
using MapEditor.Core.Geometry;

namespace MapEditor.Core.Commands;

/// <summary>Adds a brush to the scene. Undo removes it.</summary>
public sealed class CreateBrushCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly Brush _brush;

    public CreateBrushCommand(Scene scene, Brush brush)
    {
        _scene = scene;
        _brush = brush;
    }

    public void Execute() => _scene.AddBrush(_brush);
    public void Undo() => _scene.RemoveBrush(_brush);
}

/// <summary>Removes a brush from the scene. Undo re-inserts it at its original index.</summary>
public sealed class DeleteBrushCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly Brush _brush;
    private int _originalIndex;

    public DeleteBrushCommand(Scene scene, Brush brush)
    {
        _scene = scene;
        _brush = brush;
    }

    public void Execute()
    {
        _originalIndex = _scene.Brushes.TakeWhile(b => !ReferenceEquals(b, _brush)).Count();
        _scene.RemoveBrush(_brush);
    }

    public void Undo() => _scene.InsertBrush(_originalIndex, _brush);
}

/// <summary>Changes a brush's transform. Undo restores the previous transform.</summary>
public sealed class TransformBrushCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly Brush _brush;
    private readonly Transform _oldTransform;
    private readonly Transform _newTransform;

    public TransformBrushCommand(Scene scene, Brush brush, Transform newTransform)
        : this(scene, brush, brush.Transform, newTransform)
    {
    }

    public TransformBrushCommand(Scene scene, Brush brush, Transform oldTransform, Transform newTransform)
    {
        _scene = scene;
        _brush = brush;
        _oldTransform = oldTransform;
        _newTransform = newTransform;
    }

    public void Execute()
    {
        _brush.Transform = _newTransform;
        _scene.RaiseChanged();
    }

    public void Undo()
    {
        _brush.Transform = _oldTransform;
        _scene.RaiseChanged();
    }
}

/// <summary>Sets an entity's Name property. Undo restores the previous name.</summary>
public sealed class RenameEntityCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly IEntity _entity;
    private readonly string _oldName;
    private readonly string _newName;

    public RenameEntityCommand(Scene scene, IEntity entity, string newName)
    {
        _scene = scene;
        _entity = entity;
        _oldName = entity.Name;
        _newName = newName;
    }

    public void Execute()
    {
        _entity.Name = _newName;
        _scene.RaiseChanged();
    }

    public void Undo()
    {
        _entity.Name = _oldName;
        _scene.RaiseChanged();
    }
}

/// <summary>Applies a default texture to a brush and optionally clears per-surface overrides.</summary>
public sealed class ApplyBrushTextureCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly Brush _brush;
    private readonly string _newTextureKey;
    private readonly string _oldTextureKey;
    private readonly Dictionary<string, SurfaceMapping> _oldSurfaceMappings;
    private readonly bool _clearSurfaceOverrides;

    public ApplyBrushTextureCommand(Scene scene, Brush brush, string textureKey, bool clearSurfaceOverrides = true)
    {
        _scene = scene;
        _brush = brush;
        _newTextureKey = string.IsNullOrWhiteSpace(textureKey) ? "default" : textureKey;
        _oldTextureKey = brush.MaterialName;
        _oldSurfaceMappings = brush.SurfaceMappings.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        _clearSurfaceOverrides = clearSurfaceOverrides;
    }

    public void Execute()
    {
        _brush.SetBrushTexture(_newTextureKey, _clearSurfaceOverrides);
        _scene.RaiseChanged();
    }

    public void Undo()
    {
        _brush.SetBrushTexture(_oldTextureKey, clearSurfaceOverrides: true);
        _brush.ReplaceSurfaceMappings(_oldSurfaceMappings);
        _scene.RaiseChanged();
    }
}

/// <summary>Updates the full mapping for one or more logical brush surfaces.</summary>
public sealed class UpdateSurfaceMappingCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly Brush _brush;
    private readonly IReadOnlyDictionary<string, SurfaceMapping?> _oldMappings;
    private readonly IReadOnlyDictionary<string, SurfaceMapping> _newMappings;

    public UpdateSurfaceMappingCommand(Scene scene, Brush brush, IReadOnlyDictionary<string, SurfaceMapping> newMappings)
    {
        _scene = scene;
        _brush = brush;
        _newMappings = new Dictionary<string, SurfaceMapping>(newMappings, StringComparer.Ordinal);
        var oldMappings = new Dictionary<string, SurfaceMapping?>(StringComparer.Ordinal);
        foreach (var key in newMappings.Keys)
        {
            oldMappings[key] = brush.SurfaceMappings.TryGetValue(key, out var mapping) ? mapping : null;
        }

        _oldMappings = oldMappings;
    }

    public void Execute()
    {
        foreach (var (surfaceId, mapping) in _newMappings)
        {
            _brush.SetSurfaceMapping(surfaceId, mapping);
        }

        _scene.RaiseChanged();
    }

    public void Undo()
    {
        foreach (var (surfaceId, mapping) in _oldMappings)
        {
            if (mapping.HasValue)
            {
                _brush.SetSurfaceMapping(surfaceId, mapping.Value);
            }
            else
            {
                _brush.ClearSurfaceMapping(surfaceId);
            }
        }

        _scene.RaiseChanged();
    }
}

/// <summary>Changes a brush operation between additive and subtractive.</summary>
public sealed class SetBrushOperationCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly Brush _brush;
    private readonly BrushOperation _oldOperation;
    private readonly BrushOperation _newOperation;

    public SetBrushOperationCommand(Scene scene, Brush brush, BrushOperation newOperation)
    {
        _scene = scene;
        _brush = brush;
        _oldOperation = brush.Operation;
        _newOperation = newOperation;
    }

    public void Execute()
    {
        _brush.Operation = _newOperation;
        _brush.TouchAppearance();
        _scene.RaiseChanged();
    }

    public void Undo()
    {
        _brush.Operation = _oldOperation;
        _brush.TouchAppearance();
        _scene.RaiseChanged();
    }
}

/// <summary>Subtracts the cutter brush from every intersecting brush and consumes the cutter.</summary>
public sealed class SubtractIntersectingBrushesCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly Brush _cutter;
    private readonly int _cutterIndex;
    private readonly IReadOnlyList<Brush> _targets;
    private readonly IReadOnlyDictionary<Guid, int> _targetIndices;
    private readonly IReadOnlyDictionary<Guid, Brush?> _replacementBrushes;

    public int AffectedBrushCount => _targets.Count;
    public IReadOnlyList<Brush> ReplacementBrushes => _replacementBrushes.Values.Where(candidate => candidate is not null).Cast<Brush>().ToArray();

    public SubtractIntersectingBrushesCommand(Scene scene, Brush cutter, IBrushBooleanKernel? kernel = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(cutter);

        _scene = scene;
        _cutter = cutter;
        _cutterIndex = scene.Brushes.TakeWhile(candidate => !ReferenceEquals(candidate, cutter)).Count();
        kernel ??= new BspBrushBooleanKernel();

        var targets = scene.Brushes
            .Where(candidate => !ReferenceEquals(candidate, cutter))
            .Where(candidate => kernel.HasIntersection(cutter, candidate))
            .ToArray();

        if (targets.Length == 0)
        {
            throw new ArgumentException("The cutter does not intersect any other brushes.", nameof(cutter));
        }

        _targets = targets;
        _targetIndices = _targets.ToDictionary(
            target => target.Id,
            target => scene.Brushes.TakeWhile(candidate => !ReferenceEquals(candidate, target)).Count());

        var replacements = new Dictionary<Guid, Brush?>();
        foreach (var target in _targets)
        {
            var result = kernel.Subtract(target, cutter);
            replacements[target.Id] = result is null ? null : BooleanResultBrushFactory.Create(target, result);
        }

        _replacementBrushes = replacements;
    }

    public void Execute()
    {
        _scene.RemoveBrush(_cutter);
        foreach (var target in _targets.OrderByDescending(target => _targetIndices[target.Id]))
        {
            _scene.RemoveBrush(target);
        }

        foreach (var target in _targets.OrderBy(target => _targetIndices[target.Id]))
        {
            var replacement = _replacementBrushes[target.Id];
            if (replacement is not null)
            {
                _scene.InsertBrush(Math.Min(_targetIndices[target.Id], _scene.Brushes.Count), replacement);
            }
        }
    }

    public void Undo()
    {
        foreach (var replacement in _replacementBrushes.Values.Where(candidate => candidate is not null).Cast<Brush>())
        {
            _scene.RemoveBrush(replacement);
        }

        var removedBrushes = _targets
            .Select(target => (Index: _targetIndices[target.Id], Brush: target))
            .Append((Index: _cutterIndex, Brush: _cutter))
            .OrderBy(item => item.Index);

        foreach (var (index, brush) in removedBrushes)
        {
            _scene.InsertBrush(Math.Min(index, _scene.Brushes.Count), brush);
        }
    }
}

/// <summary>Merges the selected brushes into one exact resulting brush.</summary>
public sealed class MergeSelectedBrushesCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly IReadOnlyList<Brush> _sources;
    private readonly IReadOnlyDictionary<Guid, int> _sourceIndices;
    private readonly Brush _mergedBrush;

    public Brush MergedBrush => _mergedBrush;

    public MergeSelectedBrushesCommand(Scene scene, IEnumerable<Brush> brushes, IBrushBooleanKernel? kernel = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(brushes);

        _scene = scene;
        _sources = brushes.ToArray();
        if (_sources.Count < 2)
        {
            throw new ArgumentException("At least two brushes are required to merge.", nameof(brushes));
        }

        _sourceIndices = _sources.ToDictionary(
            brush => brush.Id,
            brush => scene.Brushes.TakeWhile(candidate => !ReferenceEquals(candidate, brush)).Count());

        kernel ??= new BspBrushBooleanKernel();
        var result = kernel.Merge(_sources);
        if (result is null)
        {
            throw new ArgumentException("The selected brushes cannot be merged into one valid brush.", nameof(brushes));
        }

        _mergedBrush = BooleanResultBrushFactory.Create(_sources[0], result);
    }

    public void Execute()
    {
        foreach (var brush in _sources.OrderByDescending(brush => _sourceIndices[brush.Id]))
        {
            _scene.RemoveBrush(brush);
        }

        _scene.InsertBrush(Math.Min(_sourceIndices.Values.Min(), _scene.Brushes.Count), _mergedBrush);
    }

    public void Undo()
    {
        _scene.RemoveBrush(_mergedBrush);
        foreach (var brush in _sources.OrderBy(brush => _sourceIndices[brush.Id]))
        {
            _scene.InsertBrush(Math.Min(_sourceIndices[brush.Id], _scene.Brushes.Count), brush);
        }
    }
}

internal static class BooleanResultBrushFactory
{
    public static Brush Create(Brush source, BrushBooleanKernelResult result)
    {
        var brush = new Brush
        {
            Name = source.Name,
            Primitive = source.Primitive,
            Operation = source.Operation,
            MaterialName = source.MaterialName,
            Transform = result.Transform
        };

        brush.SetGeometry(result.Geometry);
        brush.ReplaceSurfaceMappings(result.SurfaceMappings);
        return brush;
    }
}

/// <summary>Adds a light to the scene. Undo removes it.</summary>
public sealed class CreateLightCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly LightEntity _light;

    public CreateLightCommand(Scene scene, LightEntity light) { _scene = scene; _light = light; }
    public void Execute() => _scene.AddLight(_light);
    public void Undo() => _scene.RemoveLight(_light);
}

/// <summary>Adds a spawn point to the scene. Undo removes it.</summary>
public sealed class CreateSpawnPointCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly SpawnPoint _sp;

    public CreateSpawnPointCommand(Scene scene, SpawnPoint sp) { _scene = scene; _sp = sp; }
    public void Execute() => _scene.AddSpawnPoint(_sp);
    public void Undo() => _scene.RemoveSpawnPoint(_sp);
}

/// <summary>Adds a pickup entity to the scene. Undo removes it.</summary>
public sealed class CreatePickupCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly PickupEntity _pickup;

    public CreatePickupCommand(Scene scene, PickupEntity pickup) { _scene = scene; _pickup = pickup; }
    public void Execute() => _scene.AddPickup(_pickup);
    public void Undo() => _scene.RemovePickup(_pickup);
}

/// <summary>Removes a pickup from the scene. Undo re-inserts it at its original index.</summary>
public sealed class DeletePickupCommand : ISceneCommand
{
    private readonly Scene _scene;
    private readonly PickupEntity _pickup;
    private int _originalIndex;

    public DeletePickupCommand(Scene scene, PickupEntity pickup) { _scene = scene; _pickup = pickup; }

    public void Execute()
    {
        _originalIndex = _scene.Pickups.TakeWhile(p => !ReferenceEquals(p, _pickup)).Count();
        _scene.RemovePickup(_pickup);
    }

    public void Undo() => _scene.InsertPickup(_originalIndex, _pickup);
}

/// <summary>Wraps multiple commands into one atomic undoable operation.</summary>
public sealed class CompositeCommand : ISceneCommand
{
    private readonly IReadOnlyList<ISceneCommand> _commands;

    public CompositeCommand(IEnumerable<ISceneCommand> commands)
    {
        _commands = commands.ToList();
    }

    public void Execute()
    {
        foreach (var cmd in _commands) cmd.Execute();
    }

    public void Undo()
    {
        foreach (var cmd in _commands.Reverse()) cmd.Undo();
    }
}
