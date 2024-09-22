using GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views.CharacterDetailsPages;

public sealed partial class CharacterCard : UserControl
{
    public Grid ItemHero;

    public CharacterCard()
    {
        InitializeComponent();
        ItemHero = itemHero;
    }


    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(CharacterDetailsViewModel), typeof(CharacterCard),
        new PropertyMetadata(default(CharacterDetailsViewModel)));

    public CharacterDetailsViewModel ViewModel
    {
        get { return (CharacterDetailsViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }
}