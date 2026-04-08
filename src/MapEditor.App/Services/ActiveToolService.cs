using MapEditor.App.Tools;
using MapEditor.Core.Entities;

namespace MapEditor.App.Services;

/// <summary>
/// Tracks the current editor tool and exposes concrete tool instances for viewport interaction.
/// </summary>
public sealed class ActiveToolService
{
    private readonly IReadOnlyDictionary<EditorToolKind, IEditorTool> _tools;
    private EditorToolKind _currentToolKind = EditorToolKind.Select;

    public ActiveToolService(SelectTool selectTool, CreateBrushTool createBrushTool, MoveTool moveTool)
    {
        _tools = new Dictionary<EditorToolKind, IEditorTool>
        {
            [EditorToolKind.Select] = selectTool,
            [EditorToolKind.CreateBrush] = createBrushTool,
            [EditorToolKind.Move] = moveTool
        };
    }

    public event EventHandler<EditorToolKind>? ToolChanged;

    public EditorToolKind CurrentToolKind => _currentToolKind;

    public BrushPrimitive SelectedBrushPrimitive { get; private set; } = BrushPrimitive.Box;

    public IEditorTool CurrentTool => _tools[_currentToolKind];

    public void SetBrushPrimitive(BrushPrimitive primitive) => SelectedBrushPrimitive = primitive;

    public void SetTool(EditorToolKind toolKind)
    {
        if (_currentToolKind == toolKind)
        {
            return;
        }

        _tools[_currentToolKind].Cancel();
        _currentToolKind = toolKind;
        ToolChanged?.Invoke(this, toolKind);
    }

    public void SetTool(string toolName)
    {
        if (!Enum.TryParse<EditorToolKind>(toolName, ignoreCase: true, out var toolKind))
        {
            toolKind = toolName.Equals("CreateBrush", StringComparison.OrdinalIgnoreCase)
                ? EditorToolKind.CreateBrush
                : EditorToolKind.Select;
        }

        SetTool(toolKind);
    }

    public string GetDisplayName() => CurrentTool.DisplayName;
}
