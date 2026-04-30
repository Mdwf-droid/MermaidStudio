using Avalonia.Controls;
using MermaidStudio.UI.Avalonia.Controls;

namespace MermaidStudio.UI.Avalonia.Editing;

public sealed class CreateEdgeCommand : IUndoableCommand
{
    private readonly Canvas _canvas;
    private readonly IList<EdgeControl> _edgeStore;
    private readonly EdgeControl _edge;

    public string Name => "Create Edge";

    public CreateEdgeCommand(Canvas canvas, IList<EdgeControl> edgeStore, EdgeControl edge)
    {
        _canvas = canvas;
        _edgeStore = edgeStore;
        _edge = edge;
    }

    public void Execute()
    {
        if (!_edgeStore.Contains(_edge))
            _edgeStore.Add(_edge);

        if (!_canvas.Children.Contains(_edge))
            _canvas.Children.Insert(0, _edge);
    }

    public void Undo()
    {
        if (_canvas.Children.Contains(_edge))
            _canvas.Children.Remove(_edge);

        if (_edgeStore.Contains(_edge))
            _edgeStore.Remove(_edge);
    }
}
