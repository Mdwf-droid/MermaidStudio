using Avalonia.Controls;
using MermaidStudio.UI.Avalonia.Controls;

namespace MermaidStudio.UI.Avalonia.Editing;

public sealed class CreateNodeCommand : IUndoableCommand
{
    private readonly Canvas _canvas;
    private readonly NodeControl _node;

    public string Name => "Create Node";

    public CreateNodeCommand(Canvas canvas, NodeControl node)
    {
        _canvas = canvas;
        _node = node;
    }

    public void Execute()
    {
        if (!_canvas.Children.Contains(_node))
            _canvas.Children.Add(_node);
    }

    public void Undo()
    {
        if (_canvas.Children.Contains(_node))
            _canvas.Children.Remove(_node);
    }
}
