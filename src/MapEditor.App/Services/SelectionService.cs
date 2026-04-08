using MapEditor.Core;

namespace MapEditor.App.Services;

/// <summary>
/// Tracks the current editor selection and raises a notification whenever it changes.
/// Supports a primary selection plus optional additive selections.
/// </summary>
public sealed class SelectionService
{
    private readonly HashSet<Guid> _selectedIds = [];
    private Guid? _primarySelectionId;

    public event EventHandler? SelectionChanged;

    public IReadOnlyCollection<Guid> SelectedEntityIds => _selectedIds;

    public Guid? PrimarySelectionId => _primarySelectionId;

    public bool HasSelection => _selectedIds.Count > 0;

    public bool IsSelected(Guid entityId) => _selectedIds.Contains(entityId);

    public void SetSingle(Guid? entityId)
    {
        if (!entityId.HasValue || entityId.Value == Guid.Empty)
        {
            Clear();
            return;
        }

        if (_selectedIds.Count == 1 && _primarySelectionId == entityId.Value)
        {
            return;
        }

        _selectedIds.Clear();
        _selectedIds.Add(entityId.Value);
        _primarySelectionId = entityId.Value;
        RaiseChanged();
    }

    public void Add(Guid entityId)
    {
        if (entityId == Guid.Empty)
        {
            return;
        }

        var changed = _selectedIds.Add(entityId);
        _primarySelectionId = entityId;
        if (changed)
        {
            RaiseChanged();
        }
    }

    public void Toggle(Guid entityId)
    {
        if (entityId == Guid.Empty)
        {
            return;
        }

        if (_selectedIds.Remove(entityId))
        {
            if (_primarySelectionId == entityId)
            {
                _primarySelectionId = _selectedIds.LastOrDefault();
                if (_primarySelectionId == Guid.Empty)
                {
                    _primarySelectionId = null;
                }
            }

            RaiseChanged();
            return;
        }

        _selectedIds.Add(entityId);
        _primarySelectionId = entityId;
        RaiseChanged();
    }

    public void Clear()
    {
        if (_selectedIds.Count == 0 && _primarySelectionId is null)
        {
            return;
        }

        _selectedIds.Clear();
        _primarySelectionId = null;
        RaiseChanged();
    }

    public void RemoveMissing(Scene scene)
    {
        var validIds = scene.Brushes.Select(b => b.Id)
            .Concat(scene.Lights.Select(l => l.Id))
            .Concat(scene.SpawnPoints.Select(s => s.Id))
            .ToHashSet();

        if (_selectedIds.RemoveWhere(id => !validIds.Contains(id)) == 0)
        {
            return;
        }

        if (_primarySelectionId is null || !validIds.Contains(_primarySelectionId.Value))
        {
            _primarySelectionId = _selectedIds.LastOrDefault();
            if (_primarySelectionId == Guid.Empty)
            {
                _primarySelectionId = null;
            }
        }

        RaiseChanged();
    }

    private void RaiseChanged() => SelectionChanged?.Invoke(this, EventArgs.Empty);
}
