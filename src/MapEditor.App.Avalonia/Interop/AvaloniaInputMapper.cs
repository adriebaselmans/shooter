using Avalonia.Input;
using MapEditor.App.Tools;

namespace MapEditor.App.Avalonia.Interop;

internal static class AvaloniaInputMapper
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

    public static EditorModifierKeys ToEditorModifiers(KeyModifiers modifiers)
    {
        var editorModifiers = EditorModifierKeys.None;
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            editorModifiers |= EditorModifierKeys.Alt;
        }

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            editorModifiers |= EditorModifierKeys.Control;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            editorModifiers |= EditorModifierKeys.Shift;
        }

        return editorModifiers;
    }
}