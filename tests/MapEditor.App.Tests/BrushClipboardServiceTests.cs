using FluentAssertions;
using MapEditor.App.Services;
using MapEditor.Core.Entities;
using System.Numerics;

namespace MapEditor.App.Tests;

public sealed class BrushClipboardServiceTests
{
    [Fact]
    public void CreatePaste_ClonesBrushWithNewIdentityAndOffset()
    {
        var source = new Brush
        {
            Name = "Floor",
            Primitive = BrushPrimitive.Box,
            Operation = BrushOperation.Additive,
            Transform = new Transform
            {
                Position = new Vector3(10f, 20f, 30f),
                EulerDegrees = new Vector3(0f, 15f, 0f),
                Scale = new Vector3(64f, 32f, 64f)
            }
        };

        var service = new BrushClipboardService();
        service.Copy(source);

        var pasted = service.CreatePaste(new Vector3(32f, 0f, 32f));

        pasted.Should().NotBeNull();
        pasted!.Id.Should().NotBe(source.Id);
        pasted.Name.Should().Be(source.Name);
        pasted.Transform.Position.Should().Be(new Vector3(42f, 20f, 62f));
        pasted.Transform.Scale.Should().Be(source.Transform.Scale);
    }
}
