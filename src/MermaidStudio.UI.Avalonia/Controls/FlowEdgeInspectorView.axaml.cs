using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MermaidStudio.UI.Avalonia.Controls;

public partial class FlowEdgeInspectorView : UserControl
{
    public event EventHandler? ApplyEdgeLabelRequested;
    public event EventHandler? ApplyEdgeStyleRequested;

    public FlowEdgeInspectorView()
    {
        AvaloniaXamlLoader.Load(this);
        GetSelectionTypeTextBox().Text = "Edge";
        GetSelectionKindTextBox().Text = "Flowchart";
    }

    public string? EdgeLabelText => GetEdgeLabelTextBox().Text;
    public int EdgeStyleIndex => GetEdgeStyleComboBox().SelectedIndex;
    public int EdgeDirectionIndex => GetEdgeDirectionComboBox().SelectedIndex;

    public void SetState(string id, string label, int edgeStyleIndex, int edgeDirectionIndex)
    {
        GetSelectionIdTextBox().Text = id;
        GetEdgeLabelTextBox().Text = label;
        GetEdgeStyleComboBox().SelectedIndex = edgeStyleIndex;
        GetEdgeDirectionComboBox().SelectedIndex = edgeDirectionIndex;
    }

    private TextBox GetSelectionTypeTextBox()
        => this.FindControl<TextBox>("SelectionTypeTextBox")
           ?? throw new InvalidOperationException("SelectionTypeTextBox introuvable.");

    private TextBox GetSelectionIdTextBox()
        => this.FindControl<TextBox>("SelectionIdTextBox")
           ?? throw new InvalidOperationException("SelectionIdTextBox introuvable.");

    private TextBox GetSelectionKindTextBox()
        => this.FindControl<TextBox>("SelectionKindTextBox")
           ?? throw new InvalidOperationException("SelectionKindTextBox introuvable.");

    private TextBox GetEdgeLabelTextBox()
        => this.FindControl<TextBox>("EdgeLabelTextBox")
           ?? throw new InvalidOperationException("EdgeLabelTextBox introuvable.");

    private ComboBox GetEdgeStyleComboBox()
        => this.FindControl<ComboBox>("EdgeStyleComboBox")
           ?? throw new InvalidOperationException("EdgeStyleComboBox introuvable.");

    private ComboBox GetEdgeDirectionComboBox()
        => this.FindControl<ComboBox>("EdgeDirectionComboBox")
           ?? throw new InvalidOperationException("EdgeDirectionComboBox introuvable.");

    private void OnApplyEdgeLabelClicked(object? sender, RoutedEventArgs e)
        => ApplyEdgeLabelRequested?.Invoke(this, EventArgs.Empty);

    private void OnApplyEdgeStyleClicked(object? sender, RoutedEventArgs e)
        => ApplyEdgeStyleRequested?.Invoke(this, EventArgs.Empty);
}
