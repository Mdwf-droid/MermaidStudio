using MermaidStudio.Domain.Core;

namespace MermaidStudio.Domain.Nodes;

public sealed class Port
{
    public EntityId Id { get; init; } = EntityId.New();
    public string Name { get; set; } = "Port";
    public PortDirection Direction { get; set; } = PortDirection.Bidirectional;
}
