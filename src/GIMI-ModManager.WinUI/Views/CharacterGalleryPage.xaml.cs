using CommunityToolkit.WinUI.UI.Animations;
using GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class CharacterGalleryPage : Page
{
    public CharacterGalleryViewModel ViewModel { get; } = App.GetService<CharacterGalleryViewModel>();

    public CharacterGalleryPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        this.RegisterElementForConnectedAnimation("animationKeyContentGrid", itemHero);
    }

    private void ToggleModButton_OnPointerEntered(object sender, PointerRoutedEventArgs e) =>
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

    private void ToggleModButton_OnPointerExited(object sender, PointerRoutedEventArgs e) =>
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);

    private void ViewToggleSwitch_OnToggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ToggleViewCommand.CanExecute(null))
            ViewModel.ToggleViewCommand.Execute(null);
    }
}