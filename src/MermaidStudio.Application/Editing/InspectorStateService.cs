using MermaidStudio.Domain.Edges;
using MermaidStudio.Domain.Nodes;

namespace MermaidStudio.Application.Editing;

public sealed class InspectorStateService
{
    private readonly SelectionService _selectionService;
    private readonly DiagramDocumentService _documentService;

    public InspectorStateService(
        SelectionService selectionService,
        DiagramDocumentService documentService)
    {
        _selectionService = selectionService;
        _documentService = documentService;
    }

    public InspectorState BuildState<TNodeControl, TEdgeControl>(
        Func<TNodeControl, bool> isNodeActive,
        Func<TNodeControl, string?> resolveNodeId,
        Func<TEdgeControl, bool> isEdgeActive,
        Func<TEdgeControl, (string SourceNodeId, string TargetNodeId)?> resolveEdgeEndpoints)
        where TNodeControl : class
        where TEdgeControl : class
    {
        var document = _documentService.CurrentDocument;

        if (_selectionService.Kind == SelectionKind.Node)
        {
            var selectedNodeControl = _selectionService.GetSelected<TNodeControl>();

            if (selectedNodeControl == null)
                return CreateEmpty(clearSelectionRequested: true);

            if (!isNodeActive(selectedNodeControl))
                return CreateEmpty(clearSelectionRequested: true);

            var nodeId = resolveNodeId(selectedNodeControl);
            if (string.IsNullOrWhiteSpace(nodeId))
                return CreateEmpty(clearSelectionRequested: true);

            var node = document.Nodes.FirstOrDefault(n => n.Id.Value == nodeId);
            if (node == null)
                return CreateEmpty(clearSelectionRequested: true);

            return new InspectorState
            {
                NodeSectionEnabled = true,
                NodeLabel = node.Label,
                NodeStyleIndex = node.VisualStyle switch
                {
                    NodeVisualStyle.Rectangle => 0,
                    NodeVisualStyle.Rounded => 1,
                    NodeVisualStyle.Decision => 2,
                    NodeVisualStyle.Circle => 3,
                    _ => 0
                },

                EdgeSectionEnabled = false,
                EdgeLabel = string.Empty,
                EdgeStyleIndex = 0,
                EdgeDirectionIndex = 0
            };
        }

        if (_selectionService.Kind == SelectionKind.Edge)
        {
            var selectedEdgeControl = _selectionService.GetSelected<TEdgeControl>();

            if (selectedEdgeControl == null)
                return CreateEmpty(clearSelectionRequested: true);

            if (!isEdgeActive(selectedEdgeControl))
                return CreateEmpty(clearSelectionRequested: true);

            var endpoints = resolveEdgeEndpoints(selectedEdgeControl);
            if (endpoints == null)
                return CreateEmpty(clearSelectionRequested: true);

            var edge = document.Edges.FirstOrDefault(e =>
                e.SourceNodeId.Value == endpoints.Value.SourceNodeId &&
                e.TargetNodeId.Value == endpoints.Value.TargetNodeId);

            if (edge == null)
                return CreateEmpty(clearSelectionRequested: true);

            return new InspectorState
            {
                NodeSectionEnabled = false,
                NodeLabel = string.Empty,
                NodeStyleIndex = 0,

                EdgeSectionEnabled = true,
                EdgeLabel = edge.Label ?? string.Empty,
                EdgeStyleIndex = edge.Kind switch
                {
                    EdgeKind.Dashed => 1,
                    EdgeKind.Thick => 2,
                    _ => 0
                },
                EdgeDirectionIndex = edge.Direction switch
                {
                    DocumentEdgeDirection.Reverse => 1,
                    _ => 0
                }
            };
        }

        return CreateEmpty(clearSelectionRequested: false);
    }

    private static InspectorState CreateEmpty(bool clearSelectionRequested)
    {
        return new InspectorState
        {
            ClearSelectionRequested = clearSelectionRequested,

            NodeSectionEnabled = false,
            NodeLabel = string.Empty,
            NodeStyleIndex = 0,

            EdgeSectionEnabled = false,
            EdgeLabel = string.Empty,
            EdgeStyleIndex = 0,
            EdgeDirectionIndex = 0
        };
    }
}
