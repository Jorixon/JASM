using Windows.Storage;
using Windows.System;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.WinUI.Models.Options;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;

public partial class CharacterDetailsViewModel
{
    [RelayCommand]
    private async Task OpenGIMIRootFolderAsync()
    {
        var options = await _localSettingsService.ReadSettingAsync<ModManagerOptions>(ModManagerOptions.Section) ??
                      new ModManagerOptions();
        if (string.IsNullOrWhiteSpace(options.GimiRootFolderPath)) return;
        await Launcher.LaunchFolderAsync(
            await StorageFolder.GetFolderFromPathAsync(options.GimiRootFolderPath));
    }

    [RelayCommand]
    private async Task OpenCharacterFolderAsync()
    {
        var directoryToOpen = new DirectoryInfo(_modList.AbsModsFolderPath);
        if (!directoryToOpen.Exists)
        {
            _modList.InstantiateCharacterFolder();
            directoryToOpen.Refresh();

            if (!directoryToOpen.Exists)
            {
                var parentDir = directoryToOpen.Parent;

                if (parentDir is null)
                {
                    _logger.Error("Could not find parent directory of {Directory}", directoryToOpen.FullName);
                    return;
                }

                directoryToOpen = parentDir;
            }
        }

        await Launcher.LaunchFolderAsync(
            await StorageFolder.GetFolderFromPathAsync(directoryToOpen.FullName));
    }


    [RelayCommand]
    private async Task OpenModFolderAsync()
    {
        if (ModGridVM.SelectedMods.Count != 1) return;

        var mod = ModGridVM.SelectedMods.First();
        var directoryToOpen = new DirectoryInfo(mod.AbsFolderPath);
        if (!directoryToOpen.Exists)
        {
            _logger.Error("Could not find directory {Directory}", directoryToOpen.FullName);
            return;
        }

        await Launcher.LaunchFolderAsync(
            await StorageFolder.GetFolderFromPathAsync(directoryToOpen.FullName));
    }
}