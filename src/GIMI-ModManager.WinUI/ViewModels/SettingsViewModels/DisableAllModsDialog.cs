using System.Text;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels.SettingsViewModels;

public class DisableAllModsDialog
{
    private readonly ISkinManagerService _skinManagerService = App.GetService<ISkinManagerService>();
    private readonly IGameService _gameService = App.GetService<IGameService>();
    private readonly NotificationManager _notificationManager = App.GetService<NotificationManager>();
    private readonly IWindowManagerService _windowManagerService = App.GetService<IWindowManagerService>();
    private readonly ILogger _logger = App.GetService<ILogger>().ForContext<DisableAllModsDialog>();

    public async Task ShowDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Disable Mods",
            PrimaryButtonText = "Disable Mods in Categories",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };


        var categories = _gameService.GetCategories();

        var stackPanel = new StackPanel();

        stackPanel.Children.Add(new TextBlock
        {
            Text = "Select the categories you want to disable mods for:",
            IsTextSelectionEnabled = true
        });


        foreach (var category in categories)
        {
            var checkBox = new CheckBox
            {
                Content = category.DisplayNamePlural,
                IsChecked = true
            };

            stackPanel.Children.Add(checkBox);
        }


        stackPanel.Children.Add(new TextBlock
        {
            Text = "I suggest creating a preset (or a backup) of your mods before disabling mods if you have a lot of enabled mods.\n\n" +
                   "Only mods tracked by JASM will be disabled within the selected categories",
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.WrapWholeWords,
            Margin = new Thickness(0, 10, 0, 0)
        });


        dialog.Content = stackPanel;

        var result = await _windowManagerService.ShowDialogAsync(dialog);


        if (result != ContentDialogResult.Primary)
        {
            return;
        }


        var selectedCategories = stackPanel.Children
            .OfType<CheckBox>()
            .Where(c => c.IsChecked == true)
            .Select(c => categories.First(cat => cat.DisplayNamePlural.Equals(c.Content)))
            .ToList();

        if (selectedCategories.Count == 0)
        {
            _notificationManager.ShowNotification("No categories selected", "No categories were selected to disable mods.",
                TimeSpan.FromSeconds(5));
            return;
        }


        var modLists = _skinManagerService.CharacterModLists.Where(m => selectedCategories.Contains(m.Character.ModCategory)).ToList();

        var modListDisableTask = new List<Task<List<string>>>();


        foreach (var modList in modLists)
        {
            var task = Task.Run(() =>
            {
                var modsToDisable = modList.Mods.Where(m => m.IsEnabled).ToArray();
                var errors = new List<string>();
                foreach (var modEntry in modsToDisable)
                {
                    try
                    {
                        modList.DisableMod(modEntry.Id);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Error while disabling mod.");
                        errors.Add($"{modEntry.Mod.FullPath}: {e.Message}");
                    }
                }

                return errors;
            });

            modListDisableTask.Add(task);
        }

        var errorsList = await Task.WhenAll(modListDisableTask);
        var errors = errorsList.SelectMany(e => e).ToArray();

        if (errors.Length == 0)
        {
            _notificationManager.ShowNotification("Mods disabled",
                $"All tracked mods have been disabled for the selected categories: {string.Join(',', selectedCategories.Select(c => c.DisplayNamePlural))}",
                TimeSpan.FromSeconds(5));
            return;
        }


        var sb = new StringBuilder();
        sb.AppendLine("An error occured for the following mods:");

        foreach (var error in errors)
        {
            sb.AppendLine(error);
        }


        _notificationManager.ShowNotification("Errors while disabling mods", sb.ToString(), TimeSpan.FromSeconds(10));
    }
}