using MapEditor.Core.Commands;
using MapEditor.Core.Entities;

namespace MapEditor.Core;

/// <summary>
/// Facade for all scene mutations. Ensures all changes go through the command system,
/// maintaining undo/redo integrity and notifying observers via SceneChanged.
/// </summary>
public sealed class SceneService
{
    private readonly CommandHistory _history = new();

    public Scene Scene { get; } = new();

    /// <summary>Bubbles up from the underlying Scene.SceneChanged event.</summary>
    public event EventHandler<SceneChangedEventArgs>? SceneChanged;

    public SceneService()
    {
        Scene.SceneChanged += (s, e) => SceneChanged?.Invoke(this, e);
    }

    /// <summary>Executes a command and records it in the undo/redo history.</summary>
    public void Execute(ISceneCommand command)
    {
        _history.Execute(command);
    }

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    public void Undo() => _history.Undo();
    public void Redo() => _history.Redo();

    /// <summary>
    /// Replaces the scene with <paramref name="newScene"/> without adding to undo history.
    /// Used for file open / new operations.
    /// </summary>
    public void ReplaceScene(Scene newScene)
    {
        _history.Clear();
        Scene.ReplaceFrom(newScene);
    }
}
