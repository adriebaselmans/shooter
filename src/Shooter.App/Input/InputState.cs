using System.Numerics;

namespace Shooter.Input;

public enum InputKey
{
    W, A, S, D, Space, Shift, Ctrl, Esc, F1,
    Num1, Num2, Num3,
    MouseLeft,
}

/// <summary>Per-frame snapshot of input state. Set by the platform layer (Silk.NET in Program.cs).</summary>
public sealed class InputState
{
    private readonly bool[] _down = new bool[Enum.GetValues<InputKey>().Length];
    private readonly bool[] _pressedThisFrame = new bool[Enum.GetValues<InputKey>().Length];
    public Vector2 MouseDelta;
    public float ScrollDelta;

    public bool IsDown(InputKey k) => _down[(int)k];
    public bool WasPressed(InputKey k) => _pressedThisFrame[(int)k];

    public void SetDown(InputKey k, bool down)
    {
        int i = (int)k;
        if (down && !_down[i]) _pressedThisFrame[i] = true;
        _down[i] = down;
    }

    /// <summary>Call once at the end of each frame after game logic ran.</summary>
    public void EndFrame()
    {
        Array.Clear(_pressedThisFrame, 0, _pressedThisFrame.Length);
        MouseDelta = Vector2.Zero;
        ScrollDelta = 0;
    }
}
