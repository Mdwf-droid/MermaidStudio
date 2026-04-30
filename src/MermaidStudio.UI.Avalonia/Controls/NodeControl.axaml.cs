using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using MermaidStudio.Domain.Nodes;

namespace MermaidStudio.UI.Avalonia.Controls;

public partial class NodeControl : UserControl
{
    private bool _dragging;
    private Point _startMouse;
    private double _startLeft;
    private double _startTop;
    private Node? _node;
    private Border? _rootBorder;

    public NodeControl()
    {
        AvaloniaXamlLoader.Load(this);

        _rootBorder = this.FindControl<Border>("RootBorder");

        DataContextChanged += (_, _) =>
        {
            _node = DataContext as Node;
        };

        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble);
    }

    public void SetSelected(bool selected)
    {
        if (_rootBorder == null)
            return;

        _rootBorder.BorderBrush = selected
            ? Brushes.DodgerBlue
            : new SolidColorBrush(Color.Parse("#6A6A6A"));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_node == null)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // S4 : Ctrl + clic sur le port droit = ne PAS démarrer le drag node.
        // On laisse le Canvas gérer la preview.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
            IsOverRightPortLocal(e.GetPosition(this)))
        {
            return;
        }

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

    public bool IsOverRightPort(Point pointRelativeTo, Visual relativeTo)
    {
        var port = this.FindControl<Control>("RightPort");
        var topLeft = port.TranslatePoint(new Point(0, 0), relativeTo);
        if (topLeft == null)
            return false;

        var rect = new Rect(topLeft.Value, port.Bounds.Size);
        return rect.Contains(pointRelativeTo);
    }

    public Point GetRightPortCenter(Visual relativeTo)
    {
        var port = this.FindControl<Control>("RightPort");
        var localCenter = new Point(port.Bounds.Width / 2, port.Bounds.Height / 2);
        return port.TranslatePoint(localCenter, relativeTo)!.Value;
    }

    private bool IsOverRightPortLocal(Point pointOnThis)
    {
        var port = this.FindControl<Control>("RightPort");
        var topLeft = port.TranslatePoint(new Point(0, 0), this);
        if (topLeft == null)
            return false;

        var rect = new Rect(topLeft.Value, port.Bounds.Size);
        return rect.Contains(pointOnThis);
    }
}
