using MermaidStudio.Domain.Core;

namespace MermaidStudio.Domain.Edges;

public sealed class Edge
{
    public EntityId Id { get; init; } = EntityId.New();

    // Anciennes propriétés conservées pour compatibilité
    public EntityId SourcePortId { get; set; }
    public EntityId TargetPortId { get; set; }

    // ✅ R2.A : vraies extrémités documentaires du flowchart courant
    public EntityId SourceNodeId { get; set; }
    public EntityId TargetNodeId { get; set; }

    public string? Label { get; set; }

    public EdgeKind Kind { get; set; } = EdgeKind.Default;

    // ✅ R2.A : sens logique réel de l’edge dans le document
    public DocumentEdgeDirection Direction { get; set; } = DocumentEdgeDirection.Forward;
}
