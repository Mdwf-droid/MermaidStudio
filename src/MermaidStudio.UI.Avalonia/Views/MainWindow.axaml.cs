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
        // S5 : le release du canvas ne fait rien de plus.
        // Le commit se fait via la fin de preview (PortPreviewEnded).
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
    }

    private void ClearSelection()
    {
        if (_selectedNode != null)
        {
            _selectedNode.SetSelected(false);
            _selectedNode = null;
        }
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

        // Position finale = extrémité de la preview dans le canvas
        var releasePosInCanvas = _previewLine.EndPoint;

        // Recherche d'une cible valide : un autre NodeControl sous cette position
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

        // Cleanup de la preview d’abord
        canvas.Children.Remove(_previewLine);
        _previewLine = null;

        // Commit réel du lien si cible valide
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
    // S6.A — Export Mermaid flowchart
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
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");

        foreach (var node in nodes)
        {
            sb.AppendLine($"    {node.Id}[\"{Escape(node.Label)}\"]");
        }

        foreach (var edge in _edges)
        {
            var sourceNode = edge.SourceNode.DataContext as Node;
            var targetNode = edge.TargetNode.DataContext as Node;

            if (sourceNode == null || targetNode == null)
                continue;

            sb.AppendLine($"    {sourceNode.Id} --> {targetNode.Id}");
        }

        return sb.ToString();
    }

    private static string Escape(string value)
        => value.Replace("\"", "\\\"");
}
