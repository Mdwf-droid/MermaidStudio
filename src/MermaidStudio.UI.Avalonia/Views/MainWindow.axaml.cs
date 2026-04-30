using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using MermaidStudio.Domain.Nodes;
using MermaidStudio.UI.Avalonia.Controls;

namespace MermaidStudio.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    private NodeControl? _selectedNode;
    private NodeControl? _previewSource;
    private Line? _previewLine;

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

        // S4 : Ctrl + clic sur le port droit d’un node = début de preview
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var pos = e.GetPosition(canvas);

            if (canvas.InputHitTest(pos) is Visual visual)
            {
                var hitNode = visual.FindAncestorOfType<NodeControl>();
                if (hitNode != null && hitNode.IsOverRightPort(pos, canvas))
                {
                    _previewSource = hitNode;

                    var start = hitNode.GetRightPortCenter(canvas);

                    _previewLine = new Line
                    {
                        StartPoint = start,
                        EndPoint = start,
                        Stroke = Brushes.Orange,
                        StrokeThickness = 2,
                        IsHitTestVisible = false
                    };

                    canvas.Children.Add(_previewLine);
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl sans port cible = pas de création de node
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // si un node a déjà pris l’événement (drag), le canvas ne crée rien
        if (e.Handled)
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

        newNode.AddHandler(
            PointerPressedEvent,
            OnNodePressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

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
        if (_previewLine == null)
            return;

        var canvas = (Canvas)sender!;
        canvas.Children.Remove(_previewLine);

        _previewLine = null;
        _previewSource = null;
    }

    private void OnNodePressed(object? sender, PointerPressedEventArgs e)
    {
        // S3 : la sélection n’existe QUE avec Shift + clic
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
}
