using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.WinUI.Models.Settings;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;

public partial class CharacterGalleryViewModel
{
    private bool CanToggleView()
    {
        return !IsNavigating() && !IsBusy;
    }

    [RelayCommand(CanExecute = nameof(CanToggleView))]
    private async Task ToggleView()
    {
        var settings =
            await _localSettingsService
                .ReadOrCreateSettingAsync<CharacterDetailsSettings>(CharacterDetailsSettings.Key);

        settings.GalleryView = !settings.GalleryView;

        await _localSettingsService.SaveSettingAsync(CharacterDetailsSettings.Key, settings);


        _navigationService.NavigateTo(typeof(CharacterDetailsViewModel).FullName!, _moddableObject);
        _navigationService.ClearBackStack(1);
    }
}