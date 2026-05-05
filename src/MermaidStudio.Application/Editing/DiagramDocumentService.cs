using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.Edges;
using MermaidStudio.Domain.Nodes;
using MermaidStudio.Domain.States;

namespace MermaidStudio.Application.Editing;

public sealed class DiagramDocumentService
{
    public DiagramDocument CurrentDocument { get; } = new()
    {
        Kind = DiagramKind.Flowchart,
        Direction = FlowDirection.LR
    };

    public void Synchronize(
        FlowDirection direction,
        IEnumerable<Node> nodes,
        IEnumerable<DiagramDocumentEdgeState> edges)
    {
        CurrentDocument.Kind = DiagramKind.Flowchart;
        CurrentDocument.Direction = direction;

        CurrentDocument.Nodes.Clear();
        foreach (var node in nodes)
            CurrentDocument.Nodes.Add(node);

        CurrentDocument.Edges.Clear();
        foreach (var edgeState in edges)
        {
            CurrentDocument.Edges.Add(new Edge
            {
                SourceNodeId = edgeState.SourceNodeId,
                TargetNodeId = edgeState.TargetNodeId,
                Label = edgeState.Label,
                Kind = edgeState.Kind,
                Direction = edgeState.Direction
            });
        }

        CurrentDocument.StateNodes.Clear();
        CurrentDocument.StateTransitions.Clear();
    }

    public void ResetToKind(DiagramKind kind)
    {
        CurrentDocument.Kind = kind;
        CurrentDocument.Direction = FlowDirection.LR;

        CurrentDocument.Nodes.Clear();
        CurrentDocument.Edges.Clear();
        CurrentDocument.StateNodes.Clear();
        CurrentDocument.StateTransitions.Clear();
    }

    public void LoadDocument(DiagramDocument document)
    {
        CurrentDocument.Kind = document.Kind;
        CurrentDocument.Direction = document.Direction;

        CurrentDocument.Nodes.Clear();
        foreach (var node in document.Nodes)
            CurrentDocument.Nodes.Add(node);

        CurrentDocument.Edges.Clear();
        foreach (var edge in document.Edges)
            CurrentDocument.Edges.Add(edge);

        CurrentDocument.StateNodes.Clear();
        foreach (var stateNode in document.StateNodes)
            CurrentDocument.StateNodes.Add(stateNode);

        CurrentDocument.StateTransitions.Clear();
        foreach (var transition in document.StateTransitions)
            CurrentDocument.StateTransitions.Add(transition);
    }
}
