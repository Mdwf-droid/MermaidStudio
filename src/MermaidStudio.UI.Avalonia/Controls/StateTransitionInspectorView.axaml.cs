using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MermaidStudio.UI.Avalonia.Controls;

public partial class StateTransitionInspectorView : UserControl
{
    public event EventHandler? ApplyEdgeLabelRequested;

    public StateTransitionInspectorView()
    {
        AvaloniaXamlLoader.Load(this);
        GetSelectionTypeTextBox().Text = "Transition";
        GetSelectionKindTextBox().Text = "State Diagram";
    }

    public string? EdgeLabelText => GetTransitionLabelTextBox().Text;

    public void SetState(string id, string label)
    {
        GetSelectionIdTextBox().Text = id;
        GetTransitionLabelTextBox().Text = label;
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

    private TextBox GetTransitionLabelTextBox()
        => this.FindControl<TextBox>("TransitionLabelTextBox")
           ?? throw new InvalidOperationException("TransitionLabelTextBox introuvable.");

    private void OnApplyTransitionLabelClicked(object? sender, RoutedEventArgs e)
        => ApplyEdgeLabelRequested?.Invoke(this, EventArgs.Empty);
}
