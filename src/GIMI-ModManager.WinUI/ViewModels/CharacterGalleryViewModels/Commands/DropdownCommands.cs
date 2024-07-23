using Windows.System;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;

public partial class CharacterGalleryViewModel
{
    private bool CanOpenModFolder(ModGridItemVm? vm) =>
        vm is not null && !IsNavigating && !IsBusy && !vm.FolderPath.IsNullOrEmpty() && Directory.Exists(vm.FolderPath);

    [RelayCommand(CanExecute = nameof(CanOpenModFolder))]
    private async Task OpenModFolder(ModGridItemVm vm)
    {
        await Launcher.LaunchFolderPathAsync(vm.FolderPath);
    }


    private bool CanOpenModUrl(ModGridItemVm? vm) => vm is not null && !IsNavigating && !IsBusy && vm.HasModUrl;

    [RelayCommand(CanExecute = nameof(CanOpenModUrl))]
    private async Task OpenModUrl(ModGridItemVm vm)
    {
        await Launcher.LaunchUriAsync(vm.ModUrl);
    }

    [RelayCommand(CanExecute = nameof(CanOpenModFolder))]
    private async Task DeleteMod(ModGridItemVm vm)
    {
        if (this._modList is null)
        {
            return;
        }
        this._modList.DeleteModBySkinEntryId(vm.Id);
        await this.ReloadModsAsync();
    }
}