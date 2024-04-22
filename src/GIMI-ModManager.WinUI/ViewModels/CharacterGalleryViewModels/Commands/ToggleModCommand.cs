using CommunityToolkit.Mvvm.Input;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;

public partial class CharacterGalleryViewModel
{
    private bool CanToggleMod(ModGridItemVm? thisMod)
    {
        return !IsNavigating() && !IsBusy && thisMod is not null;
    }

    // This function is called from the ModModel _toggleMod delegate.
    // This is a hacky way to get the toggle button to work.
    [RelayCommand(CanExecute = nameof(CanToggleMod))]
    private async Task ToggleMod(ModGridItemVm thisMod)
    {
        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                var modList = _skinManagerService.GetCharacterModList(thisMod.Character);
                if (thisMod.IsEnabled)
                    modList.DisableMod(thisMod.Id);
                else
                    modList.EnableMod(thisMod.Id);
            });

            // TODO: Replace entire griditem with updated one
            thisMod.IsEnabled = !thisMod.IsEnabled;
            await _elevatorService.RefreshGenshinMods();
        }
        finally
        {
            IsBusy = false;
        }
    }
}