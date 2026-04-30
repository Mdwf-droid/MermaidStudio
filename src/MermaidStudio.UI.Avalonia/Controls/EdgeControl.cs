using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace MermaidStudio.UI.Avalonia.Controls;

public sealed class EdgeControl : Line
{
    private readonly NodeControl _source;
    private readonly NodeControl _target;
    private readonly Canvas _canvas;
    private bool _detached;

    public NodeControl SourceNode => _source;
    public NodeControl TargetNode => _target;

    public EdgeControl(Canvas canvas, NodeControl source, NodeControl target)
    {
        _canvas = canvas;
        _source = source;
        _target = target;

        Stroke = Brushes.White;
        StrokeThickness = 2;
        IsHitTestVisible = false;

        UpdateEndpoints();

        _source.LayoutUpdated += OnNodeLayoutUpdated;
        _target.LayoutUpdated += OnNodeLayoutUpdated;
    }

    public void Detach()
    {
        if (_detached)
            return;

        _source.LayoutUpdated -= OnNodeLayoutUpdated;
        _target.LayoutUpdated -= OnNodeLayoutUpdated;
        _detached = true;
    }

    private void OnNodeLayoutUpdated(object? sender, EventArgs e)
    {
        if (_detached)
            return;

        UpdateEndpoints();
    }

    private void UpdateEndpoints()
    {
        if (_detached)
            return;

        StartPoint = _source.GetRightPortCenter(_canvas);
        EndPoint = _target.GetRightPortCenter(_canvas);
    }
}
