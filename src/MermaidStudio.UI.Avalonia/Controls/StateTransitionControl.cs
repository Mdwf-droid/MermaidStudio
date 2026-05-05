using System.ComponentModel;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using MermaidStudio.Domain.States;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace MermaidStudio.UI.Avalonia.Controls;

public sealed class StateTransitionControl : Canvas
{
    private readonly StateNodeControl _source;
    private readonly StateNodeControl _target;
    private readonly Canvas _parentCanvas;
    private readonly StateTransition _model;

    private readonly PathShape _hitPath;
    private readonly PathShape _visiblePath;
    private readonly Polygon _arrowHead;
    private readonly Border _labelBorder;
    private readonly TextBlock _labelText;

    private bool _selected;

    public StateNodeControl SourceNode => _source;
    public StateNodeControl TargetNode => _target;
    public StateTransition Model => _model;

    public string Label
    {
        get => _model.Label;
        set
        {
            _model.Label = value ?? string.Empty;
            _labelText.Text = _model.Label;
            UpdateVisual();
        }
    }

    public StateTransitionControl(Canvas parentCanvas, StateNodeControl source, StateNodeControl target, StateTransition model)
    {
        _parentCanvas = parentCanvas;
        _source = source;
        _target = target;
        _model = model;

        Width = Math.Max(1, _parentCanvas.Bounds.Width);
        Height = Math.Max(1, _parentCanvas.Bounds.Height);
        ClipToBounds = false;

        _hitPath = new PathShape
        {
            Stroke = Brushes.Transparent,
            StrokeThickness = 12,
            IsHitTestVisible = true
        };

        _visiblePath = new PathShape
        {
            Stroke = Brushes.White,
            StrokeThickness = 2,
            Fill = null,
            IsHitTestVisible = false
        };

        _arrowHead = new Polygon
        {
            Fill = Brushes.White,
            Stroke = Brushes.White,
            StrokeThickness = 1,
            IsHitTestVisible = false
        };

        _labelText = new TextBlock
        {
            Foreground = Brushes.White,
            Text = model.Label,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 180,
            IsHitTestVisible = false
        };

        _labelBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 4),
            IsVisible = false,
            IsHitTestVisible = false,
            Child = _labelText
        };

        Children.Add(_hitPath);
        Children.Add(_visiblePath);
        Children.Add(_arrowHead);
        Children.Add(_labelBorder);

        _model.PropertyChanged += OnModelPropertyChanged;
        AttachNodeBindings();
        UpdateVisual();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        UpdateVisual();
    }

    public void RefreshGeometry()
    {
        UpdateVisual();
    }

    private void AttachNodeBindings()
    {
        if (_source.DataContext is StateNode sourceNode)
            sourceNode.PropertyChanged += OnStateNodePropertyChanged;

        if (_target.DataContext is StateNode targetNode)
            targetNode.PropertyChanged += OnStateNodePropertyChanged;
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StateTransition.Label))
        {
            _labelText.Text = _model.Label;
            UpdateVisual();
        }
    }

    private void OnStateNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StateNode.X) ||
            e.PropertyName == nameof(StateNode.Y) ||
            e.PropertyName == nameof(StateNode.Label))
        {
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        var (startSide, endSide) = ComputeAnchorSides(_source, _target);
        var startAnchor = _source.GetAnchorPoint(startSide, _parentCanvas);
        var endAnchor = _target.GetAnchorPoint(endSide, _parentCanvas);

        var geometry = ComputeGeometry(startAnchor, endAnchor, endSide);

        _hitPath.Data = geometry.PathGeometry;
        _visiblePath.Data = geometry.PathGeometry;

        var stroke = _selected ? Brushes.DodgerBlue : Brushes.White;
        _visiblePath.Stroke = stroke;
        _arrowHead.Fill = stroke;
        _arrowHead.Stroke = stroke;

        ApplyArrowVisual(geometry.ArrowTip, geometry.ArrowDirection);
        ApplyLabelVisual(geometry.LabelPoint, geometry.LabelNormal);
    }

    private void ApplyArrowVisual(Point arrowTip, Vector directionVector)
    {
        var length = directionVector.Length;
        if (length < 0.001)
        {
            _arrowHead.Points = new AvaloniaList<Point>();
            return;
        }

        var ux = directionVector.X / length;
        var uy = directionVector.Y / length;

        var px = -uy;
        var py = ux;

        const double arrowLength = 12;
        const double arrowWidth = 6;

        var baseCenter = new Point(
            arrowTip.X - ux * arrowLength,
            arrowTip.Y - uy * arrowLength);

        var leftPoint = new Point(
            baseCenter.X + px * arrowWidth,
            baseCenter.Y + py * arrowWidth);

        var rightPoint = new Point(
            baseCenter.X - px * arrowWidth,
            baseCenter.Y - py * arrowWidth);

        _arrowHead.Points = new AvaloniaList<Point>
        {
            arrowTip,
            leftPoint,
            rightPoint
        };
    }

    private void ApplyLabelVisual(Point labelPoint, Vector labelNormal)
    {
        if (string.IsNullOrWhiteSpace(_model.Label))
        {
            _labelBorder.IsVisible = false;
            return;
        }

        _labelBorder.IsVisible = true;
        _labelBorder.BorderBrush = _selected
            ? Brushes.DodgerBlue
            : new SolidColorBrush(Color.Parse("#555555"));

        _labelBorder.Measure(new Size(200, double.PositiveInfinity));
        var size = _labelBorder.DesiredSize;

        var normal = Normalize(labelNormal);
        var finalX = labelPoint.X + normal.X * 18;
        var finalY = labelPoint.Y + normal.Y * 18;

        SetLeft(_labelBorder, finalX - size.Width / 2);
        SetTop(_labelBorder, finalY - size.Height / 2);
    }

    private static (NodeAnchorSide StartSide, NodeAnchorSide EndSide)
        ComputeAnchorSides(StateNodeControl startNode, StateNodeControl endNode)
    {
        var startCenter = startNode.GetCenter(startNode.Parent as Visual ?? startNode);
        var endCenter = endNode.GetCenter(endNode.Parent as Visual ?? endNode);

        var dx = endCenter.X - startCenter.X;
        var dy = endCenter.Y - startCenter.Y;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx >= 0
                ? (NodeAnchorSide.Right, NodeAnchorSide.Left)
                : (NodeAnchorSide.Left, NodeAnchorSide.Right);
        }

        return dy >= 0
            ? (NodeAnchorSide.Bottom, NodeAnchorSide.Top)
            : (NodeAnchorSide.Top, NodeAnchorSide.Bottom);
    }

    private static (PathGeometry PathGeometry, Point ArrowTip, Vector ArrowDirection, Point LabelPoint, Vector LabelNormal)
        ComputeGeometry(Point startAnchor, Point endAnchor, NodeAnchorSide endSide)
    {
        var endNormal = GetSideNormal(endSide);
        var arrowTip = endAnchor + endNormal * 8.0;
        var arrowDirection = -endNormal;
        var dirUnit = Normalize(arrowDirection);

        var pathEnd = new Point(
            arrowTip.X - dirUnit.X * 12.0,
            arrowTip.Y - dirUnit.Y * 12.0);

        var dx = pathEnd.X - startAnchor.X;
        var dy = pathEnd.Y - startAnchor.Y;

        Point c1;
        Point c2;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            var handle = Math.Max(Math.Abs(dx) * 0.45, 40.0);
            var sign = dx >= 0 ? 1.0 : -1.0;

            c1 = new Point(startAnchor.X + handle * sign, startAnchor.Y);
            c2 = new Point(pathEnd.X - handle * sign * 0.6, pathEnd.Y);
        }
        else
        {
            var handle = Math.Max(Math.Abs(dy) * 0.45, 40.0);
            var sign = dy >= 0 ? 1.0 : -1.0;

            c1 = new Point(startAnchor.X, startAnchor.Y + handle * sign);
            c2 = new Point(pathEnd.X, pathEnd.Y - handle * sign * 0.6);
        }

        var figure = new PathFigure
        {
            StartPoint = startAnchor,
            IsClosed = false
        };
        figure.Segments.Add(new BezierSegment
        {
            Point1 = c1,
            Point2 = c2,
            Point3 = pathEnd
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        var labelPoint = EvaluateCubicBezier(startAnchor, c1, c2, pathEnd, 0.5);
        var tangent = EvaluateCubicBezierDerivative(startAnchor, c1, c2, pathEnd, 0.5);
        var labelNormal = new Vector(-tangent.Y, tangent.X);

        return (geometry, arrowTip, arrowDirection, labelPoint, labelNormal);
    }

    private static Vector GetSideNormal(NodeAnchorSide side)
    {
        return side switch
        {
            NodeAnchorSide.Left => new Vector(-1, 0),
            NodeAnchorSide.Right => new Vector(1, 0),
            NodeAnchorSide.Top => new Vector(0, -1),
            NodeAnchorSide.Bottom => new Vector(0, 1),
            _ => new Vector(1, 0)
        };
    }

    private static Vector Normalize(Vector v)
    {
        var len = v.Length;
        if (len < 0.001)
            return new Vector(0, -1);

        return new Vector(v.X / len, v.Y / len);
    }

    private static Point EvaluateCubicBezier(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var u = 1.0 - t;
        var uu = u * u;
        var uuu = uu * u;
        var tt = t * t;
        var ttt = tt * t;

        var x =
            uuu * p0.X +
            3 * uu * t * p1.X +
            3 * u * tt * p2.X +
            ttt * p3.X;

        var y =
            uuu * p0.Y +
            3 * uu * t * p1.Y +
            3 * u * tt * p2.Y +
            ttt * p3.Y;

        return new Point(x, y);
    }

    private static Vector EvaluateCubicBezierDerivative(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var u = 1.0 - t;

        var x =
            3 * u * u * (p1.X - p0.X) +
            6 * u * t * (p2.X - p1.X) +
            3 * t * t * (p3.X - p2.X);

        var y =
            3 * u * u * (p1.Y - p0.Y) +
            6 * u * t * (p2.Y - p1.Y) +
            3 * t * t * (p3.Y - p2.Y);

        return new Vector(x, y);
    }
}
