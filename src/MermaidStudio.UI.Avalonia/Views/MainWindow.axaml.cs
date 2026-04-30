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

        if (e.Handled)
            return;

        var canvas = (Canvas)sender!;
        var pos = e.GetPosition(canvas);

        var node = new NodeControl
        {
            DataContext = new Node
            {
                Label = "Node",
                X = pos.X,
                Y = pos.Y
            }
        };

        Canvas.SetLeft(node, pos.X);
        Canvas.SetTop(node, pos.Y);

        canvas.Children.Add(node);
    }
}
