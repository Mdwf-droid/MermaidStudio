namespace MermaidStudio.Application.Editing;

public enum SelectionKind
{
    None,
    Node,
    Edge
}

public sealed class SelectionService
{
    private object? _selectedItem;

    public SelectionKind Kind { get; private set; } = SelectionKind.None;

    public object? SelectedItem => _selectedItem;

    public void SelectNode(object node)
    {
        _selectedItem = node;
        Kind = SelectionKind.Node;
    }

    public void SelectEdge(object edge)
    {
        _selectedItem = edge;
        Kind = SelectionKind.Edge;
    }

    public void ClearSelection()
    {
        _selectedItem = null;
        Kind = SelectionKind.None;
    }

    public T? GetSelected<T>() where T : class
    {
        return _selectedItem as T;
    }
}
