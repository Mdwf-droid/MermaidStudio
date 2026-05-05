using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using MermaidStudio.Application.Editing;
using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.Edges;
using MermaidStudio.Domain.Nodes;
using MermaidStudio.Domain.States;
using MermaidStudio.UI.Avalonia.Editing;
using DocumentFlowDirection = MermaidStudio.Domain.Diagrams.FlowDirection;

namespace MermaidStudio.UI.Avalonia.Controls;

public partial class DiagramWorkspaceControl : UserControl
{
    private SelectionService? _selectionService;
    private DiagramEditingService? _diagramEditingService;
    private DiagramDocumentService? _documentService;
    private CommandHistory? _history;

    private NodeControl? _previewSource;
    private StateNodeControl? _previewStateSource;
    private Line? _previewLine;

    private readonly List<EdgeControl> _edges = new();
    private readonly List<StateTransitionControl> _stateTransitions = new();

    private DiagramFlowDirection _currentDiagramFlowDirection = DiagramFlowDirection.LR;

    public event EventHandler? WorkspaceStateChanged;

    public DiagramWorkspaceControl()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void Configure(
        SelectionService selectionService,
        DiagramEditingService diagramEditingService,
        DiagramDocumentService documentService,
        CommandHistory history)
    {
        _selectionService = selectionService;
        _diagramEditingService = diagramEditingService;
        _documentService = documentService;
        _history = history;
    }

    public DiagramKind CurrentDiagramKind => _documentService?.CurrentDocument.Kind ?? DiagramKind.Flowchart;

    public Canvas GetCanvas()
        => this.FindControl<Canvas>("EditorCanvas")
           ?? throw new InvalidOperationException("EditorCanvas introuvable dans DiagramWorkspaceControl.");

    public void FocusWorkspace()
    {
        Focus();
    }

    public void LoadCurrentDocument(DiagramFlowDirection flowDirection)
    {
        _currentDiagramFlowDirection = flowDirection;
        RebuildFromCurrentDocument();
    }

    public void SetFlowDirection(DiagramFlowDirection flowDirection, bool syncDocument)
    {
        _currentDiagramFlowDirection = flowDirection;

        foreach (var edge in _edges)
            edge.DiagramDirection = _currentDiagramFlowDirection;

        if (syncDocument)
            SyncFlowchartDocument();
    }

    public void CreateStateNode(StateNodeKind kind, double x, double y)
    {
        EnsureConfigured();

        var canvas = GetCanvas();

        var model = new StateNode
        {
            Label = kind == StateNodeKind.Normal ? "State" : string.Empty,
            X = x,
            Y = y,
            Kind = kind
        };

        _documentService!.CurrentDocument.Kind = DiagramKind.StateDiagram;
        _documentService.CurrentDocument.StateNodes.Add(model);

        var control = BuildStateNodeControl(model);
        Canvas.SetLeft(control, model.X);
        Canvas.SetTop(control, model.Y);

        canvas.Children.Add(control);
        RaiseWorkspaceChanged();
    }

    public void DeleteSelectedNodeOrState()
    {
        EnsureConfigured();

        var canvas = GetCanvas();

        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            var selectedNode = GetSelectedFlowNode();
            if (selectedNode == null)
                return;

            _diagramEditingService!.DeleteSelectedNode<NodeControl>(
                beforeDelete: node => node.SetSelected(false),
                executeDelete: node => _history!.Execute(new DeleteNodeCommand(canvas, node, _edges))
            );

            SyncFlowchartDocument();
            RaiseWorkspaceChanged();
            return;
        }

        var selectedStateNode = GetSelectedStateNode();
        if (selectedStateNode?.DataContext is not StateNode stateNode)
            return;

        selectedStateNode.SetSelected(false);
        _selectionService!.ClearSelection();

        var relatedTransitions = _stateTransitions
            .Where(t => t.Model.SourceStateId.Equals(stateNode.Id) || t.Model.TargetStateId.Equals(stateNode.Id))
            .ToList();

        foreach (var transition in relatedTransitions)
        {
            canvas.Children.Remove(transition);
            _stateTransitions.Remove(transition);
            _documentService!.CurrentDocument.StateTransitions.Remove(transition.Model);
        }

        canvas.Children.Remove(selectedStateNode);
        _documentService!.CurrentDocument.StateNodes.Remove(stateNode);

        RaiseWorkspaceChanged();
    }

    public void DeleteSelectedEdgeOrTransition()
    {
        EnsureConfigured();

        var canvas = GetCanvas();

        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            var selectedEdge = GetSelectedFlowEdge();
            if (selectedEdge == null)
                return;

            _diagramEditingService!.DeleteSelectedEdge<EdgeControl>(
                beforeDelete: edge => edge.SetSelected(false),
                executeDelete: edge => _history!.Execute(new DeleteEdgeCommand(canvas, _edges, edge))
            );

            SyncFlowchartDocument();
            RaiseWorkspaceChanged();
            return;
        }

        var selectedTransition = GetSelectedStateTransition();
        if (selectedTransition == null)
            return;

        selectedTransition.SetSelected(false);
        _selectionService!.ClearSelection();

        canvas.Children.Remove(selectedTransition);
        _stateTransitions.Remove(selectedTransition);
        _documentService!.CurrentDocument.StateTransitions.Remove(selectedTransition.Model);

        RaiseWorkspaceChanged();
    }

    public void ApplySelectedNodeLabel(string? rawText)
    {
        EnsureConfigured();

        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            _diagramEditingService!.UpdateSelectedNodeLabel<NodeControl, Node>(
                rawText,
                control => control.DataContext as Node,
                node => node.Label,
                (node, newLabel) => _history!.Execute(new UpdateNodeLabelCommand(node, node.Label, newLabel))
            );

            SyncFlowchartDocument();
            RaiseWorkspaceChanged();
            return;
        }

        var selectedStateNode = GetSelectedStateNode();
        if (selectedStateNode?.DataContext is not StateNode stateNode)
            return;

        if (stateNode.Kind != StateNodeKind.Normal)
            return;

        stateNode.Label = string.IsNullOrWhiteSpace(rawText?.Trim())
            ? "State"
            : rawText!.Trim();

        RaiseWorkspaceChanged();
    }

    public void ApplySelectedNodeStyle(int selectedIndex)
    {
        EnsureConfigured();

        if (CurrentDiagramKind != DiagramKind.Flowchart)
            return;

        var selectedStyle = selectedIndex switch
        {
            1 => NodeVisualStyle.Rounded,
            2 => NodeVisualStyle.Decision,
            3 => NodeVisualStyle.Circle,
            _ => NodeVisualStyle.Rectangle
        };

        _diagramEditingService!.UpdateSelectedNodeStyle<NodeControl, Node, NodeVisualStyle>(
            selectedStyle,
            control => control.DataContext as Node,
            (node, style) => node.VisualStyle = style
        );

        SyncFlowchartDocument();
        RaiseWorkspaceChanged();
    }

    public void ApplySelectedEdgeLabel(string? rawText)
    {
        EnsureConfigured();

        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            _diagramEditingService!.UpdateSelectedEdgeLabel<EdgeControl>(
                rawText,
                (edge, newLabel) => edge.Label = newLabel
            );

            SyncFlowchartDocument();
            RaiseWorkspaceChanged();
            return;
        }

        var selectedTransition = GetSelectedStateTransition();
        if (selectedTransition == null)
            return;

        selectedTransition.Label = rawText?.Trim() ?? string.Empty;
        RaiseWorkspaceChanged();
    }

    public void ApplySelectedEdgeStyle(int styleIndex, int directionIndex)
    {
        EnsureConfigured();

        if (CurrentDiagramKind != DiagramKind.Flowchart)
            return;

        var style = styleIndex switch
        {
            1 => EdgeStyleKind.Dashed,
            2 => EdgeStyleKind.Thick,
            _ => EdgeStyleKind.Default
        };

        var direction = directionIndex switch
        {
            1 => EdgeDirection.Reverse,
            _ => EdgeDirection.Forward
        };

        _diagramEditingService!.UpdateSelectedEdgeStyle<EdgeControl, EdgeStyleKind, EdgeDirection>(
            style,
            direction,
            (edge, styleValue, directionValue) =>
            {
                edge.StyleKind = styleValue;
                edge.Direction = directionValue;
            });

        SyncFlowchartDocument();
        RaiseWorkspaceChanged();
    }

    public NodeControl? GetSelectedFlowNode()
        => _selectionService?.Kind == SelectionKind.Node
            ? _selectionService.GetSelected<NodeControl>()
            : null;

    public EdgeControl? GetSelectedFlowEdge()
        => _selectionService?.Kind == SelectionKind.Edge
            ? _selectionService.GetSelected<EdgeControl>()
            : null;

    public StateNodeControl? GetSelectedStateNode()
        => _selectionService?.Kind == SelectionKind.Node
            ? _selectionService.GetSelected<StateNodeControl>()
            : null;

    public StateTransitionControl? GetSelectedStateTransition()
        => _selectionService?.Kind == SelectionKind.Edge
            ? _selectionService.GetSelected<StateTransitionControl>()
            : null;

    public void ClearSelection()
    {
        ClearSelectionVisualOnly();
        _selectionService?.ClearSelection();
        RaiseWorkspaceChanged();
    }

    public void ClearSelectionVisualOnly()
    {
        if (GetSelectedFlowNode() is { } flowNode)
            flowNode.SetSelected(false);

        if (GetSelectedFlowEdge() is { } flowEdge)
            flowEdge.SetSelected(false);

        if (GetSelectedStateNode() is { } stateNode)
            stateNode.SetSelected(false);

        if (GetSelectedStateTransition() is { } stateTransition)
            stateTransition.SetSelected(false);
    }

    public void HandleWindowKeyDown(KeyEventArgs e)
    {
        if (e.Handled)
            return;

        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _history?.Undo();
            if (CurrentDiagramKind == DiagramKind.Flowchart)
                SyncFlowchartDocument();

            RaiseWorkspaceChanged();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _history?.Redo();
            if (CurrentDiagramKind == DiagramKind.Flowchart)
                SyncFlowchartDocument();

            RaiseWorkspaceChanged();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            if (GetSelectedFlowEdge() != null || GetSelectedStateTransition() != null)
            {
                DeleteSelectedEdgeOrTransition();
                e.Handled = true;
                return;
            }

            DeleteSelectedNodeOrState();
            e.Handled = true;
        }
    }

    public void SyncFlowchartDocument()
    {
        EnsureConfigured();

        if (CurrentDiagramKind != DiagramKind.Flowchart)
            return;

        var canvas = GetCanvas();

        var nodes = canvas.Children
            .OfType<NodeControl>()
            .Select(n => n.DataContext as Node)
            .Where(n => n != null)
            .Cast<Node>()
            .ToList();

        var edgeStates = _edges
            .Select(edge =>
            {
                var sourceNode = edge.SourceNode.DataContext as Node;
                var targetNode = edge.TargetNode.DataContext as Node;

                if (sourceNode == null || targetNode == null)
                    return null;

                return new DiagramDocumentEdgeState
                {
                    SourceNodeId = sourceNode.Id,
                    TargetNodeId = targetNode.Id,
                    Label = edge.Label,
                    Kind = edge.StyleKind switch
                    {
                        EdgeStyleKind.Dashed => EdgeKind.Dashed,
                        EdgeStyleKind.Thick => EdgeKind.Thick,
                        _ => EdgeKind.Default
                    },
                    Direction = edge.Direction == EdgeDirection.Reverse
                        ? DocumentEdgeDirection.Reverse
                        : DocumentEdgeDirection.Forward
                };
            })
            .Where(e => e != null)
            .Cast<DiagramDocumentEdgeState>()
            .ToList();

        var direction = _currentDiagramFlowDirection switch
        {
            DiagramFlowDirection.TB => DocumentFlowDirection.TB,
            DiagramFlowDirection.RL => DocumentFlowDirection.RL,
            DiagramFlowDirection.BT => DocumentFlowDirection.BT,
            _ => DocumentFlowDirection.LR
        };

        _documentService!.Synchronize(direction, nodes, edgeStates);
    }

    private void RebuildFromCurrentDocument()
    {
        EnsureConfigured();

        var canvas = GetCanvas();

        if (_previewLine != null)
        {
            canvas.Children.Remove(_previewLine);
            _previewLine = null;
        }

        _previewSource = null;
        _previewStateSource = null;

        foreach (var node in canvas.Children.OfType<NodeControl>().ToList())
            canvas.Children.Remove(node);

        foreach (var edge in _edges.ToList())
            canvas.Children.Remove(edge);
        _edges.Clear();

        foreach (var stateNode in canvas.Children.OfType<StateNodeControl>().ToList())
            canvas.Children.Remove(stateNode);

        foreach (var transition in _stateTransitions.ToList())
            canvas.Children.Remove(transition);
        _stateTransitions.Clear();

        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            var nodeMap = new Dictionary<string, NodeControl>(StringComparer.Ordinal);

            foreach (var node in _documentService!.CurrentDocument.Nodes)
            {
                var control = BuildFlowNodeControl(node);
                Canvas.SetLeft(control, node.X);
                Canvas.SetTop(control, node.Y);

                canvas.Children.Add(control);
                nodeMap[node.Id.Value] = control;
            }

            foreach (var edge in _documentService.CurrentDocument.Edges)
            {
                if (!nodeMap.TryGetValue(edge.SourceNodeId.Value, out var sourceControl))
                    continue;

                if (!nodeMap.TryGetValue(edge.TargetNodeId.Value, out var targetControl))
                    continue;

                var control = new EdgeControl(canvas, sourceControl, targetControl)
                {
                    DiagramDirection = _currentDiagramFlowDirection,
                    Label = edge.Label ?? string.Empty,
                    StyleKind = edge.Kind switch
                    {
                        EdgeKind.Dashed => EdgeStyleKind.Dashed,
                        EdgeKind.Thick => EdgeStyleKind.Thick,
                        _ => EdgeStyleKind.Default
                    },
                    Direction = edge.Direction == DocumentEdgeDirection.Reverse
                        ? EdgeDirection.Reverse
                        : EdgeDirection.Forward
                };

                control.AddHandler(
                    PointerPressedEvent,
                    OnFlowEdgePressed,
                    RoutingStrategies.Bubble,
                    handledEventsToo: true);

                _edges.Add(control);
                canvas.Children.Insert(0, control);
            }

            Dispatcher.UIThread.Post(() =>
            {
                foreach (var edge in _edges)
                    edge.RefreshGeometry();
            }, DispatcherPriority.Loaded);

            return;
        }

        var stateNodeMap = new Dictionary<string, StateNodeControl>(StringComparer.Ordinal);

        foreach (var stateNode in _documentService!.CurrentDocument.StateNodes)
        {
            var control = BuildStateNodeControl(stateNode);
            Canvas.SetLeft(control, stateNode.X);
            Canvas.SetTop(control, stateNode.Y);

            canvas.Children.Add(control);
            stateNodeMap[stateNode.Id.Value] = control;
        }

        foreach (var transition in _documentService.CurrentDocument.StateTransitions)
        {
            if (!stateNodeMap.TryGetValue(transition.SourceStateId.Value, out var sourceControl))
                continue;

            if (!stateNodeMap.TryGetValue(transition.TargetStateId.Value, out var targetControl))
                continue;

            var control = new StateTransitionControl(canvas, sourceControl, targetControl, transition);
            control.AddHandler(
                PointerPressedEvent,
                OnStateTransitionPressed,
                RoutingStrategies.Bubble,
                handledEventsToo: true);

            _stateTransitions.Add(control);
            canvas.Children.Insert(0, control);
        }

        Dispatcher.UIThread.Post(() =>
        {
            foreach (var transition in _stateTransitions)
                transition.RefreshGeometry();
        }, DispatcherPriority.Loaded);
    }

    private NodeControl BuildFlowNodeControl(Node model)
    {
        var control = new NodeControl
        {
            DataContext = model
        };

        control.AddHandler(
            PointerPressedEvent,
            OnFlowNodePressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        control.PortPreviewStarted += OnFlowPortPreviewStarted;
        control.PortPreviewMoved += OnPortPreviewMoved;
        control.PortPreviewEnded += OnFlowPortPreviewEnded;

        return control;
    }

    private StateNodeControl BuildStateNodeControl(StateNode model)
    {
        var control = new StateNodeControl
        {
            DataContext = model
        };

        control.AddHandler(
            PointerPressedEvent,
            OnStateNodePressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        control.PortPreviewStarted += OnStatePortPreviewStarted;
        control.PortPreviewMoved += OnPortPreviewMoved;
        control.PortPreviewEnded += OnStatePortPreviewEnded;

        return control;
    }

    // =========================================================
    // Canvas interactions
    // =========================================================
    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        FocusWorkspace();

        var canvas = GetCanvas();

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            ClearSelection();
            return;
        }

        if (e.Handled)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var posCanvas = e.GetPosition(canvas);

        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            _diagramEditingService!.CreateNode<Node, NodeControl>(
                posCanvas.X,
                posCanvas.Y,
                (x, y) => new Node
                {
                    Label = "Node",
                    X = x,
                    Y = y,
                    VisualStyle = NodeVisualStyle.Rectangle
                },
                model => new NodeControl
                {
                    DataContext = model
                },
                newNode =>
                {
                    newNode.AddHandler(
                        PointerPressedEvent,
                        OnFlowNodePressed,
                        RoutingStrategies.Bubble,
                        handledEventsToo: true);

                    newNode.PortPreviewStarted += OnFlowPortPreviewStarted;
                    newNode.PortPreviewMoved += OnPortPreviewMoved;
                    newNode.PortPreviewEnded += OnFlowPortPreviewEnded;

                    Canvas.SetLeft(newNode, ((Node)newNode.DataContext!).X);
                    Canvas.SetTop(newNode, ((Node)newNode.DataContext!).Y);
                },
                newNode => _history!.Execute(new CreateNodeCommand(canvas, newNode))
            );

            SyncFlowchartDocument();
            RaiseWorkspaceChanged();
            return;
        }

        CreateStateNode(StateNodeKind.Normal, posCanvas.X, posCanvas.Y);
    }

    private void OnCanvasMoved(object? sender, PointerEventArgs e)
    {
        if (_previewLine == null)
            return;

        var canvas = GetCanvas();
        _previewLine.EndPoint = e.GetPosition(canvas);
    }

    private void OnCanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Rien : commit géré par les previews
    }

    // =========================================================
    // Flowchart selection
    // =========================================================
    private void OnFlowNodePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        FocusWorkspace();
        e.Handled = true;

        var node = (NodeControl)sender!;
        SetSelection(node);
    }

    private void OnFlowEdgePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        FocusWorkspace();
        e.Handled = true;

        var edge = (EdgeControl)sender!;
        SetSelection(edge);
    }

    // =========================================================
    // State selection
    // =========================================================
    private void OnStateNodePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        FocusWorkspace();
        e.Handled = true;

        var node = (StateNodeControl)sender!;
        SetSelection(node);
    }

    private void OnStateTransitionPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        FocusWorkspace();
        e.Handled = true;

        var transition = (StateTransitionControl)sender!;
        SetSelection(transition);
    }

    private void SetSelection(NodeControl node)
    {
        ClearSelectionVisualOnly();
        _selectionService!.SelectNode(node);
        node.SetSelected(true);
        RaiseWorkspaceChanged();
    }

    private void SetSelection(EdgeControl edge)
    {
        ClearSelectionVisualOnly();
        _selectionService!.SelectEdge(edge);
        edge.SetSelected(true);
        RaiseWorkspaceChanged();
    }

    private void SetSelection(StateNodeControl node)
    {
        ClearSelectionVisualOnly();
        _selectionService!.SelectNode(node);
        node.SetSelected(true);
        RaiseWorkspaceChanged();
    }

    private void SetSelection(StateTransitionControl transition)
    {
        ClearSelectionVisualOnly();
        _selectionService!.SelectEdge(transition);
        transition.SetSelected(true);
        RaiseWorkspaceChanged();
    }

    // =========================================================
    // Preview helpers
    // =========================================================
    private void OnFlowPortPreviewStarted(NodeControl source, Point startInWindow)
    {
        _previewSource = source;
        _previewStateSource = null;
        StartPreviewLine(startInWindow);
    }

    private void OnStatePortPreviewStarted(StateNodeControl source, Point startInWindow)
    {
        _previewStateSource = source;
        _previewSource = null;
        StartPreviewLine(startInWindow);
    }

    private void StartPreviewLine(Point startInWindow)
    {
        var canvas = GetCanvas();
        var canvasOrigin = canvas.TranslatePoint(new Point(0, 0), TopLevel.GetTopLevel(this));

        if (canvasOrigin == null)
            return;

        var start = new Point(
            startInWindow.X - canvasOrigin.Value.X,
            startInWindow.Y - canvasOrigin.Value.Y);

        _previewLine = new Line
        {
            StartPoint = start,
            EndPoint = start,
            Stroke = Brushes.Orange,
            StrokeThickness = 2,
            IsHitTestVisible = false
        };

        canvas.Children.Add(_previewLine);
    }

    private void OnPortPreviewMoved(Point currentInWindow)
    {
        if (_previewLine == null)
            return;

        var canvas = GetCanvas();
        var canvasOrigin = canvas.TranslatePoint(new Point(0, 0), TopLevel.GetTopLevel(this));

        if (canvasOrigin == null)
            return;

        _previewLine.EndPoint = new Point(
            currentInWindow.X - canvasOrigin.Value.X,
            currentInWindow.Y - canvasOrigin.Value.Y);
    }

    private void OnFlowPortPreviewEnded()
    {
        var canvas = GetCanvas();

        if (_previewLine == null || _previewSource == null)
            return;

        var releasePosInCanvas = _previewLine.EndPoint;

        NodeControl? targetNode = null;

        foreach (var child in canvas.Children)
        {
            if (child is NodeControl node &&
                node != _previewSource &&
                node.IsPointInsideNode(releasePosInCanvas, canvas))
            {
                targetNode = node;
                break;
            }
        }

        canvas.Children.Remove(_previewLine);
        _previewLine = null;

        _diagramEditingService!.CreateEdge<NodeControl, EdgeControl>(
            _previewSource,
            targetNode,
            (source, target) => _edges.Any(edge =>
                ReferenceEquals(edge.SourceNode, source) &&
                ReferenceEquals(edge.TargetNode, target)),
            (source, target) => new EdgeControl(canvas, source, target)
            {
                DiagramDirection = _currentDiagramFlowDirection
            },
            edge =>
            {
                edge.AddHandler(
                    PointerPressedEvent,
                    OnFlowEdgePressed,
                    RoutingStrategies.Bubble,
                    handledEventsToo: true);
            },
            edge =>
            {
                _history!.Execute(new CreateEdgeCommand(canvas, _edges, edge));
            }
        );

        _previewSource = null;
        SyncFlowchartDocument();
        RaiseWorkspaceChanged();
    }

    private void OnStatePortPreviewEnded()
    {
        var canvas = GetCanvas();

        if (_previewLine == null || _previewStateSource == null)
            return;

        var releasePosInCanvas = _previewLine.EndPoint;

        StateNodeControl? targetNode = null;

        foreach (var child in canvas.Children)
        {
            if (child is StateNodeControl node &&
                node != _previewStateSource &&
                node.IsPointInsideNode(releasePosInCanvas, canvas))
            {
                targetNode = node;
                break;
            }
        }

        canvas.Children.Remove(_previewLine);
        _previewLine = null;

        if (targetNode != null &&
            _previewStateSource.DataContext is StateNode sourceModel &&
            targetNode.DataContext is StateNode targetModel)
        {
            var exists = _documentService!.CurrentDocument.StateTransitions.Any(t =>
                t.SourceStateId.Equals(sourceModel.Id) &&
                t.TargetStateId.Equals(targetModel.Id));

            if (!exists)
            {
                var transition = new StateTransition
                {
                    SourceStateId = sourceModel.Id,
                    TargetStateId = targetModel.Id,
                    Label = string.Empty
                };

                _documentService.CurrentDocument.StateTransitions.Add(transition);

                var control = new StateTransitionControl(canvas, _previewStateSource, targetNode, transition);
                control.AddHandler(
                    PointerPressedEvent,
                    OnStateTransitionPressed,
                    RoutingStrategies.Bubble,
                    handledEventsToo: true);

                _stateTransitions.Add(control);
                canvas.Children.Insert(0, control);

                Dispatcher.UIThread.Post(() => control.RefreshGeometry(), DispatcherPriority.Loaded);
            }
        }

        _previewStateSource = null;
        RaiseWorkspaceChanged();
    }

    private void RaiseWorkspaceChanged()
    {
        WorkspaceStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureConfigured()
    {
        if (_selectionService == null ||
            _diagramEditingService == null ||
            _documentService == null ||
            _history == null)
        {
            throw new InvalidOperationException("DiagramWorkspaceControl n'a pas été configuré.");
        }
    }
}
