using System.Text;

namespace MermaidStudio.Application.Export;

public sealed class FlowchartExportService
{
    public string Export(FlowchartExportModel model)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"flowchart {FormatDirection(model.Direction)}");

        foreach (var node in model.Nodes
                     .OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            sb.AppendLine($"    {FormatNode(node)}");
        }

        foreach (var edge in model.Edges
                     .OrderBy(e => GetEffectiveSourceId(e), StringComparer.Ordinal)
                     .ThenBy(e => GetEffectiveTargetId(e), StringComparer.Ordinal))
        {
            var sourceId = GetEffectiveSourceId(edge);
            var targetId = GetEffectiveTargetId(edge);
            var arrowToken = FormatEdgeToken(edge.Style);

            if (string.IsNullOrWhiteSpace(edge.Label))
            {
                sb.AppendLine($"    {sourceId} {arrowToken} {targetId}");
            }
            else
            {
                sb.AppendLine($"    {sourceId} {arrowToken}|{Escape(edge.Label)}| {targetId}");
            }
        }

        return sb.ToString();
    }

    private static string FormatDirection(FlowchartExportDiagramDirection direction)
    {
        return direction switch
        {
            FlowchartExportDiagramDirection.TB => "TB",
            FlowchartExportDiagramDirection.RL => "RL",
            FlowchartExportDiagramDirection.BT => "BT",
            _ => "LR"
        };
    }

    private static string FormatNode(FlowchartExportNode node)
    {
        var id = node.Id;
        var label = Escape(node.Label);

        return node.Style switch
        {
            FlowchartExportNodeStyle.Rounded => $"{id}(\"{label}\")",
            FlowchartExportNodeStyle.Decision => $"{id}{{\"{label}\"}}",
            FlowchartExportNodeStyle.Circle => $"{id}((\"{label}\"))",
            _ => $"{id}[\"{label}\"]"
        };
    }

    private static string FormatEdgeToken(FlowchartExportEdgeStyle style)
    {
        return style switch
        {
            FlowchartExportEdgeStyle.Dashed => "-.->",
            FlowchartExportEdgeStyle.Thick => "==>",
            _ => "-->"
        };
    }

    private static string GetEffectiveSourceId(FlowchartExportEdge edge)
    {
        return edge.Direction == FlowchartExportEdgeDirection.Forward
            ? edge.SourceId
            : edge.TargetId;
    }

    private static string GetEffectiveTargetId(FlowchartExportEdge edge)
    {
        return edge.Direction == FlowchartExportEdgeDirection.Forward
            ? edge.TargetId
            : edge.SourceId;
    }

    private static string Escape(string value)
        => value.Replace("\"", "\\\"");
}
