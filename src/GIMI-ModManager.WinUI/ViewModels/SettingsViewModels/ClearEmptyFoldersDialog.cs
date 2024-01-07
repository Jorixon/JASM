using System.Text;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.AppManagement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.ViewModels.SettingsViewModels;

internal class ClearEmptyFoldersDialog
{
    private readonly ISkinManagerService _skinManagerService = App.GetService<ISkinManagerService>();
    private readonly NotificationManager _notificationManager = App.GetService<NotificationManager>();
    private readonly IWindowManagerService _windowManagerService = App.GetService<IWindowManagerService>();


    public async Task ShowDialogAsync()
    {
        var dialog = new ContentDialog()
        {
            Title = "Clear Empty Folders",
            Content = new TextBlock()
            {
                Text =
                    "This will delete all empty folders in a character's modList if the folder is empty or only contains .JASM_ files/folders\n" +
                    "If a character folder is empty then it will be deleted as well.\n" +
                    "Empty folders in the root of the Mods folder will also be deleted",
                TextWrapping = TextWrapping.WrapWholeWords,
                IsTextSelectionEnabled = true
            },
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel"
        };


        var result = await _windowManagerService.ShowDialogAsync(dialog);

        if (result == ContentDialogResult.Primary)
        {
            var deletedFolders = await Task.Run(() => _skinManagerService.CleanCharacterFolders());
            var sb = new StringBuilder();
            sb.AppendLine("Deleted folders:");
            foreach (var folder in deletedFolders)
            {
                sb.AppendLine(folder.FullName);
            }

            var message = sb.ToString();

            _notificationManager.ShowNotification("Empty folders deleted", message, TimeSpan.FromSeconds(5));
        }
    }
}