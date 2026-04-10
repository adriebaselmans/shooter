namespace MapEditor.App;

internal enum EditorShortcutAction
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

internal interface IEditorShortcutTarget
{
    bool TryExecuteShortcut(EditorShortcutAction action, object? parameter = null);
}
