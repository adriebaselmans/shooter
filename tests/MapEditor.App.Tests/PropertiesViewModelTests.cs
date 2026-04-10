using FluentAssertions;
using MapEditor.Core.Entities;
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

    [Fact]
    public void PopulateSurfaceMapping_UsesSplitFieldsAndSelectionChips()
    {
        var viewModel = new PropertiesViewModel();
        var brush = new Brush
        {
            Primitive = BrushPrimitive.Box,
            MaterialName = "brick_red.png"
        };
        brush.SetSurfaceMapping(BrushSurfaceIds.Top, new SurfaceMapping("brick_red.png", new Vector2(2f, 4f), new Vector2(0.5f, 1.5f), 45f, true));

        viewModel.PopulateFromBrush(brush);
        viewModel.PopulateSurfaceMapping(brush, [BrushSurfaceIds.Top, BrushSurfaceIds.Front]);

        viewModel.SurfaceOffsetUText.Should().Be("2");
        viewModel.SurfaceOffsetVText.Should().Be("4");
        viewModel.SurfaceScaleUText.Should().Be("0.5");
        viewModel.SurfaceScaleVText.Should().Be("1.5");
        viewModel.SurfaceChips.Should().Contain(chip => chip.SurfaceId == BrushSurfaceIds.Top && chip.IsSelected);
        viewModel.SurfaceChips.Should().Contain(chip => chip.SurfaceId == BrushSurfaceIds.Front && chip.IsSelected);
        viewModel.SurfaceChips.Should().Contain(chip => chip.SurfaceId == BrushSurfaceIds.Bottom && !chip.IsSelected);
    }
}
