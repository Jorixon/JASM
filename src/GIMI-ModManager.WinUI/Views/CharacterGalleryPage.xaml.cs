using CommunityToolkit.WinUI.UI.Animations;
using GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
        Loaded += CharacterGalleryPage_Loaded;
    }

    private void CharacterGalleryPage_Loaded(object sender, RoutedEventArgs e)
    {
        GridItemHeightSlider.ValueChanged += GridItemHeightSlider_OnValueChanged;
        GridItemWithSlider.ValueChanged += GridItemWithSlider_OnValueChanged;
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

    private async void GridItemHeightSlider_OnValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        await CallSetWidthHeight(GridItemWithSlider?.Value, e?.NewValue);
    }

    private async void GridItemWithSlider_OnValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        await CallSetWidthHeight(e?.NewValue, GridItemHeightSlider?.Value);
    }

    private async Task CallSetWidthHeight(double? width, double? height)
    {
        if (width is null || height is null)
            return;

        var value = new SetHeightWidth((int)Math.Round(width.Value), (int)Math.Round(height.Value));
        if (ViewModel.SetHeightWidthCommand.CanExecute(value))
            await ViewModel.SetHeightWidthCommand.ExecuteAsync(value);
    }

    private void ModSearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            ViewModel.OnSearchBoxTextChanged(textBox.Text);
    }

    private async void ModdableObjectsGridView_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not SelectableModdableObjectVm moddableObjectVm)
            return;

        if (ViewModel.NavigateToModObjectCommand.CanExecute(moddableObjectVm))
            await ViewModel.NavigateToModObjectCommand.ExecuteAsync(moddableObjectVm);
    }
}