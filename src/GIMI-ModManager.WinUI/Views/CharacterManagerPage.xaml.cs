using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class CharacterManagerPage : Page
{
    public CharacterManagerViewModel ViewModel { get; set; }

    public CharacterManagerPage()
    {
        ViewModel = App.GetService<CharacterManagerViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
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
}