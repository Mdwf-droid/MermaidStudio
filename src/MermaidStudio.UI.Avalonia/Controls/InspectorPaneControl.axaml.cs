using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MermaidStudio.UI.Avalonia.Controls;

public partial class InspectorPaneControl : UserControl
{
    public event EventHandler? ApplyNodeLabelRequested;
    public event EventHandler? ApplyNodeStyleRequested;
    public event EventHandler? ApplyEdgeLabelRequested;
    public event EventHandler? ApplyEdgeStyleRequested;

    public InspectorPaneControl()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string? NodeLabelText => GetNodeLabelTextBox().Text;
    public int NodeStyleIndex => GetNodeStyleComboBox().SelectedIndex;

    public string? EdgeLabelText => GetEdgeLabelTextBox().Text;
    public int EdgeStyleIndex => GetEdgeStyleComboBox().SelectedIndex;
    public int EdgeDirectionIndex => GetEdgeDirectionComboBox().SelectedIndex;

    public string MermaidOutputText
    {
        get => GetMermaidOutputTextBox().Text ?? string.Empty;
        set => GetMermaidOutputTextBox().Text = value;
    }

    public void ApplyState(
        bool nodeSectionEnabled,
        string nodeLabel,
        int nodeStyleIndex,
        bool nodeStyleEnabled,
        bool edgeSectionEnabled,
        string edgeLabel,
        int edgeStyleIndex,
        int edgeDirectionIndex,
        bool edgeStyleEnabled,
        bool edgeDirectionEnabled,
        bool edgeStyleApplyEnabled)
    {
        var nodeTextBox = GetNodeLabelTextBox();
        var nodeLabelButton = GetNodeLabelButton();
        var nodeStyleCombo = GetNodeStyleComboBox();
        var nodeStyleButton = GetNodeStyleButton();

        var edgeTextBox = GetEdgeLabelTextBox();
        var edgeLabelButton = GetEdgeLabelButton();
        var edgeStyleCombo = GetEdgeStyleComboBox();
        var edgeDirectionCombo = GetEdgeDirectionComboBox();
        var edgeStyleButton = GetEdgeStyleButton();

        nodeTextBox.IsEnabled = nodeSectionEnabled;
        nodeLabelButton.IsEnabled = nodeSectionEnabled;
        nodeStyleCombo.IsEnabled = nodeStyleEnabled;
        nodeStyleButton.IsEnabled = nodeStyleEnabled;

        nodeTextBox.Text = nodeLabel;
        nodeStyleCombo.SelectedIndex = nodeStyleIndex;

        edgeTextBox.IsEnabled = edgeSectionEnabled;
        edgeLabelButton.IsEnabled = edgeSectionEnabled;
        edgeStyleCombo.IsEnabled = edgeStyleEnabled;
        edgeDirectionCombo.IsEnabled = edgeDirectionEnabled;
        edgeStyleButton.IsEnabled = edgeStyleApplyEnabled;

        edgeTextBox.Text = edgeLabel;
        edgeStyleCombo.SelectedIndex = edgeStyleIndex;
        edgeDirectionCombo.SelectedIndex = edgeDirectionIndex;
    }

    private TextBox GetNodeLabelTextBox()
        => this.FindControl<TextBox>("SelectedNodeLabelTextBox")
           ?? throw new InvalidOperationException("SelectedNodeLabelTextBox introuvable.");

    private Button GetNodeLabelButton()
        => this.FindControl<Button>("ApplyNodeLabelButton")
           ?? throw new InvalidOperationException("ApplyNodeLabelButton introuvable.");

    private ComboBox GetNodeStyleComboBox()
        => this.FindControl<ComboBox>("SelectedNodeStyleComboBox")
           ?? throw new InvalidOperationException("SelectedNodeStyleComboBox introuvable.");

    private Button GetNodeStyleButton()
        => this.FindControl<Button>("ApplyNodeStyleButton")
           ?? throw new InvalidOperationException("ApplyNodeStyleButton introuvable.");

    private TextBox GetEdgeLabelTextBox()
        => this.FindControl<TextBox>("SelectedEdgeLabelTextBox")
           ?? throw new InvalidOperationException("SelectedEdgeLabelTextBox introuvable.");

    private Button GetEdgeLabelButton()
        => this.FindControl<Button>("ApplyEdgeLabelButton")
           ?? throw new InvalidOperationException("ApplyEdgeLabelButton introuvable.");

    private ComboBox GetEdgeStyleComboBox()
        => this.FindControl<ComboBox>("SelectedEdgeStyleComboBox")
           ?? throw new InvalidOperationException("SelectedEdgeStyleComboBox introuvable.");

    private ComboBox GetEdgeDirectionComboBox()
        => this.FindControl<ComboBox>("SelectedEdgeDirectionComboBox")
           ?? throw new InvalidOperationException("SelectedEdgeDirectionComboBox introuvable.");

    private Button GetEdgeStyleButton()
        => this.FindControl<Button>("ApplyEdgeStyleButton")
           ?? throw new InvalidOperationException("ApplyEdgeStyleButton introuvable.");

    private TextBox GetMermaidOutputTextBox()
        => this.FindControl<TextBox>("MermaidOutputTextBox")
           ?? throw new InvalidOperationException("MermaidOutputTextBox introuvable.");

    private void OnApplyNodeLabelClicked(object? sender, RoutedEventArgs e)
        => ApplyNodeLabelRequested?.Invoke(this, EventArgs.Empty);

    private void OnApplyNodeStyleClicked(object? sender, RoutedEventArgs e)
        => ApplyNodeStyleRequested?.Invoke(this, EventArgs.Empty);

    private void OnApplyEdgeLabelClicked(object? sender, RoutedEventArgs e)
        => ApplyEdgeLabelRequested?.Invoke(this, EventArgs.Empty);

    private void OnApplyEdgeStyleClicked(object? sender, RoutedEventArgs e)
        => ApplyEdgeStyleRequested?.Invoke(this, EventArgs.Empty);
}
