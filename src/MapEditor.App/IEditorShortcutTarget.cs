namespace MapEditor.App;

public enum EditorShortcutAction
{
    NewFile,
    OpenFile,
    SaveFile,
    Undo,
    Redo,
    Copy,
    Paste,
    Delete,
    CreateBrush,
    ToggleTextureBrowser,
    SelectTool
}

public interface IEditorShortcutTarget
{
    bool TryExecuteShortcut(EditorShortcutAction action, object? parameter = null);
}
