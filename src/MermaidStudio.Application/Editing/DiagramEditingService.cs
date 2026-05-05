namespace MermaidStudio.Application.Editing;

public sealed class DiagramEditingService
{
    private readonly SelectionService _selectionService;

    public DiagramEditingService(SelectionService selectionService)
    {
        _selectionService = selectionService;
    }

    public TNodeControl CreateNode<TModel, TNodeControl>(
        double x,
        double y,
        Func<double, double, TModel> modelFactory,
        Func<TModel, TNodeControl> controlFactory,
        Action<TNodeControl> initializeControl,
        Action<TNodeControl> persist)
        where TNodeControl : class
        where TModel : class
    {
        var model = modelFactory(x, y);
        var control = controlFactory(model);

        initializeControl(control);
        persist(control);

        return control;
    }

    public TEdgeControl? CreateEdge<TNodeControl, TEdgeControl>(
        TNodeControl source,
        TNodeControl? target,
        Func<TNodeControl, TNodeControl, bool> edgeAlreadyExists,
        Func<TNodeControl, TNodeControl, TEdgeControl> edgeFactory,
        Action<TEdgeControl> initializeEdge,
        Action<TEdgeControl> persist)
        where TNodeControl : class
        where TEdgeControl : class
    {
        if (target == null)
            return null;

        if (edgeAlreadyExists(source, target))
            return null;

        var edge = edgeFactory(source, target);

        initializeEdge(edge);
        persist(edge);

        return edge;
    }

    public bool DeleteSelectedNode<TNodeControl>(
        Action<TNodeControl> beforeDelete,
        Action<TNodeControl> executeDelete)
        where TNodeControl : class
    {
        if (_selectionService.Kind != SelectionKind.Node)
            return false;

        var node = _selectionService.GetSelected<TNodeControl>();
        if (node == null)
            return false;

        beforeDelete(node);
        _selectionService.ClearSelection();
        executeDelete(node);

        return true;
    }

    public bool DeleteSelectedEdge<TEdgeControl>(
        Action<TEdgeControl> beforeDelete,
        Action<TEdgeControl> executeDelete)
        where TEdgeControl : class
    {
        if (_selectionService.Kind != SelectionKind.Edge)
            return false;

        var edge = _selectionService.GetSelected<TEdgeControl>();
        if (edge == null)
            return false;

        beforeDelete(edge);
        _selectionService.ClearSelection();
        executeDelete(edge);

        return true;
    }

    public bool UpdateSelectedNodeLabel<TNodeControl, TModel>(
        string? rawText,
        Func<TNodeControl, TModel?> modelSelector,
        Func<TModel, string> currentLabelSelector,
        Action<TModel, string> executeUpdate)
        where TNodeControl : class
        where TModel : class
    {
        if (_selectionService.Kind != SelectionKind.Node)
            return false;

        var nodeControl = _selectionService.GetSelected<TNodeControl>();
        if (nodeControl == null)
            return false;

        var model = modelSelector(nodeControl);
        if (model == null)
            return false;

        var newLabel = string.IsNullOrWhiteSpace(rawText?.Trim())
            ? "Node"
            : rawText!.Trim();

        if (currentLabelSelector(model) == newLabel)
            return false;

        executeUpdate(model, newLabel);
        return true;
    }

    public bool UpdateSelectedNodeStyle<TNodeControl, TModel, TStyle>(
        TStyle style,
        Func<TNodeControl, TModel?> modelSelector,
        Action<TModel, TStyle> applyStyle)
        where TNodeControl : class
        where TModel : class
    {
        if (_selectionService.Kind != SelectionKind.Node)
            return false;

        var nodeControl = _selectionService.GetSelected<TNodeControl>();
        if (nodeControl == null)
            return false;

        var model = modelSelector(nodeControl);
        if (model == null)
            return false;

        applyStyle(model, style);
        return true;
    }

    public bool UpdateSelectedEdgeLabel<TEdgeControl>(
        string? rawText,
        Action<TEdgeControl, string> applyLabel)
        where TEdgeControl : class
    {
        if (_selectionService.Kind != SelectionKind.Edge)
            return false;

        var edge = _selectionService.GetSelected<TEdgeControl>();
        if (edge == null)
            return false;

        var newLabel = rawText?.Trim() ?? string.Empty;
        applyLabel(edge, newLabel);

        return true;
    }

    public bool UpdateSelectedEdgeStyle<TEdgeControl, TStyle, TDirection>(
        TStyle style,
        TDirection direction,
        Action<TEdgeControl, TStyle, TDirection> applyStyleAndDirection)
        where TEdgeControl : class
    {
        if (_selectionService.Kind != SelectionKind.Edge)
            return false;

        var edge = _selectionService.GetSelected<TEdgeControl>();
        if (edge == null)
            return false;

        applyStyleAndDirection(edge, style, direction);
        return true;
    }
}
