using Windows.Storage;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI.Controls;
using GIMI_ModManager.WinUI.Models.Options;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;

public partial class CharacterDetailsViewModel
{
    [ObservableProperty] private bool _isSingleSelectEnabled;
    [ObservableProperty] private bool _isModFolderNameColumnVisible;


    private async Task InitToolbarAsync()
    {
        var settings = await ReadSettingsAsync();
        IsSingleSelectEnabled = settings.SingleSelect;
        ModGridVM.GridSelectionMode = IsSingleSelectEnabled ? DataGridSelectionMode.Single : DataGridSelectionMode.Extended;
        IsModFolderNameColumnVisible = settings.ModFolderNameColumnVisible;
    }

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

    [RelayCommand(CanExecute = nameof(IsNotHardBusy))]
    private async Task RefreshAllModsAsync()
    {
        await CommandWrapperAsync(true,
            () => ModGridVM.ReloadAllModsAsync(minimumWaitTime: TimeSpan.FromMilliseconds(300))).ConfigureAwait(false);
    }


    [RelayCommand(CanExecute = nameof(IsNotHardBusy))]
    private async Task ToggleSingleSelectAsync()
    {
        await CommandWrapperAsync(false, async () =>
        {
            var settings = await ReadSettingsAsync();
            settings.SingleSelect = !IsSingleSelectEnabled;
            await SaveSettingsAsync(settings);

            IsSingleSelectEnabled = settings.SingleSelect;

            var firstModSelected = ModGridVM.SelectedMods.FirstOrDefault();
            ModGridVM.GridSelectionMode = IsSingleSelectEnabled ? DataGridSelectionMode.Single : DataGridSelectionMode.Extended;
            if (firstModSelected is not null)
                ModGridVM.SetSelectedMod(firstModSelected.Id);
        }).ConfigureAwait(false);
    }


    [RelayCommand]
    private async Task ToggleHideModFolderColumnAsync()
    {
        await CommandWrapperAsync(false, async () =>
        {
            var settings = await ReadSettingsAsync();
            settings.ModFolderNameColumnVisible = !IsModFolderNameColumnVisible;
            await SaveSettingsAsync(settings);

            IsModFolderNameColumnVisible = settings.ModFolderNameColumnVisible;
            ModGridVM.IsModFolderNameColumnVisible = settings.ModFolderNameColumnVisible;
        }).ConfigureAwait(false);
    }
}