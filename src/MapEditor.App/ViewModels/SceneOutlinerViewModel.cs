using CommunityToolkit.Mvvm.ComponentModel;
using MapEditor.App.Services;
using MapEditor.Core.Entities;
using MapEditor.Core;
using System.Collections.ObjectModel;

namespace MapEditor.App.ViewModels;

/// <summary>Represents one row in the scene outliner tree.</summary>
public sealed class OutlinerItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
}

/// <summary>Drives the scene outliner panel.</summary>
public sealed partial class SceneOutlinerViewModel : ObservableObject
{
    private readonly SelectionService _selectionService;
    private bool _syncingSelection;

    public SceneOutlinerViewModel(SelectionService selectionService)
    {
        _selectionService = selectionService;
        _selectionService.SelectionChanged += OnSelectionChanged;
    }

    [ObservableProperty] private Guid _selectedEntityId;

    public ObservableCollection<OutlinerItemViewModel> Items { get; } = new();

    public void Refresh(Scene scene)
    {
        Items.Clear();
        foreach (var b in scene.Brushes)
            Items.Add(new OutlinerItemViewModel { Id = b.Id, Name = b.Name, Kind = $"Brush ({b.Primitive}, {b.Operation})" });
        foreach (var l in scene.Lights)
            Items.Add(new OutlinerItemViewModel { Id = l.Id, Name = l.Name, Kind = $"Light ({l.LightType})" });
        foreach (var s in scene.SpawnPoints)
            Items.Add(new OutlinerItemViewModel { Id = s.Id, Name = s.Name, Kind = "SpawnPoint" });
    }

    partial void OnSelectedEntityIdChanged(Guid value)
    {
        if (_syncingSelection)
        {
            return;
        }

        if (value == Guid.Empty)
        {
            _selectionService.Clear();
            return;
        }

        _selectionService.SetSingle(value);
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        _syncingSelection = true;
        SelectedEntityId = _selectionService.PrimarySelectionId ?? Guid.Empty;
        _syncingSelection = false;
    }
}
