using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using MermaidStudio.Domain.Nodes;
using System.ComponentModel;

namespace MermaidStudio.UI.Avalonia.Controls;

public enum EdgeStyleKind
{
    Default,
    Dashed,
    Thick
}

public enum EdgeDirection
{
    Forward,
    Reverse
}

public sealed class EdgeControl : Canvas
{
    private readonly NodeControl _source;
    private readonly NodeControl _target;
    private readonly Canvas _parentCanvas;

    private readonly Line _hitLine;
    private readonly Line _visibleLine;
    private readonly Polygon _arrowHead;
    private readonly Border _labelBorder;
    private readonly TextBlock _labelText;

    private readonly Node? _sourceModel;
    private readonly Node? _targetModel;

    private bool _detached;
    private bool _selected;

    public NodeControl SourceNode => _source;
    public NodeControl TargetNode => _target;

    private string _label = string.Empty;
    public string Label
    {
        get => _label;
        set
        {
            _label = value ?? string.Empty;
            _labelText.Text = _label;
            UpdateVisual();
        }
    }

    private EdgeStyleKind _styleKind = EdgeStyleKind.Default;
    public EdgeStyleKind StyleKind
    {
        get => _styleKind;
        set
        {
            _styleKind = value;
            UpdateVisual();
        }
    }

    private EdgeDirection _direction = EdgeDirection.Forward;
    public EdgeDirection Direction
    {
        get => _direction;
        set
        {
            _direction = value;
            UpdateVisual();
        }
    }

    public EdgeControl(Canvas parentCanvas, NodeControl source, NodeControl target)
    {
        _parentCanvas = parentCanvas;
        _source = source;
        _target = target;

        _sourceModel = _source.DataContext as Node;
        _targetModel = _target.DataContext as Node;

        // L'edge couvre le canvas parent (une seule fois, sans boucle de layout)
        Width = Math.Max(1, _parentCanvas.Bounds.Width);
        Height = Math.Max(1, _parentCanvas.Bounds.Height);
        ClipToBounds = false;

        // Ligne invisible mais cliquable
        _hitLine = new Line
        {
            Stroke = Brushes.Transparent,
            StrokeThickness = 12,
            IsHitTestVisible = true
        };

        // Ligne visible
        _visibleLine = new Line
        {
            Stroke = Brushes.White,
            StrokeThickness = 2,
            IsHitTestVisible = false
        };

        // Tête de flèche
        _arrowHead = new Polygon
        {
            Fill = Brushes.White,
            Stroke = Brushes.White,
            StrokeThickness = 1,
            IsHitTestVisible = false
        };

        // Label d'edge
        _labelText = new TextBlock
        {
            Foreground = Brushes.White,
            Text = string.Empty,
            IsHitTestVisible = false
        };

        _labelBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 2),
            IsVisible = false,
            IsHitTestVisible = false,
            Child = _labelText
        };

        Children.Add(_hitLine);
        Children.Add(_visibleLine);
        Children.Add(_arrowHead);
        Children.Add(_labelBorder);

        Attach();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        UpdateVisual();
    }

    public void Attach()
    {
        if (!_detached)
        {
            if (_sourceModel != null)
                _sourceModel.PropertyChanged -= OnNodeModelPropertyChanged;

            if (_targetModel != null)
                _targetModel.PropertyChanged -= OnNodeModelPropertyChanged;
        }

        if (_sourceModel != null)
            _sourceModel.PropertyChanged += OnNodeModelPropertyChanged;

        if (_targetModel != null)
            _targetModel.PropertyChanged += OnNodeModelPropertyChanged;

        _detached = false;
        UpdateVisual();
    }

    public void Detach()
    {
        if (_detached)
            return;

        if (_sourceModel != null)
            _sourceModel.PropertyChanged -= OnNodeModelPropertyChanged;

        if (_targetModel != null)
            _targetModel.PropertyChanged -= OnNodeModelPropertyChanged;

        _detached = true;
    }

    private void OnNodeModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_detached)
            return;

        if (e.PropertyName == nameof(Node.X) || e.PropertyName == nameof(Node.Y))
        {
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        if (_detached)
            return;

        var p1 = _source.GetRightPortCenter(_parentCanvas);
        var p2 = _target.GetRightPortCenter(_parentCanvas);

        _hitLine.StartPoint = p1;
        _hitLine.EndPoint = p2;

        _visibleLine.StartPoint = p1;
        _visibleLine.EndPoint = p2;

        ApplyLineStyle();
        ApplyArrowVisual(p1, p2);
        ApplyLabelVisual(p1, p2);
    }

    private void ApplyLineStyle()
    {
        var stroke = _selected ? Brushes.DodgerBlue : Brushes.White;
        _visibleLine.Stroke = stroke;
        _arrowHead.Fill = stroke;
        _arrowHead.Stroke = stroke;

        switch (_styleKind)
        {
            case EdgeStyleKind.Default:
                _visibleLine.StrokeThickness = 2;
                _visibleLine.StrokeDashArray = null;
                break;

            case EdgeStyleKind.Dashed:
                _visibleLine.StrokeThickness = 2;
                _visibleLine.StrokeDashArray = new AvaloniaList<double> { 6, 4 };
                break;

            case EdgeStyleKind.Thick:
                _visibleLine.StrokeThickness = 4;
                _visibleLine.StrokeDashArray = null;
                break;
        }
    }

    private void ApplyArrowVisual(Point sourcePoint, Point targetPoint)
    {
        var arrowTip = _direction == EdgeDirection.Forward ? targetPoint : sourcePoint;
        var arrowTail = _direction == EdgeDirection.Forward ? sourcePoint : targetPoint;

        var dx = arrowTip.X - arrowTail.X;
        var dy = arrowTip.Y - arrowTail.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 0.001)
        {
            _arrowHead.Points = new AvaloniaList<Point>();
            return;
        }

        var ux = dx / length;
        var uy = dy / length;

        var px = -uy;
        var py = ux;

        var arrowLength = _styleKind == EdgeStyleKind.Thick ? 14 : 12;
        var arrowWidth = _styleKind == EdgeStyleKind.Thick ? 8 : 6;

        var basePoint = new Point(
            arrowTip.X - ux * arrowLength,
            arrowTip.Y - uy * arrowLength);

        var leftPoint = new Point(
            basePoint.X + px * arrowWidth,
            basePoint.Y + py * arrowWidth);

        var rightPoint = new Point(
            basePoint.X - px * arrowWidth,
            basePoint.Y - py * arrowWidth);

        _arrowHead.Points = new AvaloniaList<Point>
        {
            arrowTip,
            leftPoint,
            rightPoint
        };
    }

    private void ApplyLabelVisual(Point p1, Point p2)
    {
        if (string.IsNullOrWhiteSpace(_label))
        {
            _labelBorder.IsVisible = false;
            return;
        }

        _labelBorder.IsVisible = true;
        _labelBorder.BorderBrush = _selected
            ? Brushes.DodgerBlue
            : new SolidColorBrush(Color.Parse("#555555"));

        var midX = (p1.X + p2.X) / 2;
        var midY = (p1.Y + p2.Y) / 2;

        _labelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _labelBorder.DesiredSize;

        SetLeft(_labelBorder, midX - size.Width / 2);
        SetTop(_labelBorder, midY - size.Height / 2);
    }
}
