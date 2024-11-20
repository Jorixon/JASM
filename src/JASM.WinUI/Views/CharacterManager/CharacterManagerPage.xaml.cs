using GIMI_ModManager.WinUI.ViewModels;
using GIMI_ModManager.WinUI.Views.CharacterManager;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class CharacterManagerPage : Page
{
    public CharacterManagerViewModel ViewModel { get; set; }

    public CharacterManagerPage()
    {
        ViewModel = App.GetService<CharacterManagerViewModel>();
        InitializeComponent();
        ViewModel.SetSelection += CharacterSelected;
        Loaded += (sender, args) => CharacterSearchBox.Focus(FocusState.Programmatic);
    }

    private void CharacterSelected(object? sender, CharacterManagerViewModel.SetSelectionArgs e)
    {
        if (e.Character is not null)
            EditFrame.Navigate(typeof(EditCharacterPage), e.Character.InternalName.Id);
        else
            EditFrame.Content = null;
    }

    private void CharacterSearchBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ViewModel.OnSearchTextChanged(sender.Text);
    }

    private void CharacterSearchBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ViewModel.SelectCharacterCommand.Execute(args.ChosenSuggestion);
    }

    private void CharacterSearchBox_OnSuggestionChosen(AutoSuggestBox sender,
        AutoSuggestBoxSuggestionChosenEventArgs args)
    {
    }

    private void CharacterSearchBox_Ctrl_F(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        CharacterSearchBox.Focus(FocusState.Keyboard);
    }
}