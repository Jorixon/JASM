using Microsoft.UI.Xaml;

namespace JASM.Updater;

public sealed partial class MainWindow : Window
{
    public MainWindowVM ViewModel { get; } = new();

    public MainWindow()
    {
        this.InitializeComponent();
    }

    private void myButton_Click(object sender, RoutedEventArgs e)
    {
        myButton.Content = "Clicked";
    }
}