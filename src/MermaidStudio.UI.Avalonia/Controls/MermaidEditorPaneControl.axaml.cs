using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MermaidStudio.UI.Avalonia.Controls;

public partial class MermaidEditorPaneControl : UserControl
{
    private bool _suppressTextChanged;

    public event EventHandler? MermaidTextChanged;

    public MermaidEditorPaneControl()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string MermaidText
    {
        get => GetEditorTextBox().Text ?? string.Empty;
        set => SetMermaidTextSilently(value);
    }

    public void SetMermaidTextSilently(string text)
    {
        _suppressTextChanged = true;
        try
        {
            GetEditorTextBox().Text = text ?? string.Empty;
        }
        finally
        {
            _suppressTextChanged = false;
        }
    }

    public void SetError(string message)
    {
        GetErrorTextBlock().Text = message ?? string.Empty;
        GetErrorBorder().IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    public void ClearError()
    {
        GetErrorTextBlock().Text = string.Empty;
        GetErrorBorder().IsVisible = false;
    }

    private TextBox GetEditorTextBox()
        => this.FindControl<TextBox>("MermaidEditorTextBox")
           ?? throw new InvalidOperationException("MermaidEditorTextBox introuvable.");

    private Border GetErrorBorder()
        => this.FindControl<Border>("ErrorBorder")
           ?? throw new InvalidOperationException("ErrorBorder introuvable.");

    private TextBlock GetErrorTextBlock()
        => this.FindControl<TextBlock>("ErrorTextBlock")
           ?? throw new InvalidOperationException("ErrorTextBlock introuvable.");

    private void OnMermaidTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged)
            return;

        MermaidTextChanged?.Invoke(this, EventArgs.Empty);
    }
}
