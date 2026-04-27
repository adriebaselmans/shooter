using MapEditor.App.Infrastructure;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MapEditor.App;

internal static class WpfEditorShortcutRouter
{
    public static bool TryHandle(IEditorShortcutTarget target, Key key, ModifierKeys modifiers, object? originalSource)
    {
        return EditorShortcutRouter.TryHandle(
            target,
            WpfInputMapper.ToEditorKey(key),
            WpfInputMapper.ToEditorModifiers(modifiers),
            originalSource is TextBoxBase);
    }
}