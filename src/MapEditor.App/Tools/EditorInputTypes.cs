namespace MapEditor.App.Tools;

public readonly record struct ViewportPoint(double X, double Y)
{
    public static ViewportVector operator -(ViewportPoint left, ViewportPoint right) =>
        new(left.X - right.X, left.Y - right.Y);
}

public readonly record struct ViewportVector(double X, double Y);

[Flags]
public enum EditorModifierKeys
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4
}

public enum EditorKey
{
    Unknown,
    N,
    O,
    S,
    Z,
    Y,
    C,
    V,
    Delete,
    B,
    T,
    Escape
}