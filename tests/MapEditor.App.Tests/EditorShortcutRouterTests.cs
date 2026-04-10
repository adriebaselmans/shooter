using FluentAssertions;
using System.Windows.Controls;
using System.Windows.Input;
namespace MapEditor.App.Tests;

public sealed class EditorShortcutRouterTests
{
    [Fact]
    public void TryHandle_UsesControlSaveCommand()
    {
        var target = new FakeShortcutTarget();

        var handled = EditorShortcutRouter.TryHandle(target, Key.S, ModifierKeys.Control, originalSource: null);

        handled.Should().BeTrue();
        target.Executed.Should().ContainSingle().Which.Should().Be(EditorShortcutAction.SaveFile.ToString());
    }

    [Fact]
    public void TryHandle_IgnoresTextEditingSource()
    {
        var target = new FakeShortcutTarget();
        bool handled = false;

        RunInSta(() =>
        {
            handled = EditorShortcutRouter.TryHandle(target, Key.B, ModifierKeys.None, new TextBox());
        });

        handled.Should().BeFalse();
        target.Executed.Should().BeEmpty();
    }

    [Fact]
    public void TryHandle_MapsEscapeToSelectToolCommand()
    {
        var target = new FakeShortcutTarget();

        var handled = EditorShortcutRouter.TryHandle(target, Key.Escape, ModifierKeys.None, originalSource: null);

        handled.Should().BeTrue();
        target.Executed.Should().ContainSingle().Which.Should().Be($"{EditorShortcutAction.SelectTool}:Select");
    }

    [Fact]
    public void TryHandle_MapsTToTextureBrowserToggle()
    {
        var target = new FakeShortcutTarget();

        var handled = EditorShortcutRouter.TryHandle(target, Key.T, ModifierKeys.None, originalSource: null);

        handled.Should().BeTrue();
        target.Executed.Should().ContainSingle().Which.Should().Be(EditorShortcutAction.ToggleTextureBrowser.ToString());
    }

    private sealed class FakeShortcutTarget : IEditorShortcutTarget
    {
        public List<string> Executed { get; } = [];

        public bool TryExecuteShortcut(EditorShortcutAction action, object? parameter = null)
        {
            Executed.Add(parameter is null ? action.ToString() : $"{action}:{parameter}");
            return true;
        }
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw failure;
        }
    }
}
