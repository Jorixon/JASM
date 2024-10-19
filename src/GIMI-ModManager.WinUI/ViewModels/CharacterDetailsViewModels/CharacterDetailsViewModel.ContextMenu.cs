using System.Text;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;

public partial class CharacterDetailsViewModel
{
    private static bool _removeFromPresetCheckBox = false;
    private static bool _moveToRecycleBinCheckBox = true;

    private record ModToDelete(Guid Id, string DisplayName, string FolderPath, string FolderName)
    {
        public ModToDelete(ModToDelete m, Exception e, string? presetName = null) : this(m.Id, m.DisplayName, m.FolderPath, m.FolderName)
        {
            Exception = e;
            PresetName = presetName;
        }

        public Exception? Exception { get; }
        public string? PresetName { get; }
    }

    private bool CanDeleteMods() => IsNavigationFinished && !IsHardBusy && !IsSoftBusy && ModGridVM.SelectedMods.Count > 0;

    [RelayCommand(CanExecute = nameof(CanDeleteMods))]
    private async Task DeleteModsAsync()
    {
        await CommandWrapperAsync(true, async () =>
        {
            var selectedMods = ModGridVM.SelectedMods.Select(m => new ModToDelete(m.Id, m.DisplayName, m.AbsFolderPath, m.FolderName)).ToList();

            if (selectedMods.Count == 0)
                return;


            var shownCharacterName = ShownModObject.DisplayName;
            var selectedModsCount = selectedMods.Count;

            var modsToDeleteErrored = new List<ModToDelete>();
            var modsToDeletePresetError = new List<ModToDelete>();

            var modsDeleted = new List<ModToDelete>(selectedModsCount);

            var moveToRecycleBinCheckBox = new CheckBox()
            {
                Content = "Move to Recycle Bin?",
                IsChecked = _moveToRecycleBinCheckBox
            };

            var removeFromPresetsCheckBox = new CheckBox()
            {
                Content = "Remove from Presets?",
                IsChecked = _removeFromPresetCheckBox
            };


            var mods = new ListView()
            {
                ItemsSource = selectedMods.Select(m => m.DisplayName + " - " + m.FolderName),
                SelectionMode = ListViewSelectionMode.None
            };

            var scrollViewer = new ScrollViewer()
            {
                Content = mods,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 400
            };
            var stackPanel = new StackPanel()
            {
                Children =
                {
                    moveToRecycleBinCheckBox,
                    removeFromPresetsCheckBox,
                    scrollViewer
                }
            };

            var contentWrapper = new Grid()
            {
                MinWidth = 500,
                Children =
                {
                    stackPanel
                }
            };

            var dialog = new ContentDialog()
            {
                Title = $"Delete These {selectedModsCount} Mods?",
                Content = contentWrapper,
                PrimaryButtonText = "Delete",
                SecondaryButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };


            var result = await _windowManagerService.ShowDialogAsync(dialog);

            if (result != ContentDialogResult.Primary)
                return;
            var recycleMods = moveToRecycleBinCheckBox.IsChecked == true;
            var removeFromPresets = removeFromPresetsCheckBox.IsChecked == true;
            _moveToRecycleBinCheckBox = recycleMods;
            _removeFromPresetCheckBox = removeFromPresets;


            await Task.Run(async () =>
            {
                if (removeFromPresets)
                {
                    var modIdToPresetMap = await _presetService.FindPresetsForModsAsync(selectedMods.Select(m => m.Id), CancellationToken.None)
                        .ConfigureAwait(false);

                    foreach (var mod in selectedMods)
                    {
                        if (!modIdToPresetMap.TryGetValue(mod.Id, out var presets)) continue;

                        foreach (var preset in presets)
                        {
                            try
                            {
                                await _presetService.DeleteModEntryAsync(preset.Name, mod.Id, CancellationToken.None).ConfigureAwait(false);
                            }
                            catch (Exception e)
                            {
                                _logger.Error(e, "Error removing mod: {ModName} from preset: {PresetName} | mod path: {ModPath} ", mod.DisplayName,
                                    mod.FolderPath,
                                    preset.Name);
                                modsToDeletePresetError.Add(new ModToDelete(mod, e));
                            }
                        }
                    }
                }

                foreach (var mod in selectedMods)
                {
                    try
                    {
                        _modList.DeleteModBySkinEntryId(mod.Id, recycleMods);
                        modsDeleted.Add(mod);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Error deleting mod {ModName} | {ModPath}", mod.DisplayName, mod.FolderPath);

                        modsToDeleteErrored.Add(new ModToDelete(mod, e));
                    }
                }
            });

            ModGridVM.QueueModRefresh();


            if (modsToDeleteErrored.Count > 0 || modsToDeletePresetError.Count > 0)
            {
                var content = new StringBuilder();

                content.AppendLine("Error deleting mods:");


                if (modsToDeletePresetError.Count > 0)
                {
                    content.AppendLine("Preset error Mods:");
                    foreach (var mod in modsToDeletePresetError)
                    {
                        content.AppendLine($"- {mod.DisplayName}");
                        content.AppendLine($"  - {mod.Exception?.Message}");
                        content.AppendLine($"  - {mod.PresetName}");
                    }
                }

                if (modsToDeleteErrored.Count > 0)
                {
                    content.AppendLine("Delete error Mods:");
                    foreach (var mod in modsToDeleteErrored)
                    {
                        content.AppendLine($"- {mod.DisplayName}");
                        content.AppendLine($"  - {mod.Exception?.Message}");
                    }
                }

                _notificationService.ShowNotification("Error Deleting Mods", content.ToString(), TimeSpan.FromSeconds(10));
                return;
            }


            _notificationService.ShowNotification($"{modsDeleted.Count} Mods Deleted",
                $"Successfully deleted {string.Join(", ", selectedMods.Select(m => m.DisplayName))} in {shownCharacterName} Mods Folder",
                TimeSpan.FromSeconds(5));
        }).ConfigureAwait(false);
    }
}