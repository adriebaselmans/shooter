namespace MapEditor.Core.Commands;

/// <summary>
/// Dual-stack undo/redo history capped at <see cref="Capacity"/> levels.
/// New commands clear the redo stack.
/// </summary>
public sealed class CommandHistory
{
    public const int Capacity = 50;

    private readonly Stack<ISceneCommand> _undoStack = new();
    private readonly Stack<ISceneCommand> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Executes <paramref name="command"/>, records it, and clears redo history.</summary>
    public void Execute(ISceneCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();

        while (_undoStack.Count > Capacity)
            TrimOldest();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
    }

    /// <summary>Clears both stacks. Called when loading a new scene.</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    private void TrimOldest()
    {
        // Stack doesn't allow removal from bottom; rebuild without the oldest entry.
        var temp = _undoStack.Reverse().Skip(1).ToArray();
        _undoStack.Clear();
        foreach (var cmd in temp)
            _undoStack.Push(cmd);
    }
}
