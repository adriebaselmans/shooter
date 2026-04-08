using System.Windows;

namespace MapEditor.App.Views;

internal static class NativeMessageDecoder
{
    public static Point GetPoint(IntPtr lParam) =>
        new(GetSignedLowWord(lParam), GetSignedHighWord(lParam));

    public static int GetWheelDelta(IntPtr wParam) =>
        GetSignedHighWord(wParam);

    private static short GetSignedLowWord(IntPtr value) =>
        unchecked((short)(value.ToInt64() & 0xFFFF));

    private static short GetSignedHighWord(IntPtr value) =>
        unchecked((short)((value.ToInt64() >> 16) & 0xFFFF));
}
