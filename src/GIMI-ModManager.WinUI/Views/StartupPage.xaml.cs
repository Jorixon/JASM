using GIMI_ModManager.WinUI.ViewModels;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using GIMI_ModManager.WinUI.Views.Controls;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class StartupPage : Page
{
    public StartupViewModel ViewModel { get; }

    public StartupPage()
    {
        ViewModel = App.GetService<StartupViewModel>();
        InitializeComponent();
    }

    private void GimiFolder_OnPathChangedEvent(object? sender, FolderSelector.StringEventArgs e)
        => ViewModel.PathToGIMIFolderPicker.Validate(e.Value);


    private void ModsFolder_OnPathChangedEvent(object? sender, FolderSelector.StringEventArgs e)
        => ViewModel.PathToModsFolderPicker.Validate(e.Value);

    private async void GameSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        await ViewModel.SetGameCommand.ExecuteAsync(((GameComboBoxEntryVM)e.AddedItems[0]!).Value.ToString()).ConfigureAwait(false);
    }
}