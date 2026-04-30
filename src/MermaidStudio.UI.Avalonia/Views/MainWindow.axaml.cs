using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using MermaidStudio.Domain.Nodes;
using MermaidStudio.UI.Avalonia.Controls;
using System.Text;

namespace MermaidStudio.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    private NodeControl? _selectedNode;
    private NodeControl? _previewSource;
    private Line? _previewLine;

    // S5 : stock minimal des edges créés
    private readonly List<EdgeControl> _edges = new();

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        // Important pour recevoir Delete ensuite
        Focus();

        var canvas = (Canvas)sender!;

        // S3 : Shift + clic sur le canvas = désélection uniquement
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            ClearSelection();
            return;
        }

        // Important : si un node/port a déjà géré l’événement, le canvas ne fait rien
        if (e.Handled)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var posCanvas = e.GetPosition(canvas);

        var nodeModel = new Node
        {
            Label = "Node",
            X = posCanvas.X,
            Y = posCanvas.Y
        };

        var newNode = new NodeControl
        {
            DataContext = nodeModel
        };

        // S3 : sélection
        newNode.AddHandler(
            PointerPressedEvent,
            OnNodePressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        // S4/S5 : preview / commit
        newNode.PortPreviewStarted += OnPortPreviewStarted;
        newNode.PortPreviewMoved += OnPortPreviewMoved;
        newNode.PortPreviewEnded += OnPortPreviewEnded;

        Canvas.SetLeft(newNode, nodeModel.X);
        Canvas.SetTop(newNode, nodeModel.Y);

        canvas.Children.Add(newNode);
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
        // En S5/S6/S7/S8, le release du canvas ne fait rien de plus :
        // le commit se fait via la fin de preview (PortPreviewEnded).
    }

    private void OnNodePressed(object? sender, PointerPressedEventArgs e)
    {
        // S3 : sélection seulement avec Shift + clic
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;

        e.Handled = true;

        var node = (NodeControl)sender!;
        SetSelection(node);
    }

    private void SetSelection(NodeControl node)
    {
        if (_selectedNode == node)
            return;

        ClearSelection();

        _selectedNode = node;
        _selectedNode.SetSelected(true);

        if (_selectedNode.DataContext is Node selectedModel)
        {
            var textBox = this.FindControl<TextBox>("SelectedNodeLabelTextBox");
            var button = this.FindControl<Button>("ApplyNodeLabelButton");

            textBox.IsEnabled = true;
            button.IsEnabled = true;
            textBox.Text = selectedModel.Label;
        }
    }

    private void ClearSelection()
    {
        if (_selectedNode != null)
        {
            _selectedNode.SetSelected(false);
            _selectedNode = null;
        }

        var textBox = this.FindControl<TextBox>("SelectedNodeLabelTextBox");
        var button = this.FindControl<Button>("ApplyNodeLabelButton");

        textBox.IsEnabled = false;
        button.IsEnabled = false;
        textBox.Text = string.Empty;
    }

    private void OnApplyNodeLabelClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedNode?.DataContext is not Node selectedModel)
            return;

        var textBox = this.FindControl<TextBox>("SelectedNodeLabelTextBox");
        var newLabel = textBox.Text?.Trim();

        selectedModel.Label = string.IsNullOrWhiteSpace(newLabel)
            ? "Node"
            : newLabel;
    }

    // =============================
    // S4/S5 — Preview + Commit
    // =============================
    private void OnPortPreviewStarted(NodeControl source, Point startInWindow)
    {
        _previewSource = source;

        var canvas = this.FindControl<Canvas>("EditorCanvas");
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

        var canvas = this.FindControl<Canvas>("EditorCanvas");
        var canvasOrigin = canvas.TranslatePoint(new Point(0, 0), this);

        if (canvasOrigin == null)
            return;

        _previewLine.EndPoint = new Point(
            currentInWindow.X - canvasOrigin.Value.X,
            currentInWindow.Y - canvasOrigin.Value.Y);
    }

    private void OnPortPreviewEnded()
    {
        var canvas = this.FindControl<Canvas>("EditorCanvas");

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
                var edge = new EdgeControl(canvas, _previewSource, targetNode);
                _edges.Add(edge);
                canvas.Children.Insert(0, edge);
            }
        }

        _previewSource = null;
    }

    // =============================
    // S6.A / S6.B — Export Mermaid flowchart
    // =============================
    private void OnExportMermaidClicked(object? sender, RoutedEventArgs e)
    {
        var textBox = this.FindControl<TextBox>("MermaidOutputTextBox");
        textBox.Text = BuildFlowchartMermaid();
    }

    private string BuildFlowchartMermaid()
    {
        var canvas = this.FindControl<Canvas>("EditorCanvas");

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
            sb.AppendLine($"    {node.Id.Value}[\"{Escape(node.Label)}\"]");
        }

        foreach (var edge in _edges
                     .OrderBy(e => (e.SourceNode.DataContext as Node)?.Id.Value, StringComparer.Ordinal)
                     .ThenBy(e => (e.TargetNode.DataContext as Node)?.Id.Value, StringComparer.Ordinal))
        {
            var sourceNode = edge.SourceNode.DataContext as Node;
            var targetNode = edge.TargetNode.DataContext as Node;

            if (sourceNode == null || targetNode == null)
                continue;

            sb.AppendLine($"    {sourceNode.Id.Value} --> {targetNode.Id.Value}");
        }

        return sb.ToString();
    }

    private string GetSelectedFlowDirection()
    {
        var combo = this.FindControl<ComboBox>("FlowDirectionComboBox");

        if (combo.SelectedItem is ComboBoxItem item &&
            item.Content is string value &&
            (value == "LR" || value == "TB" || value == "RL" || value == "BT"))
        {
            return value;
        }

        return "LR";
    }

    private static string Escape(string value)
        => value.Replace("\"", "\\\"");

    // =============================
    // S8 — Suppression propre
    // =============================
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete && e.Key != Key.Back)
            return;

        DeleteSelectedNode();
        e.Handled = true;
    }

    private void DeleteSelectedNode()
    {
        if (_selectedNode == null)
            return;

        var canvas = this.FindControl<Canvas>("EditorCanvas");
        var nodeToDelete = _selectedNode;

        // Supprime d'abord tous les edges liés au node, avec détachement propre
        for (int i = _edges.Count - 1; i >= 0; i--)
        {
            var edge = _edges[i];
            if (ReferenceEquals(edge.SourceNode, nodeToDelete) ||
                ReferenceEquals(edge.TargetNode, nodeToDelete))
            {
                edge.Detach();
                canvas.Children.Remove(edge);
                _edges.RemoveAt(i);
            }
        }

        // Supprime ensuite le node
        canvas.Children.Remove(nodeToDelete);

        // Nettoyage sélection + panneau
        ClearSelection();
    }
}
