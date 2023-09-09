using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;


namespace GIMI_ModManager.WinUI.Views;

public sealed partial class CharactersPage : Page
{
    public CharactersViewModel ViewModel { get; }

    public CharactersPage()
    {
        ViewModel = App.GetService<CharactersViewModel>();
        this.InitializeComponent();
        Loaded += (sender, args) => SearchBox.Focus(FocusState.Keyboard);
    }


    private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        var suitableItemsCount = ViewModel.AutoSuggestBox_TextChanged(sender.Text);
    }

    private void AutoSuggestBox_OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        ViewModel.SuggestionBox_Chosen((GenshinCharacter)args.SelectedItem);
        sender.IsEnabled = false;
        sender.Text = string.Empty;
    }

    private void CharacterSearchKeyShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SearchBox.Focus(FocusState.Keyboard);
    }

    private void SearchBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is not null)
            ViewModel.SuggestionBox_Chosen((GenshinCharacter)args.ChosenSuggestion);

        if (ViewModel.SuggestionsBox.Count > 0)
        {
            ViewModel.SuggestionBox_Chosen(ViewModel.SuggestionsBox[0]);
            sender.IsEnabled = false;
            sender.Text = string.Empty;
        }
    }

    private void ImageCommandsFlyout_OnOpening(object? sender, object e)
    {

        if (sender is not MenuFlyout menuFlyout)
            return;
        
        if (menuFlyout.Target.DataContext is not CharacterGridItemModel character)
            return;

        ViewModel.OnRightClickContext(character);

    }
}