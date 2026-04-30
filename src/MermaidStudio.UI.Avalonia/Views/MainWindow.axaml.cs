using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MermaidStudio.Domain.Nodes;
using MermaidStudio.UI.Avalonia.Controls;

namespace MermaidStudio.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    private NodeControl? _selectedNode;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        // S3 : Shift + clic sur le canvas = désélection uniquement
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            ClearSelection();
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // si un node a déjà pris l’événement (drag), le canvas ne crée rien
        if (e.Handled)
            return;

        var canvas = (Canvas)sender!;
        var pos = e.GetPosition(canvas);

        var nodeModel = new Node
        {
            Label = "Node",
            X = pos.X,
            Y = pos.Y
        };

        var node = new NodeControl
        {
            DataContext = nodeModel
        };

        // IMPORTANT : on écoute aussi les événements déjà Handled par NodeControl
        node.AddHandler(
            PointerPressedEvent,
            OnNodePressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        Canvas.SetLeft(node, nodeModel.X);
        Canvas.SetTop(node, nodeModel.Y);

        canvas.Children.Add(node);
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
