using MermaidStudio.Domain.Core;

namespace MermaidStudio.Domain.Nodes;

public sealed class Node
{
    public EntityId Id { get; init; } = EntityId.New();
    public string Label { get; set; } = "Node";
    public NodeType Type { get; set; } = NodeType.Generic;

    // coordonnées de travail UI (pas des coordonnées Mermaid)
    public double X { get; set; }
    public double Y { get; set; }

    public IList<Port> Ports { get; } = new List<Port>();
}
