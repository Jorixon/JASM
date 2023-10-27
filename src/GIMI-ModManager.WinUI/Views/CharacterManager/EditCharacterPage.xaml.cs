using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GIMI_ModManager.WinUI.Views.CharacterManager;

public sealed partial class EditCharacterPage : Page
{
    public EditCharacterViewModel ViewModel { get; }

    public EditCharacterPage()
    {
        ViewModel = App.GetService<EditCharacterViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo(e.Parameter);
    }
}