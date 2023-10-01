using Microsoft.UI.Xaml.Controls;

namespace JASM.AutoUpdater;

public sealed partial class MainPage : Page
{
    public MainPageVM ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
    }
}