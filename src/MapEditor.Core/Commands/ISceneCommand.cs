namespace MapEditor.Core.Commands;

/// <summary>A reversible scene mutation command.</summary>
public interface ISceneCommand
{
    void Execute();
    void Undo();
}
