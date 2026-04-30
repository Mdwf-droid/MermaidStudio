namespace MermaidStudio.Domain.Core;

public readonly record struct EntityId(string Value)
{
    public static EntityId New() => new(Guid.NewGuid().ToString("N"));
    public override string ToString() => Value;
}
