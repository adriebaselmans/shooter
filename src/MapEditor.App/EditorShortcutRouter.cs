using MapEditor.App.ViewModels;
using MapEditor.App.Tools;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MapEditor.App;

internal static class EditorShortcutRouter
{
    public static bool TryHandle(IEditorShortcutTarget target, Key key, ModifierKeys modifiers, object? originalSource)
    {
        if (originalSource is TextBoxBase)
        {
            return false;
        }

        if (modifiers == ModifierKeys.Control)
        {
            return key switch
            {
                Key.N => target.TryExecuteShortcut(EditorShortcutAction.NewFile),
                Key.O => target.TryExecuteShortcut(EditorShortcutAction.OpenFile),
                Key.S => target.TryExecuteShortcut(EditorShortcutAction.SaveFile),
                Key.Z => target.TryExecuteShortcut(EditorShortcutAction.Undo),
                Key.Y => target.TryExecuteShortcut(EditorShortcutAction.Redo),
                Key.C => target.TryExecuteShortcut(EditorShortcutAction.Copy),
                Key.V => target.TryExecuteShortcut(EditorShortcutAction.Paste),
                _ => false
            };
        }

        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.Z)
        {
            return target.TryExecuteShortcut(EditorShortcutAction.Redo);
        }

        if (modifiers != ModifierKeys.None)
        {
            return false;
        }

        return key switch
        {
            Key.Delete => target.TryExecuteShortcut(EditorShortcutAction.Delete),
            Key.B => target.TryExecuteShortcut(EditorShortcutAction.CreateBrush),
            Key.Escape => target.TryExecuteShortcut(EditorShortcutAction.SelectTool, nameof(EditorToolKind.Select)),
            _ => false
        };
    }
}
