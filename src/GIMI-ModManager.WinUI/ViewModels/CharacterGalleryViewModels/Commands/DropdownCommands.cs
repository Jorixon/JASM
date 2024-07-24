using Windows.System;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Helpers;
using Microsoft.UI.Xaml.Controls;
using GIMI_ModManager.WinUI.Services.Notifications;
using Windows.Storage;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Services.AppManagement;

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

    /// <summary>
    /// return the result of the dialog and if the checkbox "Do not ask again" is checked
    /// 
    private async Task<(ContentDialogResult, bool)> PromptDeleteDialog(ModGridItemVm vm)
    {
        var windowManager = App.GetService<IWindowManagerService>();

        var doNotAskAgainCheckBox = new CheckBox()
        {
            Content = "Do not ask again",
            IsChecked = false,
        };
        var stackPanel = new StackPanel()
        {
            Children =
            {
                new TextBlock()
                {
                    Text = $"Are you sure you want to delete {vm.Name}?",
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
                },
                doNotAskAgainCheckBox
            }
        };

        var dialog = new ContentDialog()
        {
            Title = "Delete mod",
            Content = stackPanel,
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        
        // get result and check if checkbox is checked
        var result = await windowManager.ShowDialogAsync(dialog);
        var doNotAskAgain = doNotAskAgainCheckBox.IsChecked == true;
        
        return (result, doNotAskAgain);
    }

    [RelayCommand(CanExecute = nameof(CanOpenModFolder))]
    private async Task DeleteMod(ModGridItemVm vm)
    {
        if (this._modList is null) {return;}

        var notificationManager = App.GetService<NotificationManager>();
        var localSettings = App.GetService<ILocalSettingsService>();
        var doNotShowPrompt = localSettings.ReadSetting<bool>("DoNotShowDeletePromptAgain");

        if (doNotShowPrompt != true)
        {
            var (result, doNotAskAgainChecked) = await PromptDeleteDialog(vm);
            if (doNotAskAgainChecked)
            {
                await localSettings.SaveSettingAsync("DoNotShowDeletePromptAgain", true);
            }

            if (result != ContentDialogResult.Primary)
            {
                return;
            }
        }

        try
        {
            this._modList.DeleteModBySkinEntryId(vm.Id);
            await this.ReloadModsAsync();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to delete mod");
            notificationManager.ShowNotification("Failed to delete mod", e.Message, TimeSpan.FromSeconds(10));
        }

        notificationManager.ShowNotification("Mod deleted", $"{vm.Name} has been deleted", TimeSpan.FromSeconds(5));
    }
}