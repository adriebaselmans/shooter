using System.Numerics;
using Silk.NET.Input;

namespace Shooter.Input;

/// <summary>Owns Silk input event hookup and translates raw events into InputState updates.</summary>
internal sealed class GameInputBinder
{
    private readonly InputState _inputState;
    private Vector2 _lastMouse;
    private bool _firstMouse = true;

    public IMouse? PrimaryMouse { get; private set; }

    public GameInputBinder(IInputContext input, InputState inputState)
    {
        _inputState = inputState;

        if (input.Mice.Count > 0)
        {
            PrimaryMouse = input.Mice[0];
            PrimaryMouse.Cursor.CursorMode = CursorMode.Raw;
            PrimaryMouse.MouseMove += OnMouseMove;
            PrimaryMouse.MouseDown += OnMouseDown;
            PrimaryMouse.MouseUp += OnMouseUp;
            PrimaryMouse.Scroll += OnMouseScroll;
        }

        foreach (var keyboard in input.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
        }
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (_firstMouse)
        {
            _lastMouse = position;
            _firstMouse = false;
            return;
        }

        var delta = position - _lastMouse;
        _lastMouse = position;
        _inputState.MouseDelta += delta;
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
            _inputState.SetDown(InputKey.MouseLeft, true);
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
            _inputState.SetDown(InputKey.MouseLeft, false);
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel scroll)
    {
        _inputState.ScrollDelta += scroll.Y;
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scanCode) => SetMappedKey(key, true);
    private void OnKeyUp(IKeyboard keyboard, Key key, int scanCode) => SetMappedKey(key, false);

    private void SetMappedKey(Key key, bool down)
    {
        InputKey? mapped = key switch
        {
            Key.W => InputKey.W,
            Key.A => InputKey.A,
            Key.S => InputKey.S,
            Key.D => InputKey.D,
            Key.Space => InputKey.Space,
            Key.ShiftLeft or Key.ShiftRight => InputKey.Shift,
            Key.ControlLeft or Key.ControlRight => InputKey.Ctrl,
            Key.Escape => InputKey.Esc,
            Key.F1 => InputKey.F1,
            Key.Number1 or Key.Keypad1 => InputKey.Num1,
            Key.Number2 or Key.Keypad2 => InputKey.Num2,
            Key.Number3 or Key.Keypad3 => InputKey.Num3,
            _ => null,
        };

        if (mapped is { } inputKey)
            _inputState.SetDown(inputKey, down);
    }
}
