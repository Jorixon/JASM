using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Models.CustomControlTemplates;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;

public partial class CharacterGalleryViewModel
{
    private bool CanChangeSkin()
    {
        return !IsNavigating && !IsBusy && CharacterSkins.Count > 0 && _selectedSkin != null &&
               _moddableObject is ICharacter;
    }

    [RelayCommand(CanExecute = nameof(CanChangeSkin))]
    private async Task ChangeSkinCommand(SelectCharacterTemplate characterSkin)
    {
        var character = (ICharacter)_moddableObject!;
        var selectedSkin = character.Skins.FirstOrDefault(sk => sk.InternalNameEquals(characterSkin.InternalName));

        if (selectedSkin is null)
            return;

        _selectedSkin = selectedSkin;
        characterSkin.IsSelected = true;
        CharacterSkins.Where(c => !selectedSkin.InternalNameEquals(c.InternalName)).ForEach(c => c.IsSelected = false);

        OnPropertyChanged(nameof(ModdableObjectImagePath));
        OnPropertyChanged(nameof(ModdableObjectName));

        await ReloadModsAsync();
    }
}
