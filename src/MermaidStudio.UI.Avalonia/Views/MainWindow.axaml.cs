using Avalonia.Controls;
using MermaidStudio.UI.Avalonia.ViewModels;

namespace MermaidStudio.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
