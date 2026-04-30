using System.Text;
using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.Edges;
using MermaidStudio.Domain.Mermaid;
using MermaidStudio.Domain.Nodes;

namespace MermaidStudio.Domain.Mermaid.Flowchart;

public sealed class FlowchartExporter : IMermaidExporter
{
    public string Export(DiagramDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");

        foreach (var node in document.Nodes)
        {
            sb.AppendLine($"    {node.Id}[\"{Escape(node.Label)}\"]");
        }

        foreach (var edge in document.Edges)
        {
            var sourceNode = document.Nodes.FirstOrDefault(n => n.Ports.Any(p => p.Id == edge.SourcePortId));
            var targetNode = document.Nodes.FirstOrDefault(n => n.Ports.Any(p => p.Id == edge.TargetPortId));
            if (sourceNode is null || targetNode is null)
                continue;

            var arrow = edge.Kind switch
            {
                EdgeKind.Dashed => "-.->",
                EdgeKind.Dotted => "-..->",
                _ => "-->"
            };

            if (string.IsNullOrWhiteSpace(edge.Label))
                sb.AppendLine($"    {sourceNode.Id} {arrow} {targetNode.Id}");
            else
                sb.AppendLine($"    {sourceNode.Id} {arrow}|{Escape(edge.Label!)}| {targetNode.Id}");
        }

        return sb.ToString();
    }

    private static string Escape(string value) => value.Replace("\"", "\"");
}
