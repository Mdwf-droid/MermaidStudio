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

    // Flèche légèrement dégagée du node d’arrivée
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

        // 1) Extrémités sémantiques effectives
        var visualStartNode = _direction == EdgeDirection.Forward ? _source : _target;
        var visualEndNode = _direction == EdgeDirection.Forward ? _target : _source;

        // 2) Ancrage intelligent basé sur le sens effectif
        var (startSide, endSide) = ComputeAnchorSides(visualStartNode, visualEndNode);

        // 3) Points d’ancrage
        var startAnchor = visualStartNode.GetAnchorPoint(startSide, _parentCanvas);
        var endAnchor = visualEndNode.GetAnchorPoint(endSide, _parentCanvas);

        // Hit test = segment logique complet
        _hitLine.StartPoint = startAnchor;
        _hitLine.EndPoint = endAnchor;

        // 4) Géométrie visuelle cohérente avec la sémantique
        var geometry = ComputeVisualGeometry(startAnchor, endAnchor, endSide);

        _visibleLine.StartPoint = geometry.LineStart;
        _visibleLine.EndPoint = geometry.LineEnd;

        ApplyLineStyle();
        ApplyArrowVisual(geometry.ArrowTip, geometry.ArrowDirectionVector);
        ApplyLabelVisual(geometry.LineStart, geometry.LineEnd);
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

    private void ApplyLabelVisual(Point lineStart, Point lineEnd)
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

        var midX = (lineStart.X + lineEnd.X) / 2;
        var midY = (lineStart.Y + lineEnd.Y) / 2;

        _labelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _labelBorder.DesiredSize;

        SetLeft(_labelBorder, midX - size.Width / 2);
        SetTop(_labelBorder, midY - size.Height / 2);
    }

    private static (NodeAnchorSide StartSide, NodeAnchorSide EndSide)
        ComputeAnchorSides(NodeControl visualStartNode, NodeControl visualEndNode)
    {
        var parent = visualStartNode.Parent as Visual ?? visualStartNode;
        var startCenter = visualStartNode.GetCenter(parent);
        var endCenter = visualEndNode.GetCenter(parent);

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

    private static (Point LineStart, Point LineEnd, Point ArrowTip, Vector ArrowDirectionVector)
        ComputeVisualGeometry(Point startAnchor, Point endAnchor, NodeAnchorSide endSide)
    {
        var outwardNormal = GetSideNormal(endSide);

        // Pointe placée légèrement à l’extérieur du node d’arrivée
        var arrowTip = endAnchor + outwardNormal * ArrowTipOffset;

        // IMPORTANT :
        // la flèche doit pointer VERS le node d’arrivée,
        // donc la direction visuelle est l’opposé de la normale sortante
        var arrowDirection = -outwardNormal;

        var arrowLength = 12.0;
        var unit = Normalize(arrowDirection);

        // La ligne visible s’arrête à la base de la flèche
        var lineEnd = new Point(
            arrowTip.X - unit.X * arrowLength,
            arrowTip.Y - unit.Y * arrowLength);

        return (
            LineStart: startAnchor,
            LineEnd: lineEnd,
            ArrowTip: arrowTip,
            ArrowDirectionVector: arrowDirection
        );
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
}
