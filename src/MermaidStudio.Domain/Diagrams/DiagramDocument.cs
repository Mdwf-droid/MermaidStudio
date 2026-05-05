using MermaidStudio.Domain.Core;
using MermaidStudio.Domain.Edges;
using MermaidStudio.Domain.Nodes;
using MermaidStudio.Domain.Styles;

namespace MermaidStudio.Domain.Diagrams;

public sealed class DiagramDocument
{
    public EntityId Id { get; init; } = EntityId.New();

    public DiagramKind Kind { get; set; } = DiagramKind.Flowchart;

    // ✅ R2.A : direction réelle du diagramme courant
    public FlowDirection Direction { get; set; } = FlowDirection.LR;

    public IList<Node> Nodes { get; } = new List<Node>();
    public IList<Edge> Edges { get; } = new List<Edge>();

    public StyleSet Styles { get; } = new();
}
