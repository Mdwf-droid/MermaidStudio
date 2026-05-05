namespace MermaidStudio.Application.Export;

public enum FlowchartExportEdgeStyle
{
    Default,
    Dashed,
    Thick
}

public enum FlowchartExportEdgeDirection
{
    Forward,
    Reverse
}

public sealed class FlowchartExportEdge
{
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public FlowchartExportEdgeStyle Style { get; set; } = FlowchartExportEdgeStyle.Default;
    public FlowchartExportEdgeDirection Direction { get; set; } = FlowchartExportEdgeDirection.Forward;
}
