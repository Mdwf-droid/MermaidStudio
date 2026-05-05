using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MermaidStudio.Application.Editing;
using MermaidStudio.Application.Export;
using MermaidStudio.Application.Import;
using MermaidStudio.Application.Persistence;
using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.Edges;
using MermaidStudio.Domain.Nodes;
using MermaidStudio.Domain.States;
using MermaidStudio.UI.Avalonia.Controls;
using MermaidStudio.UI.Avalonia.Editing;
using System.IO;
using DocumentFlowDirection = MermaidStudio.Domain.Diagrams.FlowDirection;

namespace MermaidStudio.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly CommandHistory _history = new();
    private readonly FlowchartExportService _flowchartExportService = new();
    private readonly StateDiagramExportService _stateDiagramExportService = new();

    private readonly SelectionService _selectionService = new();
    private readonly DiagramEditingService _diagramEditingService;
    private readonly InspectorStateService _inspectorStateService;
    private readonly DiagramDocumentService _documentService = new();

    private readonly DiagramDocumentJsonService _jsonService = new();
    private readonly FlowchartMermaidImportService _mermaidImportService = new();

    private NodeControl? _previewSource;
    private StateNodeControl? _previewStateSource;
    private Line? _previewLine;

    private readonly List<EdgeControl> _edges = new();
    private readonly List<StateTransitionControl> _stateTransitions = new();

    private DiagramFlowDirection _currentDiagramFlowDirection = DiagramFlowDirection.LR;

    private bool _uiReady;
    private bool _suspendFlowDirectionHandling;
    private bool _suspendDiagramKindHandling;

    public MainWindow()
    {
        _diagramEditingService = new DiagramEditingService(_selectionService);
        _inspectorStateService = new InspectorStateService(_selectionService, _documentService);

        _uiReady = false;
        AvaloniaXamlLoader.Load(this);
        _uiReady = true;

        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

        SyncCurrentDocument();
        ApplyDiagramKindToUi();
    }

    private Canvas GetEditorCanvas()
        => this.FindControl<Canvas>("EditorCanvas")
           ?? throw new InvalidOperationException("EditorCanvas introuvable dans MainWindow.");

    private ComboBox GetDiagramKindComboBox()
        => this.FindControl<ComboBox>("DiagramKindComboBox")
           ?? throw new InvalidOperationException("DiagramKindComboBox introuvable dans MainWindow.");

    private ComboBox GetFlowDirectionComboBox()
        => this.FindControl<ComboBox>("FlowDirectionComboBox")
           ?? throw new InvalidOperationException("FlowDirectionComboBox introuvable dans MainWindow.");

    private Button GetAddStartStateButton()
        => this.FindControl<Button>("AddStartStateButton")
           ?? throw new InvalidOperationException("AddStartStateButton introuvable dans MainWindow.");

    private Button GetAddEndStateButton()
        => this.FindControl<Button>("AddEndStateButton")
           ?? throw new InvalidOperationException("AddEndStateButton introuvable dans MainWindow.");

    private TextBox GetSelectedNodeLabelTextBox()
        => this.FindControl<TextBox>("SelectedNodeLabelTextBox")
           ?? throw new InvalidOperationException("SelectedNodeLabelTextBox introuvable dans MainWindow.");

    private Button GetApplyNodeLabelButton()
        => this.FindControl<Button>("ApplyNodeLabelButton")
           ?? throw new InvalidOperationException("ApplyNodeLabelButton introuvable dans MainWindow.");

    private ComboBox GetSelectedNodeStyleComboBox()
        => this.FindControl<ComboBox>("SelectedNodeStyleComboBox")
           ?? throw new InvalidOperationException("SelectedNodeStyleComboBox introuvable dans MainWindow.");

    private Button GetApplyNodeStyleButton()
        => this.FindControl<Button>("ApplyNodeStyleButton")
           ?? throw new InvalidOperationException("ApplyNodeStyleButton introuvable dans MainWindow.");

    private TextBox GetSelectedEdgeLabelTextBox()
        => this.FindControl<TextBox>("SelectedEdgeLabelTextBox")
           ?? throw new InvalidOperationException("SelectedEdgeLabelTextBox introuvable dans MainWindow.");

    private Button GetApplyEdgeLabelButton()
        => this.FindControl<Button>("ApplyEdgeLabelButton")
           ?? throw new InvalidOperationException("ApplyEdgeLabelButton introuvable dans MainWindow.");

    private ComboBox GetSelectedEdgeStyleComboBox()
        => this.FindControl<ComboBox>("SelectedEdgeStyleComboBox")
           ?? throw new InvalidOperationException("SelectedEdgeStyleComboBox introuvable dans MainWindow.");

    private ComboBox GetSelectedEdgeDirectionComboBox()
        => this.FindControl<ComboBox>("SelectedEdgeDirectionComboBox")
           ?? throw new InvalidOperationException("SelectedEdgeDirectionComboBox introuvable dans MainWindow.");

    private Button GetApplyEdgeStyleButton()
        => this.FindControl<Button>("ApplyEdgeStyleButton")
           ?? throw new InvalidOperationException("ApplyEdgeStyleButton introuvable dans MainWindow.");

    private TextBox GetMermaidOutputTextBox()
        => this.FindControl<TextBox>("MermaidOutputTextBox")
           ?? throw new InvalidOperationException("MermaidOutputTextBox introuvable dans MainWindow.");

    private DiagramKind CurrentDiagramKind => _documentService.CurrentDocument.Kind;

    private NodeControl? GetSelectedFlowNode()
        => _selectionService.Kind == SelectionKind.Node
            ? _selectionService.GetSelected<NodeControl>()
            : null;

    private EdgeControl? GetSelectedFlowEdge()
        => _selectionService.Kind == SelectionKind.Edge
            ? _selectionService.GetSelected<EdgeControl>()
            : null;

    private StateNodeControl? GetSelectedStateNode()
        => _selectionService.Kind == SelectionKind.Node
            ? _selectionService.GetSelected<StateNodeControl>()
            : null;

    private StateTransitionControl? GetSelectedStateTransition()
        => _selectionService.Kind == SelectionKind.Edge
            ? _selectionService.GetSelected<StateTransitionControl>()
            : null;

    // =========================================================
    // Toolbar / kind / special buttons
    // =========================================================
    private void OnDiagramKindChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suspendDiagramKindHandling)
            return;

        if (!_uiReady)
            return;

        var combo = GetDiagramKindComboBox();
        var selectedKind = combo.SelectedIndex == 1
            ? DiagramKind.StateDiagram
            : DiagramKind.Flowchart;

        if (selectedKind == CurrentDiagramKind)
            return;

        ClearSelectionVisualOnly();
        _selectionService.ClearSelection();
        _history.Clear();

        _documentService.ResetToKind(selectedKind);

        if (selectedKind == DiagramKind.Flowchart)
            _currentDiagramFlowDirection = DiagramFlowDirection.LR;

        ApplyDiagramKindToUi();
        ApplyDocumentDirectionToUi();
        RebuildCanvasFromCurrentDocument();
        RefreshInspector();
        Focus();
    }

    private void ApplyDiagramKindToUi()
    {
        if (!_uiReady)
            return;

        _suspendDiagramKindHandling = true;
        GetDiagramKindComboBox().SelectedIndex = CurrentDiagramKind == DiagramKind.StateDiagram ? 1 : 0;
        _suspendDiagramKindHandling = false;

        var isState = CurrentDiagramKind == DiagramKind.StateDiagram;
        GetAddStartStateButton().IsEnabled = isState;
        GetAddEndStateButton().IsEnabled = isState;
        GetFlowDirectionComboBox().IsEnabled = !isState;
    }

    private void OnAddStartStateClicked(object? sender, RoutedEventArgs e)
    {
        if (CurrentDiagramKind != DiagramKind.StateDiagram)
            return;

        CreateStateNode(StateNodeKind.Start, 80, 80 + _documentService.CurrentDocument.StateNodes.Count * 55);
        RefreshInspector();
    }

    private void OnAddEndStateClicked(object? sender, RoutedEventArgs e)
    {
        if (CurrentDiagramKind != DiagramKind.StateDiagram)
            return;

        CreateStateNode(StateNodeKind.End, 160, 80 + _documentService.CurrentDocument.StateNodes.Count * 55);
        RefreshInspector();
    }

    // =========================================================
    // Save / Load JSON
    // =========================================================
    private async void OnSaveJsonClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save diagram as JSON",
            SuggestedFileName = "diagram.json",
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        if (file == null)
            return;

        var json = _jsonService.Serialize(_documentService.CurrentDocument);

        await using var stream = await file.OpenWriteAsync();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(json);
    }

    private async void OnLoadJsonClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load diagram JSON",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        var file = files.FirstOrDefault();
        if (file == null)
            return;

        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var document = _jsonService.Deserialize(json);
            LoadDocumentIntoEditor(document);
        }
        catch (Exception ex)
        {
            GetMermaidOutputTextBox().Text = $"Load JSON failed: {ex.Message}";
        }
    }

    // =========================================================
    // Import Mermaid (flowchart only in S17)
    // =========================================================
    private async void OnImportMermaidClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new ImportMermaidWindow();
        var mermaidText = await dialog.ShowDialog<string?>(this);

        if (string.IsNullOrWhiteSpace(mermaidText))
            return;

        try
        {
            var document = _mermaidImportService.Import(mermaidText);
            LoadDocumentIntoEditor(document);
        }
        catch (Exception ex)
        {
            GetMermaidOutputTextBox().Text = $"Import Mermaid failed: {ex.Message}";
        }
    }

    private void LoadDocumentIntoEditor(DiagramDocument document)
    {
        ClearSelectionVisualOnly();
        _selectionService.ClearSelection();
        _history.Clear();

        _documentService.LoadDocument(document);

        if (document.Kind == DiagramKind.Flowchart)
        {
            _currentDiagramFlowDirection = document.Direction switch
            {
                DocumentFlowDirection.TB => DiagramFlowDirection.TB,
                DocumentFlowDirection.RL => DiagramFlowDirection.RL,
                DocumentFlowDirection.BT => DiagramFlowDirection.BT,
                _ => DiagramFlowDirection.LR
            };
        }

        ApplyDiagramKindToUi();
        ApplyDocumentDirectionToUi();
        RebuildCanvasFromCurrentDocument();
        RefreshInspector();
        Focus();
    }

    private void ApplyDocumentDirectionToUi()
    {
        if (!_uiReady)
            return;

        var combo = GetFlowDirectionComboBox();

        _suspendFlowDirectionHandling = true;
        combo.SelectedIndex = _currentDiagramFlowDirection switch
        {
            DiagramFlowDirection.TB => 1,
            DiagramFlowDirection.RL => 2,
            DiagramFlowDirection.BT => 3,
            _ => 0
        };
        _suspendFlowDirectionHandling = false;
    }

    private void RebuildCanvasFromCurrentDocument()
    {
        var canvas = GetEditorCanvas();

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

            foreach (var node in _documentService.CurrentDocument.Nodes)
            {
                var control = new NodeControl
                {
                    DataContext = node
                };

                control.AddHandler(
                    PointerPressedEvent,
                    OnFlowNodePressed,
                    RoutingStrategies.Bubble,
                    handledEventsToo: true);

                control.PortPreviewStarted += OnFlowPortPreviewStarted;
                control.PortPreviewMoved += OnPortPreviewMoved;
                control.PortPreviewEnded += OnFlowPortPreviewEnded;

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

        // State diagram projection
        var stateNodeMap = new Dictionary<string, StateNodeControl>(StringComparer.Ordinal);

        foreach (var stateNode in _documentService.CurrentDocument.StateNodes)
        {
            var control = new StateNodeControl
            {
                DataContext = stateNode
            };

            control.AddHandler(
                PointerPressedEvent,
                OnStateNodePressed,
                RoutingStrategies.Bubble,
                handledEventsToo: true);

            control.PortPreviewStarted += OnStatePortPreviewStarted;
            control.PortPreviewMoved += OnPortPreviewMoved;
            control.PortPreviewEnded += OnStatePortPreviewEnded;

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

    // =========================================================
    // Canvas press (create node/state)
    // =========================================================
    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();

        var canvas = (Canvas)sender!;

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
            _diagramEditingService.CreateNode<Node, NodeControl>(
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
                newNode => _history.Execute(new CreateNodeCommand(canvas, newNode))
            );

            SyncCurrentDocument();
            RefreshInspector();
            return;
        }

        CreateStateNode(StateNodeKind.Normal, posCanvas.X, posCanvas.Y);
        RefreshInspector();
    }

    private void CreateStateNode(StateNodeKind kind, double x, double y)
    {
        var canvas = GetEditorCanvas();

        var model = new StateNode
        {
            Label = kind == StateNodeKind.Normal ? "State" : string.Empty,
            X = x,
            Y = y,
            Kind = kind
        };

        _documentService.CurrentDocument.Kind = DiagramKind.StateDiagram;
        _documentService.CurrentDocument.StateNodes.Add(model);

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

        Canvas.SetLeft(control, model.X);
        Canvas.SetTop(control, model.Y);

        canvas.Children.Add(control);
    }

    private void OnCanvasMoved(object? sender, PointerEventArgs e)
    {
        if (_previewLine == null)
            return;

        var canvas = (Canvas)sender!;
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

        Focus();
        e.Handled = true;

        var node = (NodeControl)sender!;
        SetSelection(node);
    }

    private void OnFlowEdgePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        Focus();
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

        Focus();
        e.Handled = true;

        var node = (StateNodeControl)sender!;
        SetSelection(node);
    }

    private void OnStateTransitionPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        Focus();
        e.Handled = true;

        var transition = (StateTransitionControl)sender!;
        SetSelection(transition);
    }

    private void SetSelection(NodeControl node)
    {
        ClearSelectionVisualOnly();
        _selectionService.SelectNode(node);
        node.SetSelected(true);
        RefreshInspector();
    }

    private void SetSelection(EdgeControl edge)
    {
        ClearSelectionVisualOnly();
        _selectionService.SelectEdge(edge);
        edge.SetSelected(true);
        RefreshInspector();
    }

    private void SetSelection(StateNodeControl node)
    {
        ClearSelectionVisualOnly();
        _selectionService.SelectNode(node);
        node.SetSelected(true);
        RefreshInspector();
    }

    private void SetSelection(StateTransitionControl transition)
    {
        ClearSelectionVisualOnly();
        _selectionService.SelectEdge(transition);
        transition.SetSelected(true);
        RefreshInspector();
    }

    private void ClearSelectionVisualOnly()
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

    private void ClearSelection()
    {
        ClearSelectionVisualOnly();
        _selectionService.ClearSelection();
        RefreshInspector();
    }

    // =========================================================
    // Inspector
    // =========================================================
    private void RefreshInspector()
    {
        if (!_uiReady)
            return;

        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            RefreshFlowchartInspector();
            return;
        }

        RefreshStateInspector();
    }

    private void RefreshFlowchartInspector()
    {
        var canvas = GetEditorCanvas();

        var state = _inspectorStateService.BuildState<NodeControl, EdgeControl>(
            isNodeActive: node => canvas.Children.Contains(node),
            resolveNodeId: node => (node.DataContext as Node)?.Id.Value,
            isEdgeActive: edge => canvas.Children.Contains(edge),
            resolveEdgeEndpoints: edge =>
            {
                var sourceNode = edge.SourceNode.DataContext as Node;
                var targetNode = edge.TargetNode.DataContext as Node;

                if (sourceNode == null || targetNode == null)
                    return null;

                return (sourceNode.Id.Value, targetNode.Id.Value);
            });

        if (state.ClearSelectionRequested)
        {
            _selectionService.ClearSelection();

            state = _inspectorStateService.BuildState<NodeControl, EdgeControl>(
                isNodeActive: node => canvas.Children.Contains(node),
                resolveNodeId: node => (node.DataContext as Node)?.Id.Value,
                isEdgeActive: edge => canvas.Children.Contains(edge),
                resolveEdgeEndpoints: edge =>
                {
                    var sourceNode = edge.SourceNode.DataContext as Node;
                    var targetNode = edge.TargetNode.DataContext as Node;

                    if (sourceNode == null || targetNode == null)
                        return null;

                    return (sourceNode.Id.Value, targetNode.Id.Value);
                });
        }

        ApplyInspectorState(
            nodeSectionEnabled: state.NodeSectionEnabled,
            nodeLabel: state.NodeLabel,
            nodeStyleIndex: state.NodeStyleIndex,
            nodeStyleEnabled: state.NodeSectionEnabled,
            edgeSectionEnabled: state.EdgeSectionEnabled,
            edgeLabel: state.EdgeLabel,
            edgeStyleIndex: state.EdgeStyleIndex,
            edgeDirectionIndex: state.EdgeDirectionIndex,
            edgeStyleEnabled: state.EdgeSectionEnabled,
            edgeDirectionEnabled: state.EdgeSectionEnabled,
            edgeStyleApplyEnabled: state.EdgeSectionEnabled);
    }

    private void RefreshStateInspector()
    {
        var selectedStateNode = GetSelectedStateNode();
        if (selectedStateNode?.DataContext is StateNode stateNode &&
            GetEditorCanvas().Children.Contains(selectedStateNode))
        {
            var labelEnabled = stateNode.Kind == StateNodeKind.Normal;

            ApplyInspectorState(
                nodeSectionEnabled: labelEnabled,
                nodeLabel: stateNode.Label,
                nodeStyleIndex: 0,
                nodeStyleEnabled: false,
                edgeSectionEnabled: false,
                edgeLabel: string.Empty,
                edgeStyleIndex: 0,
                edgeDirectionIndex: 0,
                edgeStyleEnabled: false,
                edgeDirectionEnabled: false,
                edgeStyleApplyEnabled: false);
            return;
        }

        var selectedTransition = GetSelectedStateTransition();
        if (selectedTransition != null &&
            GetEditorCanvas().Children.Contains(selectedTransition))
        {
            ApplyInspectorState(
                nodeSectionEnabled: false,
                nodeLabel: string.Empty,
                nodeStyleIndex: 0,
                nodeStyleEnabled: false,
                edgeSectionEnabled: true,
                edgeLabel: selectedTransition.Model.Label,
                edgeStyleIndex: 0,
                edgeDirectionIndex: 0,
                edgeStyleEnabled: false,
                edgeDirectionEnabled: false,
                edgeStyleApplyEnabled: false);
            return;
        }

        ApplyInspectorState(
            nodeSectionEnabled: false,
            nodeLabel: string.Empty,
            nodeStyleIndex: 0,
            nodeStyleEnabled: false,
            edgeSectionEnabled: false,
            edgeLabel: string.Empty,
            edgeStyleIndex: 0,
            edgeDirectionIndex: 0,
            edgeStyleEnabled: false,
            edgeDirectionEnabled: false,
            edgeStyleApplyEnabled: false);
    }

    private void ApplyInspectorState(
        bool nodeSectionEnabled,
        string nodeLabel,
        int nodeStyleIndex,
        bool nodeStyleEnabled,
        bool edgeSectionEnabled,
        string edgeLabel,
        int edgeStyleIndex,
        int edgeDirectionIndex,
        bool edgeStyleEnabled,
        bool edgeDirectionEnabled,
        bool edgeStyleApplyEnabled)
    {
        var nodeTextBox = GetSelectedNodeLabelTextBox();
        var nodeLabelButton = GetApplyNodeLabelButton();
        var nodeStyleCombo = GetSelectedNodeStyleComboBox();
        var nodeStyleButton = GetApplyNodeStyleButton();

        var edgeTextBox = GetSelectedEdgeLabelTextBox();
        var edgeLabelButton = GetApplyEdgeLabelButton();
        var edgeStyleCombo = GetSelectedEdgeStyleComboBox();
        var edgeDirectionCombo = GetSelectedEdgeDirectionComboBox();
        var edgeStyleButton = GetApplyEdgeStyleButton();

        nodeTextBox.IsEnabled = nodeSectionEnabled;
        nodeLabelButton.IsEnabled = nodeSectionEnabled;
        nodeStyleCombo.IsEnabled = nodeStyleEnabled;
        nodeStyleButton.IsEnabled = nodeStyleEnabled;

        nodeTextBox.Text = nodeLabel;
        nodeStyleCombo.SelectedIndex = nodeStyleIndex;

        edgeTextBox.IsEnabled = edgeSectionEnabled;
        edgeLabelButton.IsEnabled = edgeSectionEnabled;
        edgeStyleCombo.IsEnabled = edgeStyleEnabled;
        edgeDirectionCombo.IsEnabled = edgeDirectionEnabled;
        edgeStyleButton.IsEnabled = edgeStyleApplyEnabled;

        edgeTextBox.Text = edgeLabel;
        edgeStyleCombo.SelectedIndex = edgeStyleIndex;
        edgeDirectionCombo.SelectedIndex = edgeDirectionIndex;
    }

    // =========================================================
    // Apply actions
    // =========================================================
    private void OnApplyNodeLabelClicked(object? sender, RoutedEventArgs e)
    {
        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            _diagramEditingService.UpdateSelectedNodeLabel<NodeControl, Node>(
                GetSelectedNodeLabelTextBox().Text,
                control => control.DataContext as Node,
                node => node.Label,
                (node, newLabel) => _history.Execute(new UpdateNodeLabelCommand(node, node.Label, newLabel))
            );

            SyncCurrentDocument();
            RefreshInspector();
            return;
        }

        var selectedStateNode = GetSelectedStateNode();
        if (selectedStateNode?.DataContext is not StateNode stateNode)
            return;

        if (stateNode.Kind != StateNodeKind.Normal)
            return;

        var newLabel = string.IsNullOrWhiteSpace(GetSelectedNodeLabelTextBox().Text?.Trim())
            ? "State"
            : GetSelectedNodeLabelTextBox().Text!.Trim();

        stateNode.Label = newLabel;
        RefreshInspector();
    }

    private void OnApplyNodeStyleClicked(object? sender, RoutedEventArgs e)
    {
        if (CurrentDiagramKind != DiagramKind.Flowchart)
            return;

        var combo = GetSelectedNodeStyleComboBox();

        var selectedStyle = combo.SelectedIndex switch
        {
            1 => NodeVisualStyle.Rounded,
            2 => NodeVisualStyle.Decision,
            3 => NodeVisualStyle.Circle,
            _ => NodeVisualStyle.Rectangle
        };

        _diagramEditingService.UpdateSelectedNodeStyle<NodeControl, Node, NodeVisualStyle>(
            selectedStyle,
            control => control.DataContext as Node,
            (node, style) => node.VisualStyle = style
        );

        SyncCurrentDocument();
        RefreshInspector();
    }

    private void OnApplyEdgeLabelClicked(object? sender, RoutedEventArgs e)
    {
        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            _diagramEditingService.UpdateSelectedEdgeLabel<EdgeControl>(
                GetSelectedEdgeLabelTextBox().Text,
                (edge, newLabel) => edge.Label = newLabel
            );

            SyncCurrentDocument();
            RefreshInspector();
            return;
        }

        var selectedTransition = GetSelectedStateTransition();
        if (selectedTransition == null)
            return;

        selectedTransition.Label = GetSelectedEdgeLabelTextBox().Text?.Trim() ?? string.Empty;
        RefreshInspector();
    }

    private void OnApplyEdgeStyleClicked(object? sender, RoutedEventArgs e)
    {
        if (CurrentDiagramKind != DiagramKind.Flowchart)
            return;

        var styleCombo = GetSelectedEdgeStyleComboBox();
        var directionCombo = GetSelectedEdgeDirectionComboBox();

        var style = styleCombo.SelectedIndex switch
        {
            1 => EdgeStyleKind.Dashed,
            2 => EdgeStyleKind.Thick,
            _ => EdgeStyleKind.Default
        };

        var direction = directionCombo.SelectedIndex switch
        {
            1 => EdgeDirection.Reverse,
            _ => EdgeDirection.Forward
        };

        _diagramEditingService.UpdateSelectedEdgeStyle<EdgeControl, EdgeStyleKind, EdgeDirection>(
            style,
            direction,
            (edge, styleValue, directionValue) =>
            {
                edge.StyleKind = styleValue;
                edge.Direction = directionValue;
            });

        SyncCurrentDocument();
        RefreshInspector();
    }

    // =========================================================
    // Flow direction
    // =========================================================
    private void OnFlowDirectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suspendFlowDirectionHandling)
            return;

        if (CurrentDiagramKind != DiagramKind.Flowchart)
            return;

        if (sender is not ComboBox combo)
            return;

        if (combo.SelectedItem is ComboBoxItem item &&
            item.Content is string value)
        {
            _currentDiagramFlowDirection = value switch
            {
                "RL" => DiagramFlowDirection.RL,
                "TB" => DiagramFlowDirection.TB,
                "BT" => DiagramFlowDirection.BT,
                _ => DiagramFlowDirection.LR
            };

            if (!_uiReady)
                return;

            foreach (var edge in _edges)
                edge.DiagramDirection = _currentDiagramFlowDirection;

            SyncCurrentDocument();
        }
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
        var canvas = GetEditorCanvas();
        var canvasOrigin = canvas.TranslatePoint(new Point(0, 0), this);

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

        var canvas = GetEditorCanvas();
        var canvasOrigin = canvas.TranslatePoint(new Point(0, 0), this);

        if (canvasOrigin == null)
            return;

        _previewLine.EndPoint = new Point(
            currentInWindow.X - canvasOrigin.Value.X,
            currentInWindow.Y - canvasOrigin.Value.Y);
    }

    private void OnFlowPortPreviewEnded()
    {
        var canvas = GetEditorCanvas();

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

        _diagramEditingService.CreateEdge<NodeControl, EdgeControl>(
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
            edge => _history.Execute(new CreateEdgeCommand(canvas, _edges, edge))
        );

        _previewSource = null;
        SyncCurrentDocument();
        RefreshInspector();
    }

    private void OnStatePortPreviewEnded()
    {
        var canvas = GetEditorCanvas();

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
            var exists = _documentService.CurrentDocument.StateTransitions.Any(t =>
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
        RefreshInspector();
    }

    // =========================================================
    // Export Mermaid
    // =========================================================
    private void OnExportMermaidClicked(object? sender, RoutedEventArgs e)
    {
        var textBox = GetMermaidOutputTextBox();

        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            var exportModel = BuildFlowchartExportModelFromDocument();
            textBox.Text = _flowchartExportService.Export(exportModel);
            return;
        }

        textBox.Text = _stateDiagramExportService.Export(_documentService.CurrentDocument);
    }

    private FlowchartExportModel BuildFlowchartExportModelFromDocument()
    {
        var document = _documentService.CurrentDocument;

        var model = new FlowchartExportModel
        {
            Direction = document.Direction switch
            {
                DocumentFlowDirection.TB => FlowchartExportDiagramDirection.TB,
                DocumentFlowDirection.RL => FlowchartExportDiagramDirection.RL,
                DocumentFlowDirection.BT => FlowchartExportDiagramDirection.BT,
                _ => FlowchartExportDiagramDirection.LR
            }
        };

        foreach (var node in document.Nodes)
        {
            model.Nodes.Add(new FlowchartExportNode
            {
                Id = node.Id.Value,
                Label = node.Label,
                Style = node.VisualStyle switch
                {
                    NodeVisualStyle.Rounded => FlowchartExportNodeStyle.Rounded,
                    NodeVisualStyle.Decision => FlowchartExportNodeStyle.Decision,
                    NodeVisualStyle.Circle => FlowchartExportNodeStyle.Circle,
                    _ => FlowchartExportNodeStyle.Rectangle
                }
            });
        }

        foreach (var edge in document.Edges)
        {
            model.Edges.Add(new FlowchartExportEdge
            {
                SourceId = edge.SourceNodeId.Value,
                TargetId = edge.TargetNodeId.Value,
                Label = edge.Label ?? string.Empty,
                Style = edge.Kind switch
                {
                    EdgeKind.Dashed => FlowchartExportEdgeStyle.Dashed,
                    EdgeKind.Thick => FlowchartExportEdgeStyle.Thick,
                    _ => FlowchartExportEdgeStyle.Default
                },
                Direction = edge.Direction == DocumentEdgeDirection.Reverse
                    ? FlowchartExportEdgeDirection.Reverse
                    : FlowchartExportEdgeDirection.Forward
            });
        }

        return model;
    }

    // =========================================================
    // Undo / Redo / Delete
    // =========================================================
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _history.Undo();
            if (CurrentDiagramKind == DiagramKind.Flowchart)
                SyncCurrentDocument();

            RefreshInspector();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _history.Redo();
            if (CurrentDiagramKind == DiagramKind.Flowchart)
                SyncCurrentDocument();

            RefreshInspector();
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

    private void DeleteSelectedNodeOrState()
    {
        var canvas = GetEditorCanvas();

        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            _diagramEditingService.DeleteSelectedNode<NodeControl>(
                beforeDelete: node => node.SetSelected(false),
                executeDelete: node => _history.Execute(new DeleteNodeCommand(canvas, node, _edges))
            );

            SyncCurrentDocument();
            RefreshInspector();
            return;
        }

        var selectedStateNode = GetSelectedStateNode();
        if (selectedStateNode?.DataContext is not StateNode stateNode)
            return;

        selectedStateNode.SetSelected(false);
        _selectionService.ClearSelection();

        var relatedTransitions = _stateTransitions
            .Where(t => t.Model.SourceStateId.Equals(stateNode.Id) || t.Model.TargetStateId.Equals(stateNode.Id))
            .ToList();

        foreach (var transition in relatedTransitions)
        {
            canvas.Children.Remove(transition);
            _stateTransitions.Remove(transition);
            _documentService.CurrentDocument.StateTransitions.Remove(transition.Model);
        }

        canvas.Children.Remove(selectedStateNode);
        _documentService.CurrentDocument.StateNodes.Remove(stateNode);

        RefreshInspector();
    }

    private void DeleteSelectedEdgeOrTransition()
    {
        var canvas = GetEditorCanvas();

        if (CurrentDiagramKind == DiagramKind.Flowchart)
        {
            _diagramEditingService.DeleteSelectedEdge<EdgeControl>(
                beforeDelete: edge => edge.SetSelected(false),
                executeDelete: edge => _history.Execute(new DeleteEdgeCommand(canvas, _edges, edge))
            );

            SyncCurrentDocument();
            RefreshInspector();
            return;
        }

        var selectedTransition = GetSelectedStateTransition();
        if (selectedTransition == null)
            return;

        selectedTransition.SetSelected(false);
        _selectionService.ClearSelection();

        canvas.Children.Remove(selectedTransition);
        _stateTransitions.Remove(selectedTransition);
        _documentService.CurrentDocument.StateTransitions.Remove(selectedTransition.Model);

        RefreshInspector();
    }

    // =========================================================
    // Flowchart sync (existing)
    // =========================================================
    private void SyncCurrentDocument()
    {
        if (!_uiReady || CurrentDiagramKind != DiagramKind.Flowchart)
            return;

        var canvas = GetEditorCanvas();

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

        _documentService.Synchronize(direction, nodes, edgeStates);
    }
}
