using Avalonia.Controls;
using MermaidStudio.UI.Avalonia.Controls;

namespace MermaidStudio.UI.Avalonia.Editing;

public sealed class DeleteNodeCommand : IUndoableCommand
{
    private readonly Canvas _canvas;
    private readonly NodeControl _node;
    private readonly IList<EdgeControl> _edgeStore;
    private readonly List<EdgeControl> _attachedEdges;

    public string Name => "Delete Node";

    public DeleteNodeCommand(
        Canvas canvas,
        NodeControl node,
        IList<EdgeControl> edgeStore)
    {
        _canvas = canvas;
        _node = node;
        _edgeStore = edgeStore;

        _attachedEdges = edgeStore
            .Where(edge =>
                ReferenceEquals(edge.SourceNode, node) ||
                ReferenceEquals(edge.TargetNode, node))
            .ToList();
    }

    public void Execute()
    {
        // Supprimer d'abord les edges liés
        foreach (var edge in _attachedEdges)
        {
            edge.Detach();

            if (_canvas.Children.Contains(edge))
                _canvas.Children.Remove(edge);

            if (_edgeStore.Contains(edge))
                _edgeStore.Remove(edge);
        }

        // Puis le node
        if (_canvas.Children.Contains(_node))
            _canvas.Children.Remove(_node);
    }

    public void Undo()
    {
        // Recréer le node
        if (!_canvas.Children.Contains(_node))
            _canvas.Children.Add(_node);

        // Recréer ensuite les edges derrière les nodes
        foreach (var edge in _attachedEdges)
        {
            edge.Attach();

            if (!_edgeStore.Contains(edge))
                _edgeStore.Add(edge);

            if (!_canvas.Children.Contains(edge))
                _canvas.Children.Insert(0, edge);
        }
    }
}
