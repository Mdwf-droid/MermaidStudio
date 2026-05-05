using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using MermaidStudio.Domain.States;
using System.ComponentModel;

namespace MermaidStudio.UI.Avalonia.Controls;

public partial class StateNodeControl : UserControl
{
    private const double MinNodeWidth = 140;
    private const double MinNodeHeight = 58;
    private const double MaxTextWidth = 170;
    private const double HorizontalPadding = 28;
    private const double VerticalPadding = 20;

    private bool _dragging;
    private bool _previewDragging;
    private Point _startMouse;
    private double _startLeft;
    private double _startTop;

    private StateNode? _stateNode;

    private Border? _normalBorder;
    private Ellipse? _startShape;
    private Grid? _endShape;
    private TextBlock? _labelText;

    private bool _isSelected;

    public event Action<StateNodeControl, Point>? PortPreviewStarted;
    public event Action<Point>? PortPreviewMoved;
    public event Action? PortPreviewEnded;

    public StateNodeControl()
    {
        AvaloniaXamlLoader.Load(this);

        _normalBorder = this.FindControl<Border>("NormalBorder");
        _startShape = this.FindControl<Ellipse>("StartShape");
        _endShape = this.FindControl<Grid>("EndShape");
        _labelText = this.FindControl<TextBlock>("LabelText");

        DataContextChanged += (_, _) =>
        {
            if (_stateNode != null)
                _stateNode.PropertyChanged -= OnStateNodePropertyChanged;

            _stateNode = DataContext as StateNode;

            if (_stateNode != null)
                _stateNode.PropertyChanged += OnStateNodePropertyChanged;

            UpdateVisualKind();
        };

        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble);
    }

    private void OnStateNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StateNode.Kind) ||
            e.PropertyName == nameof(StateNode.Label))
        {
            UpdateVisualKind();
        }
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        ApplySelectionVisual();
    }

    private void UpdateVisualKind()
    {
        if (_normalBorder == null || _startShape == null || _endShape == null || _labelText == null)
            return;

        var kind = _stateNode?.Kind ?? StateNodeKind.Normal;

        _normalBorder.IsVisible = false;
        _startShape.IsVisible = false;
        _endShape.IsVisible = false;

        switch (kind)
        {
            case StateNodeKind.Start:
                Width = 34;
                Height = 34;
                _startShape.IsVisible = true;
                _labelText.IsVisible = false;
                break;

            case StateNodeKind.End:
                Width = 44;
                Height = 44;
                _endShape.IsVisible = true;
                _labelText.IsVisible = false;
                break;

            case StateNodeKind.Normal:
            default:
                _labelText.IsVisible = true;
                _labelText.MaxWidth = MaxTextWidth;
                _labelText.Text = _stateNode?.Label ?? string.Empty;
                _labelText.Measure(new Size(MaxTextWidth, double.PositiveInfinity));

                var labelSize = _labelText.DesiredSize;
                Width = Math.Max(MinNodeWidth, labelSize.Width + HorizontalPadding);
                Height = Math.Max(MinNodeHeight, labelSize.Height + VerticalPadding);

                _normalBorder.IsVisible = true;
                break;
        }

        ApplySelectionVisual();
    }

    private void ApplySelectionVisual()
    {
        var stroke = _isSelected
            ? Brushes.DodgerBlue
            : Brushes.White;

        if (_normalBorder != null)
            _normalBorder.BorderBrush = stroke;

        if (_startShape != null)
        {
            _startShape.Fill = stroke;
            _startShape.Stroke = stroke;
        }

        if (_endShape != null)
        {
            foreach (var child in _endShape.Children.OfType<Ellipse>())
                child.Stroke = stroke;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_stateNode == null)
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
            _startLeft = _stateNode.X;

        if (double.IsNaN(_startTop))
            _startTop = _stateNode.Y;

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging && _stateNode != null)
        {
            var current = e.GetPosition(null);
            var dx = current.X - _startMouse.X;
            var dy = current.Y - _startMouse.Y;

            var newLeft = _startLeft + dx;
            var newTop = _startTop + dy;

            Canvas.SetLeft(this, newLeft);
            Canvas.SetTop(this, newTop);

            _stateNode.X = newLeft;
            _stateNode.Y = newTop;
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
