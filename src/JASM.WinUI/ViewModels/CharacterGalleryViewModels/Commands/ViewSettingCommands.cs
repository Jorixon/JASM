using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.WinUI.Models.Settings;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;

public partial class CharacterGalleryViewModel
{
    private bool CanToggleSingleSelection()
    {
        return !IsNavigating && !IsBusy;
    }

    [RelayCommand(CanExecute = nameof(CanToggleSingleSelection))]
    private async Task ToggleSingleSelection()
    {
        var settings = await _localSettingsService
            .ReadOrCreateSettingAsync<CharacterGallerySettings>(CharacterGallerySettings.Key);

        settings.IsSingleSelection = !settings.IsSingleSelection;

        await _localSettingsService.SaveSettingAsync(CharacterGallerySettings.Key, settings);

        IsSingleSelection = settings.IsSingleSelection;
    }

    private bool CanSetHeightWidth(SetHeightWidth _)
    {
        return !IsNavigating && !IsBusy;
    }

    [RelayCommand(CanExecute = nameof(CanSetHeightWidth))]
    private async Task SetHeightWidth(SetHeightWidth setHeightWidth)
    {
        var settings = await _localSettingsService
            .ReadOrCreateSettingAsync<CharacterGallerySettings>(CharacterGallerySettings.Key);

        settings.ItemHeight = setHeightWidth.Height;
        settings.ItemDesiredWidth = setHeightWidth.Width;

        await _localSettingsService.SaveSettingAsync(CharacterGallerySettings.Key, settings);


        GridItemHeight = settings.ItemHeight;
        GridItemWidth = settings.ItemDesiredWidth;
    }
}

public class SetHeightWidth
{
    public SetHeightWidth(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Height { get; set; }

    public int Width { get; set; }
}