using FluentAssertions;
using MapEditor.App.ViewModels;
using System.Numerics;

namespace MapEditor.App.Tests;

public sealed class PropertiesViewModelTests
{
    [Fact]
    public void TryParseVector3_ParsesCommaSeparatedValues()
    {
        PropertiesViewModel.TryParseVector3("1.5, 2, -3.25", out var value).Should().BeTrue();
        value.Should().Be(new Vector3(1.5f, 2f, -3.25f));
    }

    [Fact]
    public void TryParseVector3_ReturnsFalseForInvalidInput()
    {
        PropertiesViewModel.TryParseVector3("1, two, 3", out _).Should().BeFalse();
    }
}
