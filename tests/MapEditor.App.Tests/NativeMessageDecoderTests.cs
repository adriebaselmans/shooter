using FluentAssertions;
using MapEditor.App.Views;

namespace MapEditor.App.Tests;

public sealed class NativeMessageDecoderTests
{
    [Fact]
    public void GetWheelDelta_DecodesPositiveDeltaWithout32BitOverflow()
    {
        var wheelMessage = unchecked((nint)0x0000000100780000L);

        NativeMessageDecoder.GetWheelDelta(wheelMessage).Should().Be(120);
    }

    [Fact]
    public void GetWheelDelta_DecodesNegativeDeltaWithout32BitOverflow()
    {
        var wheelMessage = unchecked((nint)0x00000001FF880000L);

        NativeMessageDecoder.GetWheelDelta(wheelMessage).Should().Be(-120);
    }
}
