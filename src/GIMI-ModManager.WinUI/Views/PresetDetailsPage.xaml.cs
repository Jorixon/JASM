using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class PresetDetailsPage : Page
{
    public PresetDetailsViewModel ViewModel { get; } = App.GetService<PresetDetailsViewModel>();

    public PresetDetailsPage()
    {
        InitializeComponent();
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchText = SearchBox.Text;
        ViewModel.OnTextChanged(SearchBox.Text);
    }
}