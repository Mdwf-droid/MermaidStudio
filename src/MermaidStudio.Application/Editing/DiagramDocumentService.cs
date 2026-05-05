using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.Edges;
using MermaidStudio.Domain.Nodes;

namespace MermaidStudio.Application.Editing;

public sealed class DiagramDocumentService
{
    public DiagramDocument CurrentDocument { get; } = new()
    {
        Kind = DiagramKind.Flowchart,
        Direction = FlowDirection.LR
    };

    /// <summary>
    /// R2.A :
    /// Le document courant est maintenu en parallèle de l’UI.
    /// On synchronise ici l’état documentaire à partir de l’état courant de l’éditeur.
    /// </summary>
    public void Synchronize(
        FlowDirection direction,
        IEnumerable<Node> nodes,
        IEnumerable<DiagramDocumentEdgeState> edges)
    {
        CurrentDocument.Kind = DiagramKind.Flowchart;
        CurrentDocument.Direction = direction;

        CurrentDocument.Nodes.Clear();
        foreach (var node in nodes)
        {
            CurrentDocument.Nodes.Add(node);
        }

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
    }

    public void LoadDocument(DiagramDocument document)
    {
        if (document.Kind != DiagramKind.Flowchart)
            throw new InvalidOperationException("Seuls les documents Flowchart sont supportés dans S18.");

        CurrentDocument.Kind = document.Kind;
        CurrentDocument.Direction = document.Direction;

        CurrentDocument.Nodes.Clear();
        foreach (var node in document.Nodes)
        {
            CurrentDocument.Nodes.Add(node);
        }

        CurrentDocument.Edges.Clear();
        foreach (var edge in document.Edges)
        {
            CurrentDocument.Edges.Add(edge);
        }
    }
}
