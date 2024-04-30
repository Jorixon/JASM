using CommunityToolkit.WinUI;
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
        ViewModel.Initialized += ViewModel_Initialized;
    }


    private ManualResetEventSlim? _manualResetEventSlim = new(false);

    private async void ViewModel_Initialized(object? sender, EventArgs e)
    {
        if (!IsLoaded)
            await Task.Run(() => _manualResetEventSlim?.WaitHandle.WaitOne(TimeSpan.FromSeconds(1), false));


        SetColDefinitions();
        var selectedCharacter = ViewModel.ModdableObjectVms.FirstOrDefault(m => m.IsSelected);
        if (selectedCharacter is not null)
            await ModdableObjectsGridView.SmoothScrollIntoViewWithItemAsync(selectedCharacter);

        RegisterPostInitEventHandlers();
        _manualResetEventSlim?.Dispose();
        _manualResetEventSlim = null;
    }

    private void CharacterGalleryPage_Loaded(object sender, RoutedEventArgs e)
    {
        _manualResetEventSlim?.Set();
    }

    private void RegisterPostInitEventHandlers()
    {
        GridItemHeightSlider.ValueChanged += GridItemHeightSlider_OnValueChanged;
        GridItemWithSlider.ValueChanged += GridItemWithSlider_OnValueChanged;
        NavPaneToggleSwitch.Toggled += NavPaneToggleSwitch_OnToggled;
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

    private async void NavPaneToggleSwitch_OnToggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ToggleNavPaneCommand.CanExecute(null))
        {
            await ViewModel.ToggleNavPaneCommand.ExecuteAsync(null);
            SetColDefinitions();
        }
    }

    private void SetColDefinitions()
    {
        if (ViewModel.IsNavPaneVisible)
        {
            NavPaneColDef.Width = new GridLength(1, GridUnitType.Star);
            ModGridViewColDef.Width = new GridLength(10, GridUnitType.Star);
        }
        else
        {
            NavPaneColDef.Width = new GridLength(0, GridUnitType.Auto);
            ModGridViewColDef.Width = new GridLength(1, GridUnitType.Star);
        }
    }
}