using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MermaidStudio.UI.Avalonia.Controls;

public partial class InspectorPaneControl : UserControl
{
    private readonly NoSelectionInspectorView _noSelectionView = new();
    private readonly FlowNodeInspectorView _flowNodeView = new();
    private readonly FlowEdgeInspectorView _flowEdgeView = new();
    private readonly StateNodeInspectorView _stateNodeView = new();
    private readonly StateTransitionInspectorView _stateTransitionView = new();

    public event EventHandler? ApplyNodeLabelRequested;
    public event EventHandler? ApplyNodeStyleRequested;
    public event EventHandler? ApplyEdgeLabelRequested;
    public event EventHandler? ApplyEdgeStyleRequested;

    public InspectorPaneControl()
    {
        AvaloniaXamlLoader.Load(this);

        _flowNodeView.ApplyNodeLabelRequested += (_, _) => ApplyNodeLabelRequested?.Invoke(this, EventArgs.Empty);
        _flowNodeView.ApplyNodeStyleRequested += (_, _) => ApplyNodeStyleRequested?.Invoke(this, EventArgs.Empty);

        _flowEdgeView.ApplyEdgeLabelRequested += (_, _) => ApplyEdgeLabelRequested?.Invoke(this, EventArgs.Empty);
        _flowEdgeView.ApplyEdgeStyleRequested += (_, _) => ApplyEdgeStyleRequested?.Invoke(this, EventArgs.Empty);

        _stateNodeView.ApplyNodeLabelRequested += (_, _) => ApplyNodeLabelRequested?.Invoke(this, EventArgs.Empty);

        _stateTransitionView.ApplyEdgeLabelRequested += (_, _) => ApplyEdgeLabelRequested?.Invoke(this, EventArgs.Empty);

        ShowNoSelection();
    }

    public string? NodeLabelText
        => GetContentHost().Content switch
        {
            FlowNodeInspectorView view => view.NodeLabelText,
            StateNodeInspectorView view => view.NodeLabelText,
            _ => string.Empty
        };

    public int NodeStyleIndex
        => GetContentHost().Content switch
        {
            FlowNodeInspectorView view => view.NodeStyleIndex,
            _ => 0
        };

    public string? EdgeLabelText
        => GetContentHost().Content switch
        {
            FlowEdgeInspectorView view => view.EdgeLabelText,
            StateTransitionInspectorView view => view.EdgeLabelText,
            _ => string.Empty
        };

    public int EdgeStyleIndex
        => GetContentHost().Content switch
        {
            FlowEdgeInspectorView view => view.EdgeStyleIndex,
            _ => 0
        };

    public int EdgeDirectionIndex
        => GetContentHost().Content switch
        {
            FlowEdgeInspectorView view => view.EdgeDirectionIndex,
            _ => 0
        };

    public void ShowNoSelection()
    {
        GetContentHost().Content = _noSelectionView;
    }

    public void ShowFlowNode(string id, string label, int nodeStyleIndex, string x, string y)
    {
        _flowNodeView.SetState(id, label, nodeStyleIndex, x, y);
        GetContentHost().Content = _flowNodeView;
    }

    public void ShowFlowEdge(string id, string label, int edgeStyleIndex, int edgeDirectionIndex)
    {
        _flowEdgeView.SetState(id, label, edgeStyleIndex, edgeDirectionIndex);
        GetContentHost().Content = _flowEdgeView;
    }

    public void ShowStateNode(string id, string stateKind, bool labelEditable, string labelValue, string x, string y)
    {
        _stateNodeView.SetState(id, stateKind, labelEditable, labelValue, x, y);
        GetContentHost().Content = _stateNodeView;
    }

    public void ShowStateTransition(string id, string label)
    {
        _stateTransitionView.SetState(id, label);
        GetContentHost().Content = _stateTransitionView;
    }

    private ContentControl GetContentHost()
        => this.FindControl<ContentControl>("InspectorContentHost")
           ?? throw new InvalidOperationException("InspectorContentHost introuvable.");
}
