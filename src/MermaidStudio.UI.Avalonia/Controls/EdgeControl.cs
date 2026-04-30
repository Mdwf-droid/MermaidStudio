using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace MermaidStudio.UI.Avalonia.Controls;

public sealed class EdgeControl : Line
{
    private readonly NodeControl _source;
    private readonly NodeControl _target;
    private readonly Canvas _canvas;

    public EdgeControl(Canvas canvas, NodeControl source, NodeControl target)
    {
        _canvas = canvas;
        _source = source;
        _target = target;

        Stroke = Brushes.White;
        StrokeThickness = 2;
        IsHitTestVisible = false;

        UpdateEndpoints();

        // On recalcule quand les nodes se relayoutent/se déplacent
        _source.LayoutUpdated += OnNodeLayoutUpdated;
        _target.LayoutUpdated += OnNodeLayoutUpdated;
    }

    private void OnNodeLayoutUpdated(object? sender, EventArgs e)
    {
        UpdateEndpoints();
    }

    private void UpdateEndpoints()
    {
        StartPoint = _source.GetRightPortCenter(_canvas);
        EndPoint = _target.GetRightPortCenter(_canvas);
    }
}
