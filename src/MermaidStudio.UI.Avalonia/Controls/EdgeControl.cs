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

    // S12.B : la pointe de flèche est toujours dessinée à droite du node
    private const double ArrowTipOffset = 12.0;

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

        var p1 = _source.GetRightPortCenter(_parentCanvas);
        var p2 = _target.GetRightPortCenter(_parentCanvas);

        // La ligne hit-test garde les points "réels"
        _hitLine.StartPoint = p1;
        _hitLine.EndPoint = p2;

        // Calcul des points visuels
        var visual = ComputeVisualGeometry(p1, p2);

        _visibleLine.StartPoint = visual.LineStart;
        _visibleLine.EndPoint = visual.LineEnd;

        ApplyLineStyle();
        ApplyArrowVisual(visual.ArrowTip, visual.ArrowDirection);
        ApplyLabelVisual(visual.LineStart, visual.LineEnd);
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

    private void ApplyArrowVisual(Point arrowTip, EdgeDirection direction)
    {
        var arrowLength = _styleKind == EdgeStyleKind.Thick ? 14 : 12;
        var arrowWidth = _styleKind == EdgeStyleKind.Thick ? 8 : 6;

        // S12.B :
        // - la flèche est TOUJOURS dessinée à l’extérieur, à droite du node concerné
        // - pour garder le sens visible, on inverse la géométrie entre Forward et Reverse
        AvaloniaList<Point> points;

        if (direction == EdgeDirection.Forward)
        {
            // pointe vers la droite
            var baseCenter = new Point(arrowTip.X - arrowLength, arrowTip.Y);

            points = new AvaloniaList<Point>
            {
                arrowTip,
                new Point(baseCenter.X, baseCenter.Y - arrowWidth),
                new Point(baseCenter.X, baseCenter.Y + arrowWidth)
            };
        }
        else
        {
            // pointe vers la gauche, mais positionnée à droite du node source
            var baseCenter = new Point(arrowTip.X + arrowLength, arrowTip.Y);

            points = new AvaloniaList<Point>
            {
                arrowTip,
                new Point(baseCenter.X, baseCenter.Y - arrowWidth),
                new Point(baseCenter.X, baseCenter.Y + arrowWidth)
            };
        }

        _arrowHead.Points = points;
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

    private (Point LineStart, Point LineEnd, Point ArrowTip, EdgeDirection ArrowDirection)
        ComputeVisualGeometry(Point anchorSource, Point anchorTarget)
    {
        // S12.B :
        // La ligne visible ne va pas jusqu’au port d’arrivée :
        // elle s’arrête juste avant la flèche, qui est elle-même dessinée à droite du node.

        if (_direction == EdgeDirection.Forward)
        {
            var arrowTip = new Point(anchorTarget.X + ArrowTipOffset, anchorTarget.Y);
            var lineEnd = new Point(arrowTip.X - 12.0, arrowTip.Y);

            return (
                LineStart: anchorSource,
                LineEnd: lineEnd,
                ArrowTip: arrowTip,
                ArrowDirection: EdgeDirection.Forward
            );
        }
        else
        {
            var arrowTip = new Point(anchorSource.X + ArrowTipOffset, anchorSource.Y);
            var lineStart = new Point(arrowTip.X + 12.0, arrowTip.Y);

            return (
                LineStart: lineStart,
                LineEnd: anchorTarget,
                ArrowTip: arrowTip,
                ArrowDirection: EdgeDirection.Reverse
            );
        }
    }
}
