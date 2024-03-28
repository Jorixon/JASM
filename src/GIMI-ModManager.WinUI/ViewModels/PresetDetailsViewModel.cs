using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkitWrapper;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Services.ModPresetService;
using GIMI_ModManager.Core.Services.ModPresetService.Models;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.Views;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GIMI_ModManager.WinUI.ViewModels;

public sealed partial class PresetDetailsViewModel(
    INavigationService navigationService,
    ModPresetService modPresetService,
    ISkinManagerService skinManagerService,
    ImageHandlerService imageHandlerService,
    NotificationManager notificationManager,
    IWindowManagerService windowManagerService,
    BusyService busyService)
    : ObservableRecipient, INavigationAware
{
    private readonly ImageHandlerService _imageHandlerService = imageHandlerService;
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly INavigationService _navigationService = navigationService;
    private readonly ModPresetService _modPresetService = modPresetService;
    private readonly NotificationManager _notificationManager = notificationManager;
    private readonly IWindowManagerService _windowManagerService = windowManagerService;
    private readonly BusyService _busyService = busyService;

    private const string SelectModsWindowKey = "SelectModsWindow";

    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyCanExecuteChangedFor(nameof(RemoveModFromPresetCommand), nameof(NavigateToModCommand))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(GetPageTitle))]
    private string _presetName = string.Empty;

    public string GetPageTitle => $"Preset Details: {PresetName}";

    public ObservableCollection<ModPresetEntryDetailedVm> ModEntries { get; } = new();


    public async void OnNavigatedTo(object parameter)
    {
        IsBusy = true;
        if (parameter is not PresetDetailsNavigationParameter navigationParameter)
        {
            ErrorNavigateBack();
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        var ct = _cancellationTokenSource.Token;
        try
        {
            await Task.Run(() => _modPresetService.RefreshAsync(ct), ct);
            var preset = _modPresetService.GetPresets().FirstOrDefault(p => p.Name == navigationParameter.PresetName);

            if (preset is null)
            {
                ErrorNavigateBack();
                return;
            }

            PresetName = preset.Name;

            foreach (var modPresetEntry in SortDefaultOrder(preset.Mods))
            {
                var characterSkinEntry = _skinManagerService.GetModEntryById(modPresetEntry.ModId);

                if (characterSkinEntry is null)
                {
                    var modEntry = new ModPresetEntryDetailedVm(modPresetEntry,
                        _imageHandlerService.PlaceholderImageUri)
                    {
                        NavigateToModCommand = NavigateToModCommand,
                        RemoveModFromPresetCommand = RemoveModFromPresetCommand
                    };

                    ModEntries.Add(modEntry);
                    continue;
                }

                var modSettings = await characterSkinEntry.Mod.Settings.TryReadSettingsAsync(cancellationToken: ct);

                if (modSettings is null)
                {
                    ErrorNavigateBack(); //TODO:
                    return;
                }


                var presetModEntryVm = new ModPresetEntryDetailedVm(modPresetEntry,
                        modSettings.ImagePath ?? _imageHandlerService.PlaceholderImageUri)
                    {
                        NavigateToModCommand = NavigateToModCommand,
                        RemoveModFromPresetCommand = RemoveModFromPresetCommand
                    }
                    .WithModdableObject(characterSkinEntry.ModList.Character);

                if (modSettings.ModUrl is not null && presetModEntryVm.SourceUrl is null)
                {
                    presetModEntryVm.SourceUrl = modSettings.ModUrl;
                }

                ModEntries.Add(presetModEntryVm);
            }

            var cts = _cancellationTokenSource;
            _cancellationTokenSource = null;
            cts.Dispose();
        }
        catch (TaskCanceledException e)
        {
        }
        catch (OperationCanceledException e)
        {
        }

        IsBusy = false;
    }


    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private void NavigateToMod(ModPresetEntryDetailedVm? vm)
    {
        if (vm is null || !vm.HasConnectedCharacter)
            return;

        IsBusy = true;
        _navigationService.NavigateTo(typeof(CharacterDetailsViewModel).FullName!, vm.ModdableObject);
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RemoveModFromPreset(ModPresetEntryDetailedVm? modPresetEntryVm)
    {
        if (modPresetEntryVm is null)
            return;


        IsBusy = true;
        try
        {
            await Task.Run(() => _modPresetService.DeleteModEntryAsync(PresetName, modPresetEntryVm.ModId));

            ModEntries.Remove(modPresetEntryVm);

            _notificationManager.ShowNotification("Mod removed from preset",
                $"Removed {(modPresetEntryVm.IsMissing ? "missing" : "")} mod '{modPresetEntryVm.Name}' from preset {PresetName}",
                null);
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification("Failed to remove mod from preset", e.Message, null);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddModsToPreset()
    {
        var mainWindowLock = _busyService.SetMainWindowBusy();
        SelectionResult? result;
        try
        {
            IsBusy = true;

            var presetModIds = ModEntries.Select(m => m.ModId).ToList();
            var selectableMods = _skinManagerService
                .GetAllMods(GetOptions.All)
                .Where(mod => !presetModIds.Contains(mod.Id))
                .ToList();

            var options = new InitOptions
            {
                SelectableMods = selectableMods.Select(m => m.Id).ToArray(),
                SelectionMode = ListViewSelectionMode.Single
            };

            var (modSelector, task) = ModSelector.Create(options);

            var window = new WindowEx()
            {
                SystemBackdrop = new MicaBackdrop(),
                Title = $"Select Mods",
                Content = modSelector,
                Width = 1200,
                Height = 750,
                MinHeight = 415,
                MinWidth = 1024
            };

            modSelector.CloseRequested += (_, _) => { window.Close(); };

            _windowManagerService.CreateWindow(window, SelectModsWindowKey);

            result = await task;

            if (result is null)
                return;

            var modId = result.ModIds.First();

            var modEntry = await Task.Run(() => _modPresetService.AddModEntryAsync(PresetName, modId));

            var modSettings = await selectableMods.First(m => m.Id == modId).Mod.Settings.TryReadSettingsAsync();

            var modEntryVm = new ModPresetEntryDetailedVm(modEntry,
                modSettings?.ImagePath ?? _imageHandlerService.PlaceholderImageUri)
            {
                NavigateToModCommand = NavigateToModCommand,
                RemoveModFromPresetCommand = RemoveModFromPresetCommand
            };

            ModEntries.Insert(0, modEntryVm);
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification("Failed to add mod to preset", e.Message, null);
        }
        finally
        {
            mainWindowLock.Dispose();
            IsBusy = false;
        }
    }

    public void OnNavigatedFrom()
    {
        _cancellationTokenSource?.Cancel();
    }


    private void ErrorNavigateBack()
    {
        App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            await Task.Delay(500);
            _navigationService.NavigateTo(typeof(PresetViewModel).FullName!);
            _navigationService.ClearBackStack(2);
        });
    }

    private IEnumerable<ModPresetEntry> SortDefaultOrder(IEnumerable<ModPresetEntry> modPresetEntries) =>
        modPresetEntries.OrderByDescending(m => m.IsMissing).ThenBy(m => m.AddedAt).ThenBy(m => m.CustomName);
}

public record PresetDetailsNavigationParameter(string PresetName);

public partial class ModPresetEntryDetailedVm : ModPresetEntryVm
{
    [ObservableProperty] private Uri _imageUri;


    public bool HasConnectedCharacter =>
        (CharacterUri != null || CharacterName != null) && ModdableObject is not null;

    public IModdableObject? ModdableObject;

    [ObservableProperty] private Uri? _characterUri;

    [ObservableProperty] private string? _characterName;


    public string? ModUrlName => SourceUrl?.Host;

    public bool HasSourceUrl => SourceUrl is not null;

    public string GoToText => $"Go to {ModdableObject?.ModCategory.DisplayName ?? string.Empty}";

    public ModPresetEntryDetailedVm(ModPresetEntry modEntry, Uri imageUri) : base(modEntry)
    {
        ImageUri = imageUri;
    }

    public ModPresetEntryDetailedVm WithModdableObject(IModdableObject moddableObject)
    {
        ModdableObject = moddableObject;
        CharacterUri = moddableObject.ImageUri;
        CharacterName = moddableObject.DisplayName;
        return this;
    }


    public required IRelayCommand NavigateToModCommand;
    public required IAsyncRelayCommand RemoveModFromPresetCommand;
}