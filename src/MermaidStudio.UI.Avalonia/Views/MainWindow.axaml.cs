using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using MermaidStudio.Domain.Nodes;
using MermaidStudio.UI.Avalonia.Controls;
using MermaidStudio.UI.Avalonia.Editing;
using System.Text;

namespace MermaidStudio.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly CommandHistory _history = new();

    private NodeControl? _selectedNode;
    private EdgeControl? _selectedEdge;

    private NodeControl? _previewSource;
    private Line? _previewLine;

    private readonly List<EdgeControl> _edges = new();

    // ✅ S15 : source de vérité locale pour la direction globale
    private DiagramFlowDirection _currentDiagramFlowDirection = DiagramFlowDirection.LR;

    public MainWindow()
    {
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

        var nodeModel = new Node
        {
            Label = "Node",
            X = posCanvas.X,
            Y = posCanvas.Y,
            VisualStyle = NodeVisualStyle.Rectangle
        };

        var newNode = new NodeControl
        {
            DataContext = nodeModel
        };

        newNode.AddHandler(
            PointerPressedEvent,
            OnNodePressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        newNode.PortPreviewStarted += OnPortPreviewStarted;
        newNode.PortPreviewMoved += OnPortPreviewMoved;
        newNode.PortPreviewEnded += OnPortPreviewEnded;

        Canvas.SetLeft(newNode, nodeModel.X);
        Canvas.SetTop(newNode, nodeModel.Y);

        _history.Execute(new CreateNodeCommand(canvas, newNode));
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
        if (_selectedNode == node)
            return;

        ClearSelection();

        _selectedNode = node;
        _selectedNode.SetSelected(true);

        RefreshInspector();
    }

    private void SetSelection(EdgeControl edge)
    {
        if (_selectedEdge == edge)
            return;

        ClearSelection();

        _selectedEdge = edge;
        _selectedEdge.SetSelected(true);

        RefreshInspector();
    }

    private void ClearSelection()
    {
        if (_selectedNode != null)
        {
            _selectedNode.SetSelected(false);
            _selectedNode = null;
        }

        if (_selectedEdge != null)
        {
            _selectedEdge.SetSelected(false);
            _selectedEdge = null;
        }

        RefreshInspector();
    }

    private void RefreshInspector()
    {
        var canvas = GetEditorCanvas();

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
        if (_selectedNode?.DataContext is Node selectedNodeModel &&
            canvas.Children.Contains(_selectedNode))
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

            if (_selectedNode != null && !canvas.Children.Contains(_selectedNode))
                _selectedNode = null;
        }

        // Edge sélectionné
        if (_selectedEdge != null && canvas.Children.Contains(_selectedEdge))
        {
            edgeTextBox.IsEnabled = true;
            edgeLabelButton.IsEnabled = true;
            edgeTextBox.Text = _selectedEdge.Label;

            edgeStyleCombo.IsEnabled = true;
            edgeDirectionCombo.IsEnabled = true;
            edgeStyleButton.IsEnabled = true;

            edgeStyleCombo.SelectedIndex = _selectedEdge.StyleKind switch
            {
                EdgeStyleKind.Default => 0,
                EdgeStyleKind.Dashed => 1,
                EdgeStyleKind.Thick => 2,
                _ => 0
            };

            edgeDirectionCombo.SelectedIndex = _selectedEdge.Direction switch
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

            if (_selectedEdge != null && !canvas.Children.Contains(_selectedEdge))
                _selectedEdge = null;
        }
    }

    private void OnApplyNodeLabelClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedNode?.DataContext is not Node selectedModel)
            return;

        var textBox = GetSelectedNodeLabelTextBox();
        var newLabel = string.IsNullOrWhiteSpace(textBox.Text?.Trim())
            ? "Node"
            : textBox.Text!.Trim();

        if (selectedModel.Label == newLabel)
            return;

        _history.Execute(new UpdateNodeLabelCommand(selectedModel, selectedModel.Label, newLabel));
        RefreshInspector();
    }

    private void OnApplyNodeStyleClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedNode?.DataContext is not Node selectedModel)
            return;

        var combo = GetSelectedNodeStyleComboBox();

        selectedModel.VisualStyle = combo.SelectedIndex switch
        {
            1 => NodeVisualStyle.Rounded,
            2 => NodeVisualStyle.Decision,
            3 => NodeVisualStyle.Circle,
            _ => NodeVisualStyle.Rectangle
        };

        RefreshInspector();
    }

    private void OnApplyEdgeLabelClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedEdge == null)
            return;

        var textBox = GetSelectedEdgeLabelTextBox();
        var newLabel = textBox.Text?.Trim() ?? string.Empty;

        _selectedEdge.Label = newLabel;
        RefreshInspector();
    }

    private void OnApplyEdgeStyleClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedEdge == null)
            return;

        var styleCombo = GetSelectedEdgeStyleComboBox();
        var directionCombo = GetSelectedEdgeDirectionComboBox();

        _selectedEdge.StyleKind = styleCombo.SelectedIndex switch
        {
            1 => EdgeStyleKind.Dashed,
            2 => EdgeStyleKind.Thick,
            _ => EdgeStyleKind.Default
        };

        _selectedEdge.Direction = directionCombo.SelectedIndex switch
        {
            1 => EdgeDirection.Reverse,
            _ => EdgeDirection.Forward
        };

        RefreshInspector();
    }

    // ✅ S15 : lit directement le sender, plus de FindControl pendant l'init XAML
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

        if (targetNode != null)
        {
            var exists = _edges.Any(edge =>
                ReferenceEquals(edge.SourceNode, _previewSource) &&
                ReferenceEquals(edge.TargetNode, targetNode));

            if (!exists)
            {
                var edge = new EdgeControl(canvas, _previewSource, targetNode)
                {
                    DiagramDirection = _currentDiagramFlowDirection
                };

                edge.AddHandler(
                    PointerPressedEvent,
                    OnEdgePressed,
                    RoutingStrategies.Bubble,
                    handledEventsToo: true);

                _history.Execute(new CreateEdgeCommand(canvas, _edges, edge));
            }
        }

        _previewSource = null;
        RefreshInspector();
    }

    // =============================
    // Export Mermaid flowchart
    // =============================
    private void OnExportMermaidClicked(object? sender, RoutedEventArgs e)
    {
        var textBox = GetMermaidOutputTextBox();
        textBox.Text = BuildFlowchartMermaid();
    }

    private string BuildFlowchartMermaid()
    {
        var canvas = GetEditorCanvas();

        var nodes = canvas.Children
            .OfType<NodeControl>()
            .Select(n => n.DataContext as Node)
            .Where(n => n != null)
            .Cast<Node>()
            .OrderBy(n => n.Id.Value, StringComparer.Ordinal)
            .ToList();

        var direction = GetSelectedFlowDirection();

        var sb = new StringBuilder();
        sb.AppendLine($"flowchart {direction}");

        foreach (var node in nodes)
        {
            sb.AppendLine($"    {FormatNode(node)}");
        }

        foreach (var edge in _edges
                     .OrderBy(e => GetExportSourceNode(e)?.Id.Value, StringComparer.Ordinal)
                     .ThenBy(e => GetExportTargetNode(e)?.Id.Value, StringComparer.Ordinal))
        {
            var sourceNode = GetExportSourceNode(edge);
            var targetNode = GetExportTargetNode(edge);

            if (sourceNode == null || targetNode == null)
                continue;

            var arrowToken = edge.StyleKind switch
            {
                EdgeStyleKind.Dashed => "-.->",
                EdgeStyleKind.Thick => "==>",
                _ => "-->"
            };

            if (string.IsNullOrWhiteSpace(edge.Label))
            {
                sb.AppendLine($"    {sourceNode.Id.Value} {arrowToken} {targetNode.Id.Value}");
            }
            else
            {
                sb.AppendLine($"    {sourceNode.Id.Value} {arrowToken}|{Escape(edge.Label)}| {targetNode.Id.Value}");
            }
        }

        return sb.ToString();
    }

    private string FormatNode(Node node)
    {
        var id = node.Id.Value;
        var label = Escape(node.Label);

        return node.VisualStyle switch
        {
            NodeVisualStyle.Rounded => $"{id}(\"{label}\")",
            NodeVisualStyle.Decision => $"{id}{{\"{label}\"}}",
            NodeVisualStyle.Circle => $"{id}((\"{label}\"))",
            _ => $"{id}[\"{label}\"]"
        };
    }

    private Node? GetExportSourceNode(EdgeControl edge)
    {
        return edge.Direction == EdgeDirection.Forward
            ? edge.SourceNode.DataContext as Node
            : edge.TargetNode.DataContext as Node;
    }

    private Node? GetExportTargetNode(EdgeControl edge)
    {
        return edge.Direction == EdgeDirection.Forward
            ? edge.TargetNode.DataContext as Node
            : edge.SourceNode.DataContext as Node;
    }

    private string GetSelectedFlowDirection()
    {
        return _currentDiagramFlowDirection switch
        {
            DiagramFlowDirection.RL => "RL",
            DiagramFlowDirection.TB => "TB",
            DiagramFlowDirection.BT => "BT",
            _ => "LR"
        };
    }

    private static string Escape(string value)
        => value.Replace("\"", "\\\"");

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
            if (_selectedEdge != null)
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
        if (_selectedNode == null)
            return;

        var canvas = GetEditorCanvas();
        var nodeToDelete = _selectedNode;

        ClearSelection();

        _history.Execute(new DeleteNodeCommand(canvas, nodeToDelete, _edges));
        RefreshInspector();
    }

    private void DeleteSelectedEdge()
    {
        if (_selectedEdge == null)
            return;

        var canvas = GetEditorCanvas();
        var edgeToDelete = _selectedEdge;

        ClearSelection();

        _history.Execute(new DeleteEdgeCommand(canvas, _edges, edgeToDelete));
        RefreshInspector();
    }
}
