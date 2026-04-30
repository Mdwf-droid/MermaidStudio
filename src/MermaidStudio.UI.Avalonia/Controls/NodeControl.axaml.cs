using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using MermaidStudio.Domain.Nodes;
using System.ComponentModel;

namespace MermaidStudio.UI.Avalonia.Controls;

public enum NodeAnchorSide
{
    Left,
    Right,
    Top,
    Bottom
}

public partial class NodeControl : UserControl
{
    private bool _dragging;
    private bool _previewDragging;
    private Point _startMouse;
    private double _startLeft;
    private double _startTop;

    private Node? _node;

    private Border? _rootBorder;
    private Polygon? _decisionShape;
    private Ellipse? _circleShape;

    private bool _isSelected;

    // Preview / linking
    public event Action<NodeControl, Point>? PortPreviewStarted;
    public event Action<Point>? PortPreviewMoved;
    public event Action? PortPreviewEnded;

    public NodeControl()
    {
        AvaloniaXamlLoader.Load(this);

        _rootBorder = this.FindControl<Border>("RootBorder");
        _decisionShape = this.FindControl<Polygon>("DecisionShape");
        _circleShape = this.FindControl<Ellipse>("CircleShape");

        DataContextChanged += (_, _) =>
        {
            if (_node != null)
                _node.PropertyChanged -= OnNodePropertyChanged;

            _node = DataContext as Node;

            if (_node != null)
                _node.PropertyChanged += OnNodePropertyChanged;

            UpdateVisualStyle();
        };

        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble);
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Node.VisualStyle))
        {
            UpdateVisualStyle();
        }
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        ApplySelectionVisual();
    }

    private void UpdateVisualStyle()
    {
        if (_rootBorder == null || _decisionShape == null || _circleShape == null)
            return;

        var style = _node?.VisualStyle ?? NodeVisualStyle.Rectangle;

        _rootBorder.IsVisible = false;
        _decisionShape.IsVisible = false;
        _circleShape.IsVisible = false;

        switch (style)
        {
            case NodeVisualStyle.Rectangle:
                _rootBorder.IsVisible = true;
                _rootBorder.CornerRadius = new CornerRadius(0);
                break;

            case NodeVisualStyle.Rounded:
                _rootBorder.IsVisible = true;
                _rootBorder.CornerRadius = new CornerRadius(12);
                break;

            case NodeVisualStyle.Decision:
                _decisionShape.IsVisible = true;
                break;

            case NodeVisualStyle.Circle:
                _circleShape.IsVisible = true;
                break;
        }

        ApplySelectionVisual();
    }

    private void ApplySelectionVisual()
    {
        IBrush stroke = _isSelected
            ? Brushes.DodgerBlue
            : new SolidColorBrush(Color.Parse("#6A6A6A"));

        if (_rootBorder != null)
            _rootBorder.BorderBrush = stroke;

        if (_decisionShape != null)
            _decisionShape.Stroke = stroke;

        if (_circleShape != null)
            _circleShape.Stroke = stroke;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_node == null)
            return;

        if (_previewDragging)
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
        if (_dragging && _node != null)
        {
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
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    // =============================
    // Preview / linking depuis le port droit
    // =============================
    private void OnRightPortPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        _previewDragging = true;
        PortPreviewStarted?.Invoke(this, e.GetPosition(null));
        e.Handled = true;
    }

    private void OnRightPortMoved(object? sender, PointerEventArgs e)
    {
        if (!_previewDragging)
            return;

        PortPreviewMoved?.Invoke(e.GetPosition(null));
        e.Handled = true;
    }

    private void OnRightPortReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_previewDragging)
            return;

        _previewDragging = false;
        PortPreviewEnded?.Invoke();
        e.Handled = true;
    }

    // =============================
    // Helpers géométriques
    // =============================
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

        var translated = port.TranslatePoint(localCenter, relativeTo);
        return translated ?? default;
    }

    public bool IsPointInsideNode(Point pointRelativeTo, Visual relativeTo)
    {
        var topLeft = this.TranslatePoint(new Point(0, 0), relativeTo);
        if (topLeft == null)
            return false;

        var rect = new Rect(topLeft.Value, Bounds.Size);
        return rect.Contains(pointRelativeTo);
    }

    public Point GetCenter(Visual relativeTo)
    {
        var centerLocal = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var translated = this.TranslatePoint(centerLocal, relativeTo);
        return translated ?? default;
    }

    public Point GetAnchorPoint(NodeAnchorSide side, Visual relativeTo)
    {
        Point localPoint = side switch
        {
            NodeAnchorSide.Left => new Point(0, Bounds.Height / 2),
            NodeAnchorSide.Right => new Point(Bounds.Width, Bounds.Height / 2),
            NodeAnchorSide.Top => new Point(Bounds.Width / 2, 0),
            NodeAnchorSide.Bottom => new Point(Bounds.Width / 2, Bounds.Height),
            _ => new Point(Bounds.Width, Bounds.Height / 2)
        };

        var translated = this.TranslatePoint(localPoint, relativeTo);
        return translated ?? default;
    }
}
