using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.GamesService.Interfaces;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;

public partial class CharacterGalleryViewModel
{
    private bool CanToggleMod(ModGridItemVm? thisMod)
    {
        return !IsNavigating && !IsBusy && thisMod is not null;
    }

    // This function is called from the ModModel _toggleMod delegate.
    // This is a hacky way to get the toggle button to work.
    [RelayCommand(CanExecute = nameof(CanToggleMod))]
    private async Task ToggleMod(ModGridItemVm thisMod)
    {
        if (IsNavigating)
            return;

        IsBusy = true;
        try
        {
            CharacterSkinEntry thisSkinMod = null!;
            var mods = new List<CharacterSkinEntry>();

            await Task.Run(async () =>
            {
                var allSkinMods = _modList.Mods;
                thisSkinMod = allSkinMods.First(m => m.Id == thisMod.Id);

                if (_moddableObject is ICharacter { Skins.Count: > 1 } character)
                {
                    var selectedSkin = _selectedSkin!;

                    var skinEntries = _characterSkinService.GetModsForSkinAsync(selectedSkin);


                    await foreach (var skinEntry in skinEntries.ConfigureAwait(false))
                    {
                        var mod = allSkinMods.FirstOrDefault(m => m.Id == skinEntry.Id);
                        if (mod is not null)
                            mods.Add(mod);
                    }
                }
                else
                    mods.AddRange(allSkinMods);
            });


            await ToggleOnlyMod(mods, thisSkinMod);
            await _elevatorService.RefreshGenshinMods();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to toggle mod");
        }
        finally
        {
            IsBusy = false;
        }
    }


    private async Task ToggleOnlyMod(IEnumerable<CharacterSkinEntry> skinEntries, CharacterSkinEntry modToEnable)
    {
        if (IsNavigating)
            return;

        var disableOtherMods = modToEnable.IsEnabled == false;

        foreach (var skinEntry in skinEntries)
        {
            if (skinEntry.Id == modToEnable.Id)
            {
                await SetModIsEnabled(skinEntry, !skinEntry.IsEnabled);
                continue;
            }

            if (disableOtherMods && IsSingleSelection)
                await SetModIsEnabled(skinEntry, false);
        }
    }

    private async Task SetModIsEnabled(CharacterSkinEntry skinEntry, bool setStatus)
    {
        // TODO: Update entire item instead of just the IsEnabled property

        switch (setStatus)
        {
            case true when !skinEntry.IsEnabled:
                _modList!.EnableMod(skinEntry.Id);
                await UpdateGridItemAsync(skinEntry).ConfigureAwait(false);
                break;
            case false when skinEntry.IsEnabled:
                _modList!.DisableMod(skinEntry.Id);
                await UpdateGridItemAsync(skinEntry).ConfigureAwait(false);
                break;
        }
    }
}