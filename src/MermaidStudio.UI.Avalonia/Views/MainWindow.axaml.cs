using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MermaidStudio.Domain.Nodes;
using MermaidStudio.UI.Avalonia.Controls;

namespace MermaidStudio.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // Très important : si un node a déjà géré l’événement, le canvas ne fait rien
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

        Canvas.SetLeft(node, nodeModel.X);
        Canvas.SetTop(node, nodeModel.Y);

        canvas.Children.Add(node);
    }
}
