using MermaidStudio.Domain.Core;

namespace MermaidStudio.Domain.Edges;

public sealed class Edge
{
    public EntityId Id { get; init; } = EntityId.New();
    public EntityId SourcePortId { get; set; }
    public EntityId TargetPortId { get; set; }
    public string? Label { get; set; }
    public EdgeKind Kind { get; set; } = EdgeKind.Default;
}
