namespace MermaidStudio.Application.Export;

public enum FlowchartExportNodeStyle
{
    Rectangle,
    Rounded,
    Decision,
    Circle
}

public sealed class FlowchartExportNode
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = "Node";
    public FlowchartExportNodeStyle Style { get; set; } = FlowchartExportNodeStyle.Rectangle;
}
