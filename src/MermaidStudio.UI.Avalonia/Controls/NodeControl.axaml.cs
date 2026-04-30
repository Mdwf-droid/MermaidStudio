using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MermaidStudio.Domain.Nodes;

namespace MermaidStudio.UI.Avalonia.Controls;

public partial class NodeControl : UserControl
{
    private bool _dragging;
    private Point _startMouse;
    private double _startLeft;
    private double _startTop;
    private Node? _node;

    public NodeControl()
    {
        AvaloniaXamlLoader.Load(this);

        DataContextChanged += (_, _) =>
        {
            _node = DataContext as Node;
        };

        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_node == null)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _dragging = true;
        _startMouse = e.GetPosition(null);
        _startLeft = Canvas.GetLeft(this);
        _startTop = Canvas.GetTop(this);

        if (double.IsNaN(_startLeft))
            _startLeft = _node.X;

        if (double.IsNaN(_startTop))
            _startTop = _node.Y;

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || _node == null)
            return;

        var current = e.GetPosition(null);
        var dx = current.X - _startMouse.X;
        var dy = current.Y - _startMouse.Y;

        var newLeft = _startLeft + dx;
        var newTop = _startTop + dy;

        Canvas.SetLeft(this, newLeft);
        Canvas.SetTop(this, newTop);

        _node.X = newLeft;
        _node.Y = newTop;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }
}
