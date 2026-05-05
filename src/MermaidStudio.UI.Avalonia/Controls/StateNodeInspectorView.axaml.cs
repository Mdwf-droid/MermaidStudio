using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MermaidStudio.UI.Avalonia.Controls;

public partial class StateNodeInspectorView : UserControl
{
    public event EventHandler? ApplyNodeLabelRequested;

    public StateNodeInspectorView()
    {
        AvaloniaXamlLoader.Load(this);
        GetSelectionTypeTextBox().Text = "State";
        GetSelectionKindTextBox().Text = "State Diagram";
    }

    public string? NodeLabelText => GetStateLabelTextBox().Text;

    public void SetState(string id, string stateKind, bool labelEditable, string labelValue, string x, string y)
    {
        GetSelectionIdTextBox().Text = id;
        GetStateKindTextBox().Text = stateKind;
        GetStateLabelTextBox().Text = labelValue;
        GetStateLabelTextBox().IsEnabled = labelEditable;
        GetApplyStateLabelButton().IsEnabled = labelEditable;
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

    private TextBox GetStateLabelTextBox()
        => this.FindControl<TextBox>("StateLabelTextBox")
           ?? throw new InvalidOperationException("StateLabelTextBox introuvable.");

    private Button GetApplyStateLabelButton()
        => this.FindControl<Button>("ApplyStateLabelButton")
           ?? throw new InvalidOperationException("ApplyStateLabelButton introuvable.");

    private TextBox GetStateKindTextBox()
        => this.FindControl<TextBox>("StateKindTextBox")
           ?? throw new InvalidOperationException("StateKindTextBox introuvable.");

    private TextBox GetLayoutXTextBox()
        => this.FindControl<TextBox>("LayoutXTextBox")
           ?? throw new InvalidOperationException("LayoutXTextBox introuvable.");

    private TextBox GetLayoutYTextBox()
        => this.FindControl<TextBox>("LayoutYTextBox")
           ?? throw new InvalidOperationException("LayoutYTextBox introuvable.");

    private void OnApplyStateLabelClicked(object? sender, RoutedEventArgs e)
        => ApplyNodeLabelRequested?.Invoke(this, EventArgs.Empty);
}
