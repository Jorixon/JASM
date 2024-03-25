using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class PresetViewModel(
    ModPresetService modPresetService,
    UserPreferencesService userPreferencesService,
    NotificationManager notificationManager,
    IGameService gameService,
    ISkinManagerService skinManagerService,
    IWindowManagerService windowManagerService,
    CharacterSkinService characterSkinService)
    : ObservableRecipient, INavigationAware
{
    private readonly CharacterSkinService _characterSkinService = characterSkinService;
    private readonly IWindowManagerService _windowManagerService = windowManagerService;
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly ModPresetService _modPresetService = modPresetService;
    private readonly UserPreferencesService _userPreferencesService = userPreferencesService;
    private readonly NotificationManager _notificationManager = notificationManager;
    private readonly IGameService _gameService = gameService;
    private readonly Random _random = new();


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreatePresetCommand), nameof(DeletePresetCommand), nameof(ApplyPresetCommand),
        nameof(DuplicatePresetCommand), nameof(RenamePresetCommand), nameof(ReorderPresetsCommand),
        nameof(SaveActivePreferencesCommand), nameof(ApplyPresetCommand))]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;


    [ObservableProperty] private ObservableCollection<ModPresetVm> _presets = new();

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(CreatePresetCommand))]
    private string _newPresetNameInput = string.Empty;

    [ObservableProperty] private bool _createEmptyPresetInput;
    private bool _isNotBusy;


    private bool CanCreatePreset()
    {
        return !IsBusy &&
               !NewPresetNameInput.IsNullOrEmpty() &&
               Presets.All(p => !p.Name.Trim().Equals(NewPresetNameInput.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand(CanExecute = nameof(CanCreatePreset))]
    private async Task CreatePreset()
    {
        IsBusy = true;
        try
        {
            await Task.Run(() => _modPresetService.CreatePresetAsync(NewPresetNameInput, CreateEmptyPresetInput));
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification("Failed to create preset", e.Message, TimeSpan.FromSeconds(5));
        }

        ReloadPresets();
        NewPresetNameInput = string.Empty;
        CreateEmptyPresetInput = false;
        IsBusy = false;
    }

    private bool CanDuplicatePreset() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanDuplicatePreset))]
    private async Task DuplicatePreset(ModPresetVm preset)
    {
        IsBusy = true;

        try
        {
            await _modPresetService.DuplicatePresetAsync(preset.Name);
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification("Failed to duplicate preset", e.Message, TimeSpan.FromSeconds(5));
        }

        ReloadPresets();
        IsBusy = false;
    }

    private bool CanDeletePreset() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanDeletePreset))]
    private async Task DeletePreset(ModPresetVm preset)
    {
        IsBusy = true;

        try
        {
            await Task.Run(() => _modPresetService.DeletePresetAsync(preset.Name));
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification("Failed to delete preset", e.Message, TimeSpan.FromSeconds(5));
        }

        ReloadPresets();
        IsBusy = false;
    }


    private bool CanApplyPreset() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanApplyPreset))]
    private async Task ApplyPreset(ModPresetVm preset)
    {
        IsBusy = true;

        try
        {
            await Task.Run(() => _modPresetService.ApplyPresetAsync(preset.Name));
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification("Failed to apply preset", e.Message, TimeSpan.FromSeconds(5));
        }

        ReloadPresets();
        IsBusy = false;
    }

    private bool CanRenamePreset()
    {
        return !IsBusy;
    }

    [RelayCommand(CanExecute = nameof(CanRenamePreset))]
    private async Task RenamePreset(ModPresetVm preset)
    {
        IsBusy = true;

        try
        {
            await Task.Run(() => _modPresetService.RenamePresetAsync(preset.Name, preset.NameInput.Trim()));
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification("Failed to rename preset", e.Message, TimeSpan.FromSeconds(5));
        }

        ReloadPresets();
        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task ReorderPresets()
    {
        IsBusy = true;

        try
        {
            await Task.Run(() => _modPresetService.SavePresetOrderAsync(Presets.Select(p => p.Name)));
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification("Failed to save preset order", e.Message, TimeSpan.FromSeconds(5));
        }

        ReloadPresets();
        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task SaveActivePreferences()
    {
        IsBusy = true;

        try
        {
            await Task.Run(() => _userPreferencesService.SaveModPreferencesAsync());
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification("Failed to save active preferences", e.Message,
                TimeSpan.FromSeconds(5));
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task ApplySavedModPreferences()
    {
        IsBusy = true;

        try
        {
            await Task.Run(() => _userPreferencesService.SetModPreferencesAsync());
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification("Failed to apply saved preferences", e.Message,
                TimeSpan.FromSeconds(5));
        }

        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RandomizeMods()
    {
        var dialog = new ContentDialog
        {
            Title = "Randomize Mods",
            PrimaryButtonText = "Randomize",
            CloseButtonText = "Cancel"
        };


        var categories = _gameService.GetCategories();

        var stackPanel = new StackPanel();


        foreach (var category in categories)
        {
            var checkBox = new CheckBox
            {
                Content = category.DisplayNamePlural,
                IsChecked = true
            };

            stackPanel.Children.Add(checkBox);
        }

        dialog.Content = stackPanel;

        var result = await _windowManagerService.ShowDialogAsync(dialog);


        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        using var _ = StartBusy();


        var selectedCategories = stackPanel.Children
            .OfType<CheckBox>()
            .Where(c => c.IsChecked == true)
            .Select(c => categories.First(cat => cat.DisplayNamePlural.Equals(c.Content)))
            .ToList();

        if (selectedCategories.Count == 0)
        {
            _notificationManager.ShowNotification("No categories selected", "No categories were selected to randomize.",
                TimeSpan.FromSeconds(5));
            return;
        }


        try
        {
            await Task.Run(async () =>
            {
                var modLists = _skinManagerService.CharacterModLists
                    .Where(modList => selectedCategories.Contains(modList.Character.ModCategory))
                    .Where(modList => !modList.Character.IsMultiMod)
                    .ToList();

                foreach (var modList in modLists)
                {
                    var mods = modList.Mods.ToList();

                    if (mods.Count == 0)
                        continue;

                    // Need special handling for characters because they have an in game skins
                    if (modList.Character is ICharacter { Skins.Count: > 1 } character)
                    {
                        var skinModMap = await _characterSkinService.GetAllModsBySkinAsync(character)
                            .ConfigureAwait(false);
                        if (skinModMap is null)
                            continue;

                        // Don't know what to do with undetectable mods
                        skinModMap.UndetectableMods.ForEach(mod => modList.DisableMod(mod.Id));

                        foreach (var (_, skinMods) in skinModMap.ModsBySkin)
                        {
                            if (skinMods.Count == 0)
                                continue;

                            foreach (var mod in skinMods.Where(mod => modList.IsModEnabled(mod)))
                            {
                                modList.DisableMod(mod.Id);
                            }

                            var randomModIndex = _random.Next(0, skinMods.Count);
                            modList.EnableMod(skinMods.ElementAt(randomModIndex).Id);
                        }


                        continue;
                    }


                    foreach (var characterSkinEntry in mods.Where(characterSkinEntry => characterSkinEntry.IsEnabled))
                    {
                        modList.DisableMod(characterSkinEntry.Id);
                    }


                    var randomIndex = _random.Next(0, mods.Count);
                    modList.EnableMod(mods[randomIndex].Id);
                }
            });
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification("Failed to randomize mods", e.Message, TimeSpan.FromSeconds(5));
            return;
        }


        _notificationManager.ShowNotification("Mods randomized",
            "Mods have been randomized for the categories: " +
            string.Join(", ",
                selectedCategories.Select(c =>
                    c.DisplayNamePlural)),
            TimeSpan.FromSeconds(5));
    }

    public void OnNavigatedTo(object parameter)
    {
        ReloadPresets();
    }

    public void OnNavigatedFrom()
    {
    }

    private void ReloadPresets()
    {
        var presets = _modPresetService.GetPresets().OrderBy(i => i.Index);
        Presets.Clear();
        foreach (var preset in presets)
        {
            Presets.Add(new ModPresetVm(preset)
            {
                RenamePresetCommand = RenamePresetCommand,
                DuplicatePresetCommand = DuplicatePresetCommand,
                DeletePresetCommand = DeletePresetCommand,
                ApplyPresetCommand = ApplyPresetCommand
            });
        }
    }

    public class StartOperation : IDisposable
    {
        public StartOperation(Action setIsDone)
        {
            _setIsDone = setIsDone;
        }

        private readonly Action _setIsDone;

        public void Dispose()
        {
            _setIsDone();
        }
    }

    private StartOperation StartBusy()
    {
        IsBusy = true;
        return new StartOperation(() => IsBusy = false);
    }
}

public partial class ModPresetVm : ObservableObject
{
    public ModPresetVm(ModPreset preset)
    {
        Name = preset.Name;
        NameInput = Name;
        EnabledModsCount = preset.Mods.Count;
    }

    public string Name { get; }
    public int EnabledModsCount { get; set; }

    [ObservableProperty] private string _nameInput = string.Empty;

    [ObservableProperty] private bool _isEditingName;

    [ObservableProperty] private string _renameButtonText = RenameText;

    [RelayCommand]
    private async Task StartEditingName()
    {
        if (IsEditingName && RenameButtonText == ConfirmText)
        {
            if (NameInput.Trim().IsNullOrEmpty() || NameInput.Trim() == Name)
            {
                ResetInput();
                return;
            }

            if (RenamePresetCommand.CanExecute(this))
            {
                await RenamePresetCommand.ExecuteAsync(this);
                ResetInput();
                return;
            }

            ResetInput();
            return;
        }


        IsEditingName = true;
        NameInput = Name;
        RenameButtonText = ConfirmText;

        void ResetInput()
        {
            NameInput = Name;
            IsEditingName = false;
            RenameButtonText = RenameText;
        }
    }

    public required IAsyncRelayCommand RenamePresetCommand { get; init; }
    public required IAsyncRelayCommand DuplicatePresetCommand { get; init; }
    public required IAsyncRelayCommand DeletePresetCommand { get; init; }
    public required IAsyncRelayCommand ApplyPresetCommand { get; init; }

    private const string RenameText = "Rename";
    private const string ConfirmText = "Save New Name";
}