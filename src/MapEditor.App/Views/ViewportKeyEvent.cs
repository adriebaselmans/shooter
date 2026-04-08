using System.Windows.Input;

namespace MapEditor.App.Views;

public sealed class ViewportKeyEvent : EventArgs
{
    public ViewportKeyEvent(Key key, ModifierKeys modifiers)
    {
        Key = key;
        Modifiers = modifiers;
    }

    public Key Key { get; }
    public ModifierKeys Modifiers { get; }
    public bool Handled { get; set; }
}
