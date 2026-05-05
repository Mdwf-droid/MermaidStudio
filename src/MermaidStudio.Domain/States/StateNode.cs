using System.ComponentModel;
using System.Runtime.CompilerServices;
using MermaidStudio.Domain.Core;

namespace MermaidStudio.Domain.States;

public sealed class StateNode : INotifyPropertyChanged
{
    private string _label = "State";
    private double _x;
    private double _y;
    private StateNodeKind _kind = StateNodeKind.Normal;

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

    public StateNodeKind Kind
    {
        get => _kind;
        set
        {
            if (_kind == value) return;
            _kind = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
