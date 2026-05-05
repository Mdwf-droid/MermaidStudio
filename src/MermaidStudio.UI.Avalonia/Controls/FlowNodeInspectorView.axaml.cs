using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MermaidStudio.UI.Avalonia.Controls;

public partial class FlowNodeInspectorView : UserControl
{
    public event EventHandler? ApplyNodeLabelRequested;
    public event EventHandler? ApplyNodeStyleRequested;

    public FlowNodeInspectorView()
    {
        AvaloniaXamlLoader.Load(this);
        GetSelectionTypeTextBox().Text = "Node";
        GetSelectionKindTextBox().Text = "Flowchart";
    }

    public string? NodeLabelText => GetNodeLabelTextBox().Text;
    public int NodeStyleIndex => GetNodeStyleComboBox().SelectedIndex;

    public void SetState(string id, string label, int nodeStyleIndex, string x, string y)
    {
        GetSelectionIdTextBox().Text = id;
        GetNodeLabelTextBox().Text = label;
        GetNodeStyleComboBox().SelectedIndex = nodeStyleIndex;
        GetLayoutXTextBox().Text = x;
        GetLayoutYTextBox().Text = y;
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

    private TextBox GetNodeLabelTextBox()
        => this.FindControl<TextBox>("NodeLabelTextBox")
           ?? throw new InvalidOperationException("NodeLabelTextBox introuvable.");

    private ComboBox GetNodeStyleComboBox()
        => this.FindControl<ComboBox>("NodeStyleComboBox")
           ?? throw new InvalidOperationException("NodeStyleComboBox introuvable.");

    private TextBox GetLayoutXTextBox()
        => this.FindControl<TextBox>("LayoutXTextBox")
           ?? throw new InvalidOperationException("LayoutXTextBox introuvable.");

    private TextBox GetLayoutYTextBox()
        => this.FindControl<TextBox>("LayoutYTextBox")
           ?? throw new InvalidOperationException("LayoutYTextBox introuvable.");

    private void OnApplyNodeLabelClicked(object? sender, RoutedEventArgs e)
        => ApplyNodeLabelRequested?.Invoke(this, EventArgs.Empty);

    private void OnApplyNodeStyleClicked(object? sender, RoutedEventArgs e)
        => ApplyNodeStyleRequested?.Invoke(this, EventArgs.Empty);
}
