using MapEditor.Core;
using MapEditor.Core.Entities;

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
