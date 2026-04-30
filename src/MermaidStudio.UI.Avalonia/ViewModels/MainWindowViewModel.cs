using MermaidStudio.Application.Editor;

namespace MermaidStudio.UI.Avalonia.ViewModels;

public sealed class MainWindowViewModel
{
    public EditorState EditorState { get; } = new();
    public string Title => "MermaidStudio";
}
