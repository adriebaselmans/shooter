using System.Windows.Input;
using MapEditor.App.Tools;

namespace MapEditor.App.Infrastructure;

internal static class WpfInputMapper
{
    public static EditorKey ToEditorKey(Key key) => key switch
    {
        Key.N => EditorKey.N,
        Key.O => EditorKey.O,
        Key.S => EditorKey.S,
        Key.Z => EditorKey.Z,
        Key.Y => EditorKey.Y,
        Key.C => EditorKey.C,
        Key.V => EditorKey.V,
        Key.Delete => EditorKey.Delete,
        Key.B => EditorKey.B,
        Key.T => EditorKey.T,
        Key.Escape => EditorKey.Escape,
        _ => EditorKey.Unknown
    };

    public static EditorModifierKeys ToEditorModifiers(ModifierKeys modifiers)
    {
        var editorModifiers = EditorModifierKeys.None;
        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            editorModifiers |= EditorModifierKeys.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            editorModifiers |= EditorModifierKeys.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            editorModifiers |= EditorModifierKeys.Shift;
        }

        return editorModifiers;
    }
}