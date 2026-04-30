using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MermaidStudio.UI.Avalonia.Controls;

public partial class NodeControl : UserControl
{
    public NodeControl()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
