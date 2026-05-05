using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MermaidStudio.UI.Avalonia.Views;

public partial class ImportMermaidWindow : Window
{
    public ImportMermaidWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnImportClicked(object? sender, RoutedEventArgs e)
    {
        var textBox = this.FindControl<TextBox>("MermaidInputTextBox")
                     ?? throw new InvalidOperationException("MermaidInputTextBox introuvable.");

        Close(textBox.Text ?? string.Empty);
    }
}
