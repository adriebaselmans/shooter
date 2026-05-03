using System.Runtime.InteropServices;

namespace Shooter.Platform.Metal;

internal static partial class ObjCRuntime
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";

    [LibraryImport(ObjCLib)]
    public static partial IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport(ObjCLib)]
    public static partial IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend_UInt64(IntPtr receiver, IntPtr selector, ulong arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend_IntPtr_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend_UInt64_UInt64_Bool(IntPtr receiver, IntPtr selector, ulong arg1, ulong arg2, [MarshalAs(UnmanagedType.I1)] bool arg3);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend_UInt64_UInt64_UInt64_Bool(IntPtr receiver, IntPtr selector, ulong arg1, ulong arg2, ulong arg3, [MarshalAs(UnmanagedType.I1)] bool arg4);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend_UInt64_UInt64(IntPtr receiver, IntPtr selector, ulong arg1, ulong arg2);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend_IntPtr_UInt64_UInt64(IntPtr receiver, IntPtr selector, IntPtr arg1, ulong arg2, ulong arg3);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial IntPtr IntPtr_objc_msgSend_UInt64_UInt64_UInt64(IntPtr receiver, IntPtr selector, ulong arg1, ulong arg2, ulong arg3);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void Void_objc_msgSend(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void Void_objc_msgSend_Bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void Void_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void Void_objc_msgSend_UInt64(IntPtr receiver, IntPtr selector, ulong arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void Void_objc_msgSend_IntPtr_UInt64_UInt64(IntPtr receiver, IntPtr selector, IntPtr arg1, ulong arg2, ulong arg3);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void Void_objc_msgSend_IntPtr_UInt64(IntPtr receiver, IntPtr selector, IntPtr arg1, ulong arg2);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void Void_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void Void_objc_msgSend_Double(IntPtr receiver, IntPtr selector, double arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void Void_objc_msgSend_UInt64_UInt64_UInt64(IntPtr receiver, IntPtr selector, ulong arg1, ulong arg2, ulong arg3);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void Void_objc_msgSend_MTLRegion_UInt64_IntPtr_UInt64(IntPtr receiver, IntPtr selector, MTLRegion arg1, ulong arg2, IntPtr arg3, ulong arg4);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void Void_objc_msgSend_CGSize(IntPtr receiver, IntPtr selector, CGSize arg1);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    public static partial void Void_objc_msgSend_MTLClearColor(IntPtr receiver, IntPtr selector, MTLClearColor arg1);

    [LibraryImport(ObjCLib)]
    public static partial IntPtr objc_retain(IntPtr obj);

    [LibraryImport(ObjCLib)]
    public static partial void objc_release(IntPtr obj);
}

[StructLayout(LayoutKind.Sequential)]
internal struct CGSize
{
    public double Width;
    public double Height;

    public CGSize(double width, double height)
    {
        Width = width;
        Height = height;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct MTLClearColor
{
    public double Red;
    public double Green;
    public double Blue;
    public double Alpha;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MTLOrigin
{
    public ulong X;
    public ulong Y;
    public ulong Z;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MTLSize
{
    public ulong Width;
    public ulong Height;
    public ulong Depth;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MTLRegion
{
    public MTLOrigin Origin;
    public MTLSize Size;
}

internal static partial class MetalNative
{
    private const string MetalLib = "/System/Library/Frameworks/Metal.framework/Metal";

    [LibraryImport(MetalLib)]
    public static partial IntPtr MTLCreateSystemDefaultDevice();

    public static MTLClearColor CreateClearColor(double red, double green, double blue, double alpha) =>
        new()
        {
            Red = red,
            Green = green,
            Blue = blue,
            Alpha = alpha,
        };
}
