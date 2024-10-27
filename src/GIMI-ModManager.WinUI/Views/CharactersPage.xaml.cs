using Windows.ApplicationModel.DataTransfer;
using CommunityToolkit.WinUI;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.ViewModels;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Serilog;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class CharactersPage : Page
{
    public CharactersViewModel ViewModel { get; }

    public CharactersPage()
    {
        ViewModel = App.GetService<CharactersViewModel>();
        InitializeComponent();
        Loaded += (sender, args) =>
        {
            SearchBox.Focus(FocusState.Keyboard);
            ViewModel.SimpleSelectProcessDialogVM.Dialog = SelectProcessDialog;
        };
        ViewModel.OnScrollToCharacter += ViewModel_OnScrollToCharacter;
    }

    private void ViewModel_OnScrollToCharacter(object? sender, ScrollToCharacterArgs e)
    {
        if (e.Character is null) return;

        var item = CharactersGridView.Items.FirstOrDefault(x =>
            ((CharacterGridItemModel)x).Character.InternalNameEquals(e.Character.Character));
        if (item is null)
            return;

        CharactersGridView.SmoothScrollIntoViewWithItemAsync(item, ScrollItemPlacement.Center, disableAnimation: true,
            scrollIfVisible: false);
    }


    private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ViewModel.AutoSuggestBox_TextChanged(sender.Text);
    }


    private void CharacterSearchKeyShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SearchBox.Focus(FocusState.Keyboard);
    }

    private async void SearchBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        await ViewModel.SuggestionBox_Chosen((CharacterGridItemModel?)args.ChosenSuggestion);
    }

    private void ImageCommandsFlyout_OnOpening(object? sender, object e)
    {
        if (sender is not MenuFlyout menuFlyout)
            return;

        if (menuFlyout.Target.DataContext is not CharacterGridItemModel character)
            return;

        ViewModel.OnRightClickContext(character);
    }

    private void SetGridDropHereVisibility(Grid characterThumbnail, Visibility visibility)
    {
        var dropHereIcon = ((FontIcon)characterThumbnail.FindName("DropHereIcon"));
        dropHereIcon.Visibility = visibility;
        var dropHereBorder = ((Border)characterThumbnail.FindName("DropHereBorder"));
        dropHereBorder.Visibility = visibility;
    }

    private void CharacterThumbnail_OnDragEnter(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;

        var gridItem = ((Grid)sender);
        SetGridDropHereVisibility(gridItem, Visibility.Visible);
    }

    private void CharacterThumbnail_OnDragLeave(object sender, DragEventArgs e)
    {
        var gridItem = ((Grid)sender);
        SetGridDropHereVisibility(gridItem, Visibility.Collapsed);
    }

    private async void CharacterThumbnail_OnDrop(object sender, DragEventArgs e)
    {

        if (((Grid)sender).DataContext is CharacterGridItemModel characterGridItem)
        {
            var urlFormats = new[] { "Text", "UniformResourceLocatorW", "UniformResourceLocator" };
            if (urlFormats.All(format => e.DataView.Contains(format)))
            {
                try
                {
                    var uri = await e.DataView.GetWebLinkAsync();
                    await ViewModel.ModUrlDroppedOnCharacterAsync(characterGridItem, uri);

                }
                catch (Exception)
                {
                    // ignored
                }
            }
            else
                await ViewModel.ModDroppedOnCharacterAsync(characterGridItem, await e.DataView.GetStorageItemsAsync());

        }

        var gridItem = ((Grid)sender);
        SetGridDropHereVisibility(gridItem, Visibility.Collapsed);
    }

    private void DragAndDropArea_OnDragEnter(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        Log.Information("DragEnter_DragAndDropArea_OnDragEnter");
    }

    private void DragAndDropArea_OnDrop(object sender, DragEventArgs e)
    {
        Log.Information("Drop_DragAndDropArea_OnDrop");
    }

    private void BitmapImage_OnImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        Log.Error("Failed to load dock panel element icon. Reason: {e}", e.ErrorMessage);
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.DockPanelVM.ElementSelectionChanged(e.AddedItems.OfType<ElementIcon>(),
            e.RemovedItems.OfType<ElementIcon>());
    }

    private void SortingComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SortByCommand.Execute(e.AddedItems.OfType<SortingMethod>());
    }
}