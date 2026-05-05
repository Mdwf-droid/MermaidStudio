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
using MermaidStudio.UI.Avalonia.Controls;
using MermaidStudio.UI.Avalonia.Editing;
using System.IO;
using DocumentFlowDirection = MermaidStudio.Domain.Diagrams.FlowDirection;

namespace MermaidStudio.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly CommandHistory _history = new();
    private readonly FlowchartExportService _flowchartExportService = new();

    private readonly SelectionService _selectionService = new();
    private readonly DiagramEditingService _diagramEditingService;
    private readonly InspectorStateService _inspectorStateService;
    private readonly DiagramDocumentService _documentService = new();

    private readonly DiagramDocumentJsonService _jsonService = new();
    private readonly FlowchartMermaidImportService _mermaidImportService = new();

    private NodeControl? _previewSource;
    private Line? _previewLine;

    private readonly List<EdgeControl> _edges = new();

    private DiagramFlowDirection _currentDiagramFlowDirection = DiagramFlowDirection.LR;

    private bool _uiReady;
    private bool _suspendFlowDirectionHandling;

    public MainWindow()
    {
        _diagramEditingService = new DiagramEditingService(_selectionService);
        _inspectorStateService = new InspectorStateService(_selectionService, _documentService);

        _uiReady = false;
        AvaloniaXamlLoader.Load(this);
        _uiReady = true;

        // S18 fix : capture clavier robuste même si un TextBox garde le focus
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

        SyncCurrentDocument();
    }

    private Canvas GetEditorCanvas()
        => this.FindControl<Canvas>("EditorCanvas")
           ?? throw new InvalidOperationException("EditorCanvas introuvable dans MainWindow.");

    private ComboBox GetFlowDirectionComboBox()
        => this.FindControl<ComboBox>("FlowDirectionComboBox")
           ?? throw new InvalidOperationException("FlowDirectionComboBox introuvable dans MainWindow.");

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

    private NodeControl? GetSelectedNode()
        => _selectionService.Kind == SelectionKind.Node
            ? _selectionService.GetSelected<NodeControl>()
            : null;

    private EdgeControl? GetSelectedEdge()
        => _selectionService.Kind == SelectionKind.Edge
            ? _selectionService.GetSelected<EdgeControl>()
            : null;

    // =========================================================
    // S18 — Save / Load JSON
    // =========================================================
    private async void OnSaveJsonClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save diagram as JSON",
            SuggestedFileName = "diagram.flowchart.json",
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
    // S18 — Import Mermaid “pur”
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

        _currentDiagramFlowDirection = document.Direction switch
        {
            DocumentFlowDirection.TB => DiagramFlowDirection.TB,
            DocumentFlowDirection.RL => DiagramFlowDirection.RL,
            DocumentFlowDirection.BT => DiagramFlowDirection.BT,
            _ => DiagramFlowDirection.LR
        };

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

        var existingNodes = canvas.Children.OfType<NodeControl>().ToList();
        foreach (var node in existingNodes)
            canvas.Children.Remove(node);

        foreach (var edge in _edges.ToList())
            canvas.Children.Remove(edge);

        _edges.Clear();

        var nodeMap = new Dictionary<string, NodeControl>(StringComparer.Ordinal);

        foreach (var node in _documentService.CurrentDocument.Nodes)
        {
            var control = new NodeControl
            {
                DataContext = node
            };

            control.AddHandler(
                PointerPressedEvent,
                OnNodePressed,
                RoutingStrategies.Bubble,
                handledEventsToo: true);

            control.PortPreviewStarted += OnPortPreviewStarted;
            control.PortPreviewMoved += OnPortPreviewMoved;
            control.PortPreviewEnded += OnPortPreviewEnded;

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
                OnEdgePressed,
                RoutingStrategies.Bubble,
                handledEventsToo: true);

            _edges.Add(control);
            canvas.Children.Insert(0, control);
        }

        // S18 fix : après reconstruction, recalculer la géométrie des edges
        // une fois le layout des nodes réellement stabilisé.
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var edge in _edges)
                edge.RefreshGeometry();
        }, DispatcherPriority.Loaded);
    }

    // =========================================================
    // Interactions canvas / sélection
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
                    OnNodePressed,
                    RoutingStrategies.Bubble,
                    handledEventsToo: true);

                newNode.PortPreviewStarted += OnPortPreviewStarted;
                newNode.PortPreviewMoved += OnPortPreviewMoved;
                newNode.PortPreviewEnded += OnPortPreviewEnded;

                Canvas.SetLeft(newNode, ((Node)newNode.DataContext!).X);
                Canvas.SetTop(newNode, ((Node)newNode.DataContext!).Y);
            },
            newNode => _history.Execute(new CreateNodeCommand(canvas, newNode))
        );

        SyncCurrentDocument();
        RefreshInspector();
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
        // Rien à faire ici : le commit du lien est géré par la fin de preview
    }

    private void OnNodePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        Focus();
        e.Handled = true;

        var node = (NodeControl)sender!;
        SetSelection(node);
    }

    private void OnEdgePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        Focus();
        e.Handled = true;

        var edge = (EdgeControl)sender!;
        SetSelection(edge);
    }

    private void SetSelection(NodeControl node)
    {
        var currentNode = GetSelectedNode();
        if (ReferenceEquals(currentNode, node))
            return;

        ClearSelectionVisualOnly();

        _selectionService.SelectNode(node);
        node.SetSelected(true);

        RefreshInspector();
    }

    private void SetSelection(EdgeControl edge)
    {
        var currentEdge = GetSelectedEdge();
        if (ReferenceEquals(currentEdge, edge))
            return;

        ClearSelectionVisualOnly();

        _selectionService.SelectEdge(edge);
        edge.SetSelected(true);

        RefreshInspector();
    }

    private void ClearSelectionVisualOnly()
    {
        var selectedNode = GetSelectedNode();
        if (selectedNode != null)
            selectedNode.SetSelected(false);

        var selectedEdge = GetSelectedEdge();
        if (selectedEdge != null)
            selectedEdge.SetSelected(false);
    }

    private void ClearSelection()
    {
        ClearSelectionVisualOnly();
        _selectionService.ClearSelection();
        RefreshInspector();
    }

    // =========================================================
    // Inspecteur (R2.C)
    // =========================================================
    private void RefreshInspector()
    {
        if (!_uiReady)
            return;

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

        ApplyInspectorState(state);
    }

    private void ApplyInspectorState(InspectorState state)
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

        nodeTextBox.IsEnabled = state.NodeSectionEnabled;
        nodeLabelButton.IsEnabled = state.NodeSectionEnabled;
        nodeStyleCombo.IsEnabled = state.NodeSectionEnabled;
        nodeStyleButton.IsEnabled = state.NodeSectionEnabled;

        nodeTextBox.Text = state.NodeLabel;
        nodeStyleCombo.SelectedIndex = state.NodeStyleIndex;

        edgeTextBox.IsEnabled = state.EdgeSectionEnabled;
        edgeLabelButton.IsEnabled = state.EdgeSectionEnabled;
        edgeStyleCombo.IsEnabled = state.EdgeSectionEnabled;
        edgeDirectionCombo.IsEnabled = state.EdgeSectionEnabled;
        edgeStyleButton.IsEnabled = state.EdgeSectionEnabled;

        edgeTextBox.Text = state.EdgeLabel;
        edgeStyleCombo.SelectedIndex = state.EdgeStyleIndex;
        edgeDirectionCombo.SelectedIndex = state.EdgeDirectionIndex;
    }

    // =========================================================
    // Apply depuis l’inspecteur
    // =========================================================
    private void OnApplyNodeLabelClicked(object? sender, RoutedEventArgs e)
    {
        _diagramEditingService.UpdateSelectedNodeLabel<NodeControl, Node>(
            GetSelectedNodeLabelTextBox().Text,
            control => control.DataContext as Node,
            node => node.Label,
            (node, newLabel) => _history.Execute(new UpdateNodeLabelCommand(node, node.Label, newLabel))
        );

        SyncCurrentDocument();
        RefreshInspector();
    }

    private void OnApplyNodeStyleClicked(object? sender, RoutedEventArgs e)
    {
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
        _diagramEditingService.UpdateSelectedEdgeLabel<EdgeControl>(
            GetSelectedEdgeLabelTextBox().Text,
            (edge, newLabel) => edge.Label = newLabel
        );

        SyncCurrentDocument();
        RefreshInspector();
    }

    private void OnApplyEdgeStyleClicked(object? sender, RoutedEventArgs e)
    {
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
    // Direction globale
    // =========================================================
    private void OnFlowDirectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suspendFlowDirectionHandling)
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
    // Preview + commit du lien
    // =========================================================
    private void OnPortPreviewStarted(NodeControl source, Point startInWindow)
    {
        _previewSource = source;

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

    private void OnPortPreviewEnded()
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
                    OnEdgePressed,
                    RoutingStrategies.Bubble,
                    handledEventsToo: true);
            },
            edge => _history.Execute(new CreateEdgeCommand(canvas, _edges, edge))
        );

        _previewSource = null;
        SyncCurrentDocument();
        RefreshInspector();
    }

    // =========================================================
    // Export Mermaid
    // =========================================================
    private void OnExportMermaidClicked(object? sender, RoutedEventArgs e)
    {
        var textBox = GetMermaidOutputTextBox();
        var exportModel = BuildFlowchartExportModelFromDocument();
        textBox.Text = _flowchartExportService.Export(exportModel);
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
            SyncCurrentDocument();
            RefreshInspector();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _history.Redo();
            SyncCurrentDocument();
            RefreshInspector();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            if (GetSelectedEdge() != null)
            {
                DeleteSelectedEdge();
                e.Handled = true;
                return;
            }

            DeleteSelectedNode();
            e.Handled = true;
        }
    }

    private void DeleteSelectedNode()
    {
        var canvas = GetEditorCanvas();

        _diagramEditingService.DeleteSelectedNode<NodeControl>(
            beforeDelete: node => node.SetSelected(false),
            executeDelete: node => _history.Execute(new DeleteNodeCommand(canvas, node, _edges))
        );

        SyncCurrentDocument();
        RefreshInspector();
    }

    private void DeleteSelectedEdge()
    {
        var canvas = GetEditorCanvas();

        _diagramEditingService.DeleteSelectedEdge<EdgeControl>(
            beforeDelete: edge => edge.SetSelected(false),
            executeDelete: edge => _history.Execute(new DeleteEdgeCommand(canvas, _edges, edge))
        );

        SyncCurrentDocument();
        RefreshInspector();
    }

    // =========================================================
    // Synchronisation du document courant
    // =========================================================
    private void SyncCurrentDocument()
    {
        if (!_uiReady)
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
