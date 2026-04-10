namespace MapEditor.App.Services;

/// <summary>Tracks the currently selected logical brush surfaces for texture editing.</summary>
public sealed class SurfaceSelectionService
{
    private readonly HashSet<string> _selectedSurfaceIds = new(StringComparer.Ordinal);

    public event EventHandler? SelectionChanged;

    public Guid? BrushId { get; private set; }
    public IReadOnlyCollection<string> SelectedSurfaceIds => _selectedSurfaceIds;
    public bool HasSelection => BrushId is not null && _selectedSurfaceIds.Count > 0;

    public bool IsSelected(Guid brushId, string surfaceId) =>
        BrushId == brushId && _selectedSurfaceIds.Contains(surfaceId);

    public void Replace(Guid? brushId, IEnumerable<string> surfaceIds)
    {
        var nextBrushId = brushId == Guid.Empty ? null : brushId;
        var nextIds = surfaceIds
            .Where(surfaceId => !string.IsNullOrWhiteSpace(surfaceId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (BrushId == nextBrushId && _selectedSurfaceIds.SetEquals(nextIds))
        {
            return;
        }

        BrushId = nextBrushId;
        _selectedSurfaceIds.Clear();
        foreach (var surfaceId in nextIds)
        {
            _selectedSurfaceIds.Add(surfaceId);
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (BrushId is null && _selectedSurfaceIds.Count == 0)
        {
            return;
        }

        BrushId = null;
        _selectedSurfaceIds.Clear();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
