using MapEditor.Core.Entities;
using System.Numerics;

namespace MapEditor.App.Services;

/// <summary>Stores a local copy of the last copied brush for editor-local paste operations.</summary>
public sealed class BrushClipboardService
{
    private Brush? _copiedBrush;

    public bool HasBrush => _copiedBrush is not null;

    public void Copy(Brush brush)
    {
        _copiedBrush = brush.Clone();
    }

    public Brush? CreatePaste(Vector3 offset)
    {
        if (_copiedBrush is null)
        {
            return null;
        }

        var clone = _copiedBrush.Clone();
        clone.Transform = clone.Transform with
        {
            Position = clone.Transform.Position + offset
        };
        return clone;
    }
}
