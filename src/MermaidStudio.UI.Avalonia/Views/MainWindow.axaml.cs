using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using MermaidStudio.Application.Editing;
using MermaidStudio.Application.Export;
using MermaidStudio.Domain.Nodes;
using MermaidStudio.UI.Avalonia.Controls;
using MermaidStudio.UI.Avalonia.Editing;

namespace MermaidStudio.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly CommandHistory _history = new();
    private readonly FlowchartExportService _flowchartExportService = new();

    // ✅ R1.B : source de vérité de la sélection
    private readonly SelectionService _selectionService = new();

    // ✅ R1.C : orchestration des actions d’édition
    private readonly DiagramEditingService _diagramEditingService;

    private NodeControl? _previewSource;
    private Line? _previewLine;

    private readonly List<EdgeControl> _edges = new();

    // S15 : source de vérité locale pour la direction globale
    private DiagramFlowDirection _currentDiagramFlowDirection = DiagramFlowDirection.LR;

    public MainWindow()
    {
        _diagramEditingService = new DiagramEditingService(_selectionService);
        AvaloniaXamlLoader.Load(this);
    }

    private Canvas GetEditorCanvas()
        => this.FindControl<Canvas>("EditorCanvas")
           ?? throw new InvalidOperationException("EditorCanvas introuvable dans MainWindow.");

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

        e.Handled = true;

        var node = (NodeControl)sender!;
        SetSelection(node);
    }

    private void OnEdgePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

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

    private void RefreshInspector()
    {
        var canvas = GetEditorCanvas();

        var selectedNode = GetSelectedNode();
        var selectedEdge = GetSelectedEdge();

        var nodeTextBox = GetSelectedNodeLabelTextBox();
        var nodeLabelButton = GetApplyNodeLabelButton();
        var nodeStyleCombo = GetSelectedNodeStyleComboBox();
        var nodeStyleButton = GetApplyNodeStyleButton();

        var edgeTextBox = GetSelectedEdgeLabelTextBox();
        var edgeLabelButton = GetApplyEdgeLabelButton();
        var edgeStyleCombo = GetSelectedEdgeStyleComboBox();
        var edgeDirectionCombo = GetSelectedEdgeDirectionComboBox();
        var edgeStyleButton = GetApplyEdgeStyleButton();

        // Node sélectionné
        if (selectedNode?.DataContext is Node selectedNodeModel &&
            canvas.Children.Contains(selectedNode))
        {
            nodeTextBox.IsEnabled = true;
            nodeLabelButton.IsEnabled = true;
            nodeTextBox.Text = selectedNodeModel.Label;

            nodeStyleCombo.IsEnabled = true;
            nodeStyleButton.IsEnabled = true;
            nodeStyleCombo.SelectedIndex = selectedNodeModel.VisualStyle switch
            {
                NodeVisualStyle.Rectangle => 0,
                NodeVisualStyle.Rounded => 1,
                NodeVisualStyle.Decision => 2,
                NodeVisualStyle.Circle => 3,
                _ => 0
            };
        }
        else
        {
            nodeTextBox.IsEnabled = false;
            nodeLabelButton.IsEnabled = false;
            nodeTextBox.Text = string.Empty;

            nodeStyleCombo.IsEnabled = false;
            nodeStyleButton.IsEnabled = false;
            nodeStyleCombo.SelectedIndex = 0;

            if (selectedNode != null && !canvas.Children.Contains(selectedNode))
                _selectionService.ClearSelection();
        }

        // Edge sélectionné
        if (selectedEdge != null && canvas.Children.Contains(selectedEdge))
        {
            edgeTextBox.IsEnabled = true;
            edgeLabelButton.IsEnabled = true;
            edgeTextBox.Text = selectedEdge.Label;

            edgeStyleCombo.IsEnabled = true;
            edgeDirectionCombo.IsEnabled = true;
            edgeStyleButton.IsEnabled = true;

            edgeStyleCombo.SelectedIndex = selectedEdge.StyleKind switch
            {
                EdgeStyleKind.Default => 0,
                EdgeStyleKind.Dashed => 1,
                EdgeStyleKind.Thick => 2,
                _ => 0
            };

            edgeDirectionCombo.SelectedIndex = selectedEdge.Direction switch
            {
                EdgeDirection.Forward => 0,
                EdgeDirection.Reverse => 1,
                _ => 0
            };
        }
        else
        {
            edgeTextBox.IsEnabled = false;
            edgeLabelButton.IsEnabled = false;
            edgeTextBox.Text = string.Empty;

            edgeStyleCombo.IsEnabled = false;
            edgeDirectionCombo.IsEnabled = false;
            edgeStyleButton.IsEnabled = false;
            edgeStyleCombo.SelectedIndex = 0;
            edgeDirectionCombo.SelectedIndex = 0;

            if (selectedEdge != null && !canvas.Children.Contains(selectedEdge))
                _selectionService.ClearSelection();
        }
    }

    private void OnApplyNodeLabelClicked(object? sender, RoutedEventArgs e)
    {
        _diagramEditingService.UpdateSelectedNodeLabel<NodeControl, Node>(
            GetSelectedNodeLabelTextBox().Text,
            control => control.DataContext as Node,
            node => node.Label,
            (node, newLabel) => _history.Execute(new UpdateNodeLabelCommand(node, node.Label, newLabel))
        );

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

        RefreshInspector();
    }

    private void OnApplyEdgeLabelClicked(object? sender, RoutedEventArgs e)
    {
        _diagramEditingService.UpdateSelectedEdgeLabel<EdgeControl>(
            GetSelectedEdgeLabelTextBox().Text,
            (edge, newLabel) => edge.Label = newLabel
        );

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

        RefreshInspector();
    }

    private void OnFlowDirectionChanged(object? sender, SelectionChangedEventArgs e)
    {
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

            foreach (var edge in _edges)
            {
                edge.DiagramDirection = _currentDiagramFlowDirection;
            }
        }
    }

    // =============================
    // Preview + commit du lien
    // =============================
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
        RefreshInspector();
    }

    // =============================
    // Export Mermaid (R1.A)
    // =============================
    private void OnExportMermaidClicked(object? sender, RoutedEventArgs e)
    {
        var textBox = GetMermaidOutputTextBox();
        var exportModel = BuildFlowchartExportModel();
        textBox.Text = _flowchartExportService.Export(exportModel);
    }

    private FlowchartExportModel BuildFlowchartExportModel()
    {
        var canvas = GetEditorCanvas();

        var model = new FlowchartExportModel
        {
            Direction = _currentDiagramFlowDirection switch
            {
                DiagramFlowDirection.TB => FlowchartExportDiagramDirection.TB,
                DiagramFlowDirection.RL => FlowchartExportDiagramDirection.RL,
                DiagramFlowDirection.BT => FlowchartExportDiagramDirection.BT,
                _ => FlowchartExportDiagramDirection.LR
            }
        };

        foreach (var node in canvas.Children
                     .OfType<NodeControl>()
                     .Select(n => n.DataContext as Node)
                     .Where(n => n != null)
                     .Cast<Node>())
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

        foreach (var edge in _edges)
        {
            var sourceNode = edge.SourceNode.DataContext as Node;
            var targetNode = edge.TargetNode.DataContext as Node;

            if (sourceNode == null || targetNode == null)
                continue;

            model.Edges.Add(new FlowchartExportEdge
            {
                SourceId = sourceNode.Id.Value,
                TargetId = targetNode.Id.Value,
                Label = edge.Label,
                Style = edge.StyleKind switch
                {
                    EdgeStyleKind.Dashed => FlowchartExportEdgeStyle.Dashed,
                    EdgeStyleKind.Thick => FlowchartExportEdgeStyle.Thick,
                    _ => FlowchartExportEdgeStyle.Default
                },
                Direction = edge.Direction == EdgeDirection.Reverse
                    ? FlowchartExportEdgeDirection.Reverse
                    : FlowchartExportEdgeDirection.Forward
            });
        }

        return model;
    }

    // =============================
    // Suppression + Undo/Redo
    // =============================
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _history.Undo();
            RefreshInspector();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _history.Redo();
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

        RefreshInspector();
    }

    private void DeleteSelectedEdge()
    {
        var canvas = GetEditorCanvas();

        _diagramEditingService.DeleteSelectedEdge<EdgeControl>(
            beforeDelete: edge => edge.SetSelected(false),
            executeDelete: edge => _history.Execute(new DeleteEdgeCommand(canvas, _edges, edge))
        );

        RefreshInspector();
    }
}
