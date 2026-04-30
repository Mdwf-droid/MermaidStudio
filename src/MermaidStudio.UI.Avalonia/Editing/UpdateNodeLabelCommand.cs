using MermaidStudio.Domain.Nodes;

namespace MermaidStudio.UI.Avalonia.Editing;

public sealed class UpdateNodeLabelCommand : IUndoableCommand
{
    private readonly Node _node;
    private readonly string _oldLabel;
    private readonly string _newLabel;

    public string Name => "Update Node Label";

    public UpdateNodeLabelCommand(Node node, string oldLabel, string newLabel)
    {
        _node = node;
        _oldLabel = oldLabel;
        _newLabel = newLabel;
    }

    public void Execute()
    {
        _node.Label = _newLabel;
    }

    public void Undo()
    {
        _node.Label = _oldLabel;
    }
}
