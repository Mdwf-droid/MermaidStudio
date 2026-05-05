namespace MermaidStudio.UI.Avalonia.Controls;

public sealed class ViewportState
{
    public double MinZoom { get; } = 0.25;
    public double MaxZoom { get; } = 3.00;
    public double ZoomStep { get; } = 0.10;

    public double Zoom { get; private set; } = 1.0;

    public bool SetZoom(double value)
    {
        var clamped = Math.Clamp(value, MinZoom, MaxZoom);
        if (Math.Abs(clamped - Zoom) < 0.0001)
            return false;

        Zoom = clamped;
        return true;
    }

    public bool ZoomIn() => SetZoom(Zoom + ZoomStep);
    public bool ZoomOut() => SetZoom(Zoom - ZoomStep);
    public bool Reset() => SetZoom(1.0);

    public string GetDisplayText()
        => $"{Math.Round(Zoom * 100):0}%";
}
