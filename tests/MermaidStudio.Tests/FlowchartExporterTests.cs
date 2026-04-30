using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.Edges;
using MermaidStudio.Domain.Mermaid.Flowchart;
using MermaidStudio.Domain.Nodes;
using Xunit;

namespace MermaidStudio.Tests;

public sealed class FlowchartExporterTests
{
    [Fact]
    public void Export_IncludesNodesAndEdges()
    {
        var doc = new DiagramDocument { Kind = DiagramKind.Flowchart };

        var nodeA = new Node { Label = "A" };
        var portA = new Port { Name = "Out", Direction = PortDirection.Out };
        nodeA.Ports.Add(portA);

        var nodeB = new Node { Label = "B" };
        var portB = new Port { Name = "In", Direction = PortDirection.In };
        nodeB.Ports.Add(portB);

        doc.Nodes.Add(nodeA);
        doc.Nodes.Add(nodeB);
        doc.Edges.Add(new Edge { SourcePortId = portA.Id, TargetPortId = portB.Id });

        var exporter = new FlowchartExporter();
        var text = exporter.Export(doc);

        Assert.Contains("flowchart LR", text);
        Assert.Contains("A", text);
        Assert.Contains("B", text);
        Assert.Contains("-->", text);
    }
}
