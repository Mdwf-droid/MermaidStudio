namespace MermaidStudio.Application.Export;

public enum FlowchartExportDiagramDirection
{
    LR,
    TB,
    RL,
    BT
}

public sealed class FlowchartExportModel
{
    public FlowchartExportDiagramDirection Direction { get; set; } = FlowchartExportDiagramDirection.LR;

    public IList<FlowchartExportNode> Nodes { get; } = new List<FlowchartExportNode>();
    public IList<FlowchartExportEdge> Edges { get; } = new List<FlowchartExportEdge>();
}