using MermaidStudio.Domain.Core;
using MermaidStudio.Domain.Edges;

namespace MermaidStudio.Application.Editing;

public sealed class DiagramDocumentEdgeState
{
    public EntityId SourceNodeId { get; set; }
    public EntityId TargetNodeId { get; set; }

    public string Label { get; set; } = string.Empty;

    public EdgeKind Kind { get; set; } = EdgeKind.Default;

    public DocumentEdgeDirection Direction { get; set; } = DocumentEdgeDirection.Forward;
}
