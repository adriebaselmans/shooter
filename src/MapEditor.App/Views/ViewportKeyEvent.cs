using MapEditor.App.Tools;

namespace MapEditor.App.Views;

public sealed class ViewportKeyEvent : EventArgs
{
    public ViewportKeyEvent(EditorKey key, EditorModifierKeys modifiers)
    {
        Key = key;
        Modifiers = modifiers;
    }

    public EditorKey Key { get; }
    public EditorModifierKeys Modifiers { get; }
    public bool Handled { get; set; }
}
