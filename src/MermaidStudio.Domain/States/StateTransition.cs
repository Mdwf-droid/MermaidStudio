using System.ComponentModel;
using System.Runtime.CompilerServices;
using MermaidStudio.Domain.Core;

namespace MermaidStudio.Domain.States;

public sealed class StateTransition : INotifyPropertyChanged
{
    private string _label = string.Empty;
    private EntityId _sourceStateId;
    private EntityId _targetStateId;

    public EntityId Id { get; init; } = EntityId.New();

    public EntityId SourceStateId
    {
        get => _sourceStateId;
        set
        {
            if (_sourceStateId.Equals(value)) return;
            _sourceStateId = value;
            OnPropertyChanged();
        }
    }

    public EntityId TargetStateId
    {
        get => _targetStateId;
        set
        {
            if (_targetStateId.Equals(value)) return;
            _targetStateId = value;
            OnPropertyChanged();
        }
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
