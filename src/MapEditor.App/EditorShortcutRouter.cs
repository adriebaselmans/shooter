using MapEditor.App.Tools;

namespace MapEditor.App;

public static class EditorShortcutRouter
{
    public static bool TryHandle(IEditorShortcutTarget target, EditorKey key, EditorModifierKeys modifiers, bool isTextEditingSource)
    {
        if (isTextEditingSource)
        {
            return false;
        }

        if (modifiers == EditorModifierKeys.Control)
        {
            return key switch
            {
                EditorKey.N => target.TryExecuteShortcut(EditorShortcutAction.NewFile),
                EditorKey.O => target.TryExecuteShortcut(EditorShortcutAction.OpenFile),
                EditorKey.S => target.TryExecuteShortcut(EditorShortcutAction.SaveFile),
                EditorKey.Z => target.TryExecuteShortcut(EditorShortcutAction.Undo),
                EditorKey.Y => target.TryExecuteShortcut(EditorShortcutAction.Redo),
                EditorKey.C => target.TryExecuteShortcut(EditorShortcutAction.Copy),
                EditorKey.V => target.TryExecuteShortcut(EditorShortcutAction.Paste),
                _ => false
            };
        }

        if (modifiers == (EditorModifierKeys.Control | EditorModifierKeys.Shift) && key == EditorKey.Z)
        {
            return target.TryExecuteShortcut(EditorShortcutAction.Redo);
        }

        if (modifiers != EditorModifierKeys.None)
        {
            return false;
        }

        return key switch
        {
            EditorKey.Delete => target.TryExecuteShortcut(EditorShortcutAction.Delete),
            EditorKey.B => target.TryExecuteShortcut(EditorShortcutAction.CreateBrush),
            EditorKey.T => target.TryExecuteShortcut(EditorShortcutAction.ToggleTextureBrowser),
            EditorKey.Escape => target.TryExecuteShortcut(EditorShortcutAction.SelectTool, nameof(EditorToolKind.Select)),
            _ => false
        };
    }
}
