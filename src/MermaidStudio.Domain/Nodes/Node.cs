using System.ComponentModel;
using System.Runtime.CompilerServices;
using MermaidStudio.Domain.Core;

namespace MermaidStudio.Domain.Nodes;

public sealed class Node : INotifyPropertyChanged
{
    private string _label = "Node";
    private NodeType _type = NodeType.Generic;
    private double _x;
    private double _y;

    public EntityId Id { get; init; } = EntityId.New();

    public string Label
    {
        get => _label;
        set
        {
            if (_label == value) return;
            _label = value;
            OnPropertyChanged();
        }
    }

    public NodeType Type
    {
        get => _type;
        set
        {
            if (_type == value) return;
            _type = value;
            OnPropertyChanged();
        }
    }

    // coordonnées d’édition (UI)
    public double X
    {
        get => _x;
        set
        {
            if (_x == value) return;
            _x = value;
            OnPropertyChanged();
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (_y == value) return;
            _y = value;
            OnPropertyChanged();
        }
    }

    public IList<Port> Ports { get; } = new List<Port>();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
