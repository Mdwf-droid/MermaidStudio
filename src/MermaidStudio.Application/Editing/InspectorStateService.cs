namespace MermaidStudio.Application.Editing;

public sealed class InspectorStateService
{
    private readonly SelectionService _selectionService;

    public InspectorStateService(SelectionService selectionService)
    {
        _selectionService = selectionService;
    }

    public InspectorState BuildState<TNodeControl, TNodeModel, TEdgeControl>(
        Func<TNodeControl, bool> isNodeActive,
        Func<TNodeControl, TNodeModel?> nodeModelSelector,
        Func<TNodeModel, string> nodeLabelSelector,
        Func<TNodeModel, int> nodeStyleIndexSelector,
        Func<TEdgeControl, bool> isEdgeActive,
        Func<TEdgeControl, string> edgeLabelSelector,
        Func<TEdgeControl, int> edgeStyleIndexSelector,
        Func<TEdgeControl, int> edgeDirectionIndexSelector)
        where TNodeControl : class
        where TNodeModel : class
        where TEdgeControl : class
    {
        if (_selectionService.Kind == SelectionKind.Node)
        {
            var selectedNode = _selectionService.GetSelected<TNodeControl>();

            if (selectedNode == null)
            {
                return CreateEmpty(clearSelectionRequested: true);
            }

            if (!isNodeActive(selectedNode))
            {
                return CreateEmpty(clearSelectionRequested: true);
            }

            var model = nodeModelSelector(selectedNode);
            if (model == null)
            {
                return CreateEmpty(clearSelectionRequested: true);
            }

            return new InspectorState
            {
                NodeSectionEnabled = true,
                NodeLabel = nodeLabelSelector(model),
                NodeStyleIndex = nodeStyleIndexSelector(model),

                EdgeSectionEnabled = false,
                EdgeLabel = string.Empty,
                EdgeStyleIndex = 0,
                EdgeDirectionIndex = 0
            };
        }

        if (_selectionService.Kind == SelectionKind.Edge)
        {
            var selectedEdge = _selectionService.GetSelected<TEdgeControl>();

            if (selectedEdge == null)
            {
                return CreateEmpty(clearSelectionRequested: true);
            }

            if (!isEdgeActive(selectedEdge))
            {
                return CreateEmpty(clearSelectionRequested: true);
            }

            return new InspectorState
            {
                NodeSectionEnabled = false,
                NodeLabel = string.Empty,
                NodeStyleIndex = 0,

                EdgeSectionEnabled = true,
                EdgeLabel = edgeLabelSelector(selectedEdge),
                EdgeStyleIndex = edgeStyleIndexSelector(selectedEdge),
                EdgeDirectionIndex = edgeDirectionIndexSelector(selectedEdge)
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
