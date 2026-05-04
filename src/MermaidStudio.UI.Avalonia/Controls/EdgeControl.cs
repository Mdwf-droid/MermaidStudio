using System.ComponentModel;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using MermaidStudio.Domain.Nodes;
using PathShape = Avalonia.Controls.Shapes.Path;

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

public enum DiagramFlowDirection
{
    LR,
    RL,
    TB,
    BT
}

public sealed class EdgeControl : Canvas
{
    private readonly NodeControl _source;
    private readonly NodeControl _target;
    private readonly Canvas _parentCanvas;

    private readonly PathShape _hitPath;
    private readonly PathShape _visiblePath;
    private readonly Polygon _arrowHead;
    private readonly Border _labelBorder;
    private readonly TextBlock _labelText;

    private readonly Node? _sourceModel;
    private readonly Node? _targetModel;

    private bool _detached;
    private bool _selected;

    private const double ArrowTipOffset = 10.0;

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

    private DiagramFlowDirection _diagramDirection = DiagramFlowDirection.LR;
    public DiagramFlowDirection DiagramDirection
    {
        get => _diagramDirection;
        set
        {
            _diagramDirection = value;
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

        Width = Math.Max(1, _parentCanvas.Bounds.Width);
        Height = Math.Max(1, _parentCanvas.Bounds.Height);
        ClipToBounds = false;

        // Path invisible mais cliquable
        _hitPath = new PathShape
        {
            Stroke = Brushes.Transparent,
            StrokeThickness = 12,
            IsHitTestVisible = true
        };

        // Path visible Bézier
        _visiblePath = new PathShape
        {
            Stroke = Brushes.White,
            StrokeThickness = 2,
            Fill = null,
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

        // Label d’edge
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

        Children.Add(_hitPath);
        Children.Add(_visiblePath);
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
            UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (_detached)
            return;

        // 1) Extrémités sémantiques effectives
        var visualStartNode = _direction == EdgeDirection.Forward ? _source : _target;
        var visualEndNode = _direction == EdgeDirection.Forward ? _target : _source;

        // 2) Ancrage intelligent (S13) basé sur le sens effectif
        var (startSide, endSide) = ComputeAnchorSides(visualStartNode, visualEndNode);

        // 3) Points d’ancrage
        var startAnchor = visualStartNode.GetAnchorPoint(startSide, _parentCanvas);
        var endAnchor = visualEndNode.GetAnchorPoint(endSide, _parentCanvas);

        // 4) Géométrie visuelle Bézier
        var geometry = ComputeBezierGeometry(startAnchor, endAnchor, endSide, _diagramDirection);

        _hitPath.Data = geometry.PathGeometry;
        _visiblePath.Data = geometry.PathGeometry;

        ApplyPathStyle();
        ApplyArrowVisual(geometry.ArrowTip, geometry.ArrowDirectionVector);
        ApplyLabelVisual(geometry.LabelPoint);
    }

    private void ApplyPathStyle()
    {
        var stroke = _selected ? Brushes.DodgerBlue : Brushes.White;
        _visiblePath.Stroke = stroke;
        _arrowHead.Fill = stroke;
        _arrowHead.Stroke = stroke;

        switch (_styleKind)
        {
            case EdgeStyleKind.Default:
                _visiblePath.StrokeThickness = 2;
                _visiblePath.StrokeDashArray = null;
                break;

            case EdgeStyleKind.Dashed:
                _visiblePath.StrokeThickness = 2;
                _visiblePath.StrokeDashArray = new AvaloniaList<double> { 6, 4 };
                break;

            case EdgeStyleKind.Thick:
                _visiblePath.StrokeThickness = 4;
                _visiblePath.StrokeDashArray = null;
                break;
        }
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

        var arrowLength = _styleKind == EdgeStyleKind.Thick ? 14 : 12;
        var arrowWidth = _styleKind == EdgeStyleKind.Thick ? 8 : 6;

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

    private void ApplyLabelVisual(Point labelPoint)
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

        _labelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _labelBorder.DesiredSize;

        SetLeft(_labelBorder, labelPoint.X - size.Width / 2);
        SetTop(_labelBorder, labelPoint.Y - size.Height / 2);
    }

    private static (NodeAnchorSide StartSide, NodeAnchorSide EndSide)
        ComputeAnchorSides(NodeControl visualStartNode, NodeControl visualEndNode)
    {
        var startCenter = visualStartNode.GetCenter(visualStartNode.Parent as Visual ?? visualStartNode);
        var endCenter = visualEndNode.GetCenter(visualEndNode.Parent as Visual ?? visualEndNode);

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

    private static (PathGeometry PathGeometry, Point ArrowTip, Vector ArrowDirectionVector, Point LabelPoint)
        ComputeBezierGeometry(Point startAnchor, Point endAnchor, NodeAnchorSide endSide, DiagramFlowDirection diagramDirection)
    {
        var outwardNormal = GetSideNormal(endSide);

        // Pointe légèrement à l’extérieur du node d’arrivée
        var arrowTip = endAnchor + outwardNormal * ArrowTipOffset;

        // La flèche doit pointer vers le node d’arrivée
        var arrowDirection = -outwardNormal;
        var arrowDirUnit = Normalize(arrowDirection);

        var arrowLength = 12.0;
        var pathEnd = new Point(
            arrowTip.X - arrowDirUnit.X * arrowLength,
            arrowTip.Y - arrowDirUnit.Y * arrowLength);

        // Contrôles Bézier selon la direction globale Mermaid
        var dx = pathEnd.X - startAnchor.X;
        var dy = pathEnd.Y - startAnchor.Y;

        var horizontalDominant =
            diagramDirection == DiagramFlowDirection.LR ||
            diagramDirection == DiagramFlowDirection.RL;

        Point c1;
        Point c2;

        if (horizontalDominant)
        {
            var handle = Math.Max(Math.Abs(dx) * 0.5, 40.0);
            var sign = dx >= 0 ? 1.0 : -1.0;

            c1 = new Point(startAnchor.X + handle * sign, startAnchor.Y);

            c2 = new Point(
                pathEnd.X - arrowDirUnit.X * Math.Max(24.0, handle * 0.35),
                pathEnd.Y - arrowDirUnit.Y * Math.Max(24.0, handle * 0.35));
        }
        else
        {
            var handle = Math.Max(Math.Abs(dy) * 0.5, 40.0);
            var sign = dy >= 0 ? 1.0 : -1.0;

            c1 = new Point(startAnchor.X, startAnchor.Y + handle * sign);

            c2 = new Point(
                pathEnd.X - arrowDirUnit.X * Math.Max(24.0, handle * 0.35),
                pathEnd.Y - arrowDirUnit.Y * Math.Max(24.0, handle * 0.35));
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

        return (geometry, arrowTip, arrowDirection, labelPoint);
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
            return new Vector(1, 0);

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
}
