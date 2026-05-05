namespace MermaidStudio.Application.Editing;

public sealed class InspectorState
{
    public bool ClearSelectionRequested { get; set; }

    public bool NodeSectionEnabled { get; set; }
    public string NodeLabel { get; set; } = string.Empty;
    public int NodeStyleIndex { get; set; }

    public bool EdgeSectionEnabled { get; set; }
    public string EdgeLabel { get; set; } = string.Empty;
    public int EdgeStyleIndex { get; set; }
    public int EdgeDirectionIndex { get; set; }
}
