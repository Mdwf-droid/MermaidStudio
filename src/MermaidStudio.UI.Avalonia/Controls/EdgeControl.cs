using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace MermaidStudio.UI.Avalonia.Controls;

public sealed class EdgeControl : Canvas
{
    private readonly NodeControl _source;
    private readonly NodeControl _target;
    private readonly Canvas _parentCanvas;

    private readonly Line _hitLine;
    private readonly Line _visibleLine;
    private readonly Border _labelBorder;
    private readonly TextBlock _labelText;

    private bool _detached;

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

    public EdgeControl(Canvas parentCanvas, NodeControl source, NodeControl target)
    {
        _parentCanvas = parentCanvas;
        _source = source;
        _target = target;

        // Le Canvas de l'edge couvre le canvas parent
        Width = _parentCanvas.Bounds.Width;
        Height = _parentCanvas.Bounds.Height;
        ClipToBounds = false;

        // Ligne invisible MAIS cliquable : utilisée pour la sélection
        _hitLine = new Line
        {
            Stroke = Brushes.Transparent,
            StrokeThickness = 12,
            IsHitTestVisible = true
        };

        // Ligne visible : purement visuelle
        _visibleLine = new Line
        {
            Stroke = Brushes.White,
            StrokeThickness = 2,
            IsHitTestVisible = false
        };

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
        Children.Add(_labelBorder);

        Attach();
    }

    public void SetSelected(bool selected)
    {
        _visibleLine.Stroke = selected ? Brushes.DodgerBlue : Brushes.White;
        _labelBorder.BorderBrush = selected
            ? Brushes.DodgerBlue
            : new SolidColorBrush(Color.Parse("#555555"));
    }

    public void Attach()
    {
        if (!_detached)
        {
            _source.LayoutUpdated -= OnNodeLayoutUpdated;
            _target.LayoutUpdated -= OnNodeLayoutUpdated;
            _parentCanvas.LayoutUpdated -= OnCanvasLayoutUpdated;
        }

        _source.LayoutUpdated += OnNodeLayoutUpdated;
        _target.LayoutUpdated += OnNodeLayoutUpdated;
        _parentCanvas.LayoutUpdated += OnCanvasLayoutUpdated;

        _detached = false;
        UpdateVisual();
    }

    public void Detach()
    {
        if (_detached)
            return;

        _source.LayoutUpdated -= OnNodeLayoutUpdated;
        _target.LayoutUpdated -= OnNodeLayoutUpdated;
        _parentCanvas.LayoutUpdated -= OnCanvasLayoutUpdated;

        _detached = true;
    }

    private void OnNodeLayoutUpdated(object? sender, EventArgs e)
    {
        if (_detached)
            return;

        UpdateVisual();
    }

    private void OnCanvasLayoutUpdated(object? sender, EventArgs e)
    {
        if (_detached)
            return;

        Width = _parentCanvas.Bounds.Width;
        Height = _parentCanvas.Bounds.Height;

        UpdateVisual();
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

        if (string.IsNullOrWhiteSpace(_label))
        {
            _labelBorder.IsVisible = false;
            return;
        }

        _labelBorder.IsVisible = true;

        var midX = (p1.X + p2.X) / 2;
        var midY = (p1.Y + p2.Y) / 2;

        _labelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _labelBorder.DesiredSize;

        SetLeft(_labelBorder, midX - size.Width / 2);
        SetTop(_labelBorder, midY - size.Height / 2);
    }
}
