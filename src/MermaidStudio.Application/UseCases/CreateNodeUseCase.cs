using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.Nodes;

namespace MermaidStudio.Application.UseCases;

public sealed class CreateNodeUseCase
{
    public Node Execute(DiagramDocument document, string label, double x, double y)
    {
        var node = new Node { Label = label, X = x, Y = y };
        node.Ports.Add(new Port { Name = "Right", Direction = PortDirection.Out });
        document.Nodes.Add(node);
        return node;
    }
}
