using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkitWrapper;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Models.CustomControlTemplates;
using GIMI_ModManager.WinUI.Models.Options;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using GIMI_ModManager.WinUI.Views;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class CharacterDetailsViewModel : ObservableRecipient, INavigationAware
{
    private readonly IGameService _gameService;
    private readonly ILogger _logger;
    private readonly INavigationService _navigationService;
    private readonly ISkinManagerService _skinManagerService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IWindowManagerService _windowManagerService;
    private readonly NotificationManager _notificationService;
    private readonly ModDragAndDropService _modDragAndDropService;
    private readonly ModCrawlerService _modCrawlerService;
    private readonly ModNotificationManager _modNotificationManager;
    private readonly ModSettingsService _modSettingsService;
    private readonly ImageHandlerService _imageHandlerService;
    private readonly ElevatorService _elevatorService;

    private ICharacterModList _modList = null!;
    public ModListVM ModListVM { get; } = null!;
    public ModPaneVM ModPaneVM { get; } = null!;

    public MoveModsFlyoutVM MoveModsFlyoutVM { get; private set; }

    [ObservableProperty] private IModdableObject _shownCharacter = null!;

    public readonly ObservableCollection<SelectCharacterTemplate> SelectableInGameSkins = new();

    [ObservableProperty] public bool _multipleInGameSkins = false;

    [ObservableProperty] private ICharacterSkin? _selectedInGameSkin;

    [ObservableProperty] private Uri _moddableObjectImage;

    private static readonly Dictionary<ICharacter, string> _lastSelectedSkin = new();

    public ModListVM.SortMethod? SortMethod { get; set; }

    [ObservableProperty] private bool _isICharacter = false;


    public CharacterDetailsViewModel(IGameService gameService, ILogger logger,
        INavigationService navigationService, ISkinManagerService skinManagerService,
        NotificationManager notificationService, ILocalSettingsService localSettingsService,
        ModDragAndDropService modDragAndDropService, ModCrawlerService modCrawlerService,
        ModNotificationManager modNotificationManager, ModSettingsService modSettingsService,
        IWindowManagerService windowManagerService, ImageHandlerService imageHandlerService,
        ElevatorService elevatorService)
    {
        _gameService = gameService;
        _logger = logger.ForContext<CharacterDetailsViewModel>();
        _navigationService = navigationService;
        _skinManagerService = skinManagerService;
        _notificationService = notificationService;
        _localSettingsService = localSettingsService;
        _modDragAndDropService = modDragAndDropService;
        _modCrawlerService = modCrawlerService;
        _modNotificationManager = modNotificationManager;
        _modSettingsService = modSettingsService;
        _windowManagerService = windowManagerService;
        _imageHandlerService = imageHandlerService;
        _elevatorService = elevatorService;

        _modDragAndDropService.DragAndDropFinished += async (sender, args) =>
        {
            foreach (var extractResult in args.ExtractResults)
            {
                var extractedFolderName = new DirectoryInfo(extractResult.ExtractedFolderPath).Name;
                await AddNewModAddedNotificationAsync(AttentionType.Added,
                    extractedFolderName, null);
            }
            await App.MainWindow.DispatcherQueue.EnqueueAsync(
                async () => { await RefreshMods(); }).ConfigureAwait(false);
        };

        ModdableObjectImage = _imageHandlerService.PlaceholderImageUri;

        MoveModsFlyoutVM = new MoveModsFlyoutVM(_gameService, _skinManagerService);
        MoveModsFlyoutVM.ModsMoved += async (sender, args) => await RefreshMods().ConfigureAwait(false);
        MoveModsFlyoutVM.ModsDeleted += async (sender, args) => await RefreshMods().ConfigureAwait(false);
        MoveModsFlyoutVM.ModCharactersSkinOverriden +=
            async (sender, args) => await RefreshMods().ConfigureAwait(false);

        ModListVM = new ModListVM(skinManagerService, modNotificationManager);
        ModListVM.OnModsSelected += OnModsSelected;

        ModPaneVM = new ModPaneVM();

        _modNotificationManager.OnModNotification += OnOnModNotificationHandler;
    }

    private void OnOnModNotificationHandler(object? sender, ModNotificationManager.ModNotificationEvent e)
    {
        App.MainWindow.DispatcherQueue.EnqueueAsync(() => RefreshModsCommand.ExecuteAsync(null));
    }

    private async void OnModsSelected(object? sender, ModListVM.ModSelectedEventArgs args)
    {
        var selectedMod = args.Mods.FirstOrDefault();
        var mod = _modList.Mods.FirstOrDefault(x => x.Id == selectedMod?.Id);
        if (mod is null || selectedMod is null)
        {
            ModPaneVM.UnloadMod();
            return;
        }

        var recentlyAddedModNotifications = args.Mods.SelectMany(x =>
            x.ModNotifications.Where(notification => notification.AttentionType == AttentionType.Added)).ToArray();

        if (recentlyAddedModNotifications.Any())
            foreach (var modNotification in recentlyAddedModNotifications)
            {
                await _modNotificationManager.RemoveModNotificationAsync(modNotification.Id);

                foreach (var newModModel in args.Mods)
                {
                    var notification = newModModel.ModNotifications.FirstOrDefault(x => x.Id == modNotification.Id);
                    if (notification is not null) newModModel.ModNotifications.Remove(notification);
                }
            }


        await ModPaneVM.LoadMod(selectedMod).ConfigureAwait(false);
    }

    private void ModListOnModsChanged(object? sender, ModFolderChangedArgs e)
    {
        App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            if (!IsAddingModFolder)
                _notificationService.ShowNotification(
                    $"Folder Activity Detected in {ShownCharacter.DisplayName}'s Mod Folder",
                    "Files/Folders were changed in the characters mod folder and mods have been refreshed.",
                    TimeSpan.FromSeconds(5));


            if (e.ChangeType == ModFolderChangeType.Renamed)
            {
                var inMemoryModNotification = new ModNotification()
                {
                    CharacterInternalName = ShownCharacter.InternalName,
                    ShowOnOverview = false,
                    ModFolderName = new DirectoryInfo(e.NewName).Name,
                    AttentionType = e.ChangeType switch
                    {
                        ModFolderChangeType.Created => AttentionType.Added,
                        ModFolderChangeType.Renamed => AttentionType.Added,
                        _ => AttentionType.None
                    },
                    Message = e.ChangeType switch
                    {
                        ModFolderChangeType.Created =>
                            $"Mod '{e.NewName}' was added to {ShownCharacter.DisplayName}'s mod folder.",
                        ModFolderChangeType.Renamed =>
                            $"Mod '{e.OldName}' was renamed to '{e.NewName}' in {ShownCharacter.DisplayName}'s mod folder.",
                        _ => string.Empty
                    }
                };

                await _modNotificationManager.AddModNotification(inMemoryModNotification);
            }

            await RefreshMods().ConfigureAwait(false);
        });
    }

    public async void OnNavigatedTo(object parameter)
    {
        var internalName = new InternalName("_");


        if (parameter is CharacterGridItemModel characterGridItemModel)
        {
            internalName = characterGridItemModel.Character.InternalName;
        }
        else if (parameter is INameable iInternalName)
        {
            internalName = iInternalName.InternalName;
        }
        else if (parameter is string internalNameString)
        {
            internalName = new InternalName(internalNameString);
        }


        var moddableObject = _gameService.GetModdableObjectByIdentifier(internalName);

        if (moddableObject is null)
        {
            ErrorNavigateBack();
            return;
        }

        if (moddableObject.ImageUri is not null)
            ModdableObjectImage = moddableObject.ImageUri;


        ShownCharacter = moddableObject;
        MoveModsFlyoutVM.SetShownCharacter(moddableObject);

        _modList = _skinManagerService.GetCharacterModList(moddableObject.InternalName);

        if (_gameService.IsMultiMod(moddableObject))
            ModListVM.DisableInfoBar = true;

        if (moddableObject is ICharacter character)
        {
            IsICharacter = true;
            var skins = character.Skins.ToArray();

            foreach (var characterInGameSkin in skins)
            {
                var skinImage = characterInGameSkin.ImageUri ?? _imageHandlerService.PlaceholderImageUri;
                SelectableInGameSkins.Add(
                    new SelectCharacterTemplate(characterInGameSkin.DisplayName, characterInGameSkin.InternalName,
                        skinImage.ToString())
                );
            }

            SelectedInGameSkin = skins.First(skinVm => skinVm.IsDefault);

            if (SelectedInGameSkin.ImageUri is not null)
                ModdableObjectImage = SelectedInGameSkin.ImageUri;

            MoveModsFlyoutVM.SetActiveSkin(SelectedInGameSkin);

            MultipleInGameSkins = character.Skins.Count > 1;
        }


        try
        {
            await RefreshMods();
            _modList.ModsChanged += ModListOnModsChanged;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error while refreshing mods.");
            _notificationService.ShowNotification("Error while loading modes.",
                $"An error occurred while loading the mods for this character.\n{e.Message}",
                TimeSpan.FromSeconds(10));
            ErrorNavigateBack();
        }

        if (IsICharacter)
        {
            var lastSelectedSkin = SelectableInGameSkins.FirstOrDefault(selectCharacterTemplate =>
                selectCharacterTemplate.InternalName.Equals(
                    _lastSelectedSkin.FirstOrDefault(kv => kv.Key == ShownCharacter).Value,
                    StringComparison.CurrentCultureIgnoreCase));

            if (lastSelectedSkin is not null) await SwitchCharacterSkin(lastSelectedSkin);
        }
    }

    // This function is called from the ModModel _toggleMod delegate.
    // This is a hacky way to get the toggle button to work.
    private void ToggleMod(ModModel thisMod)
    {
        var modList = _skinManagerService.GetCharacterModList(thisMod.Character);
        if (thisMod.IsEnabled)
            modList.DisableMod(thisMod.Id);
        else
            modList.EnableMod(thisMod.Id);

        thisMod.IsEnabled = !thisMod.IsEnabled;
        thisMod.FolderName = _modList.Mods.First(mod => mod.Id == thisMod.Id).Mod.Name;
        _elevatorService.RefreshGenshinMods();
    }

    [ObservableProperty] private bool _isAddingModFolder = false;

    [RelayCommand]
    private async Task AddModFolder()
    {
        var folderPicker = new FolderPicker();
        folderPicker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder is null)
        {
            _logger.Debug("User cancelled folder picker.");
            return;
        }

        try
        {
            IsAddingModFolder = true;
            await Task.Run(async () =>
            {
                await _modDragAndDropService.AddStorageItemFoldersAsync(_modList,
                    new ReadOnlyCollection<IStorageItem>(new List<IStorageItem> { folder }));
            });
        }
        finally
        {
            IsAddingModFolder = false;
        }
    }

    [RelayCommand]
    private async Task AddModArchiveAsync()
    {
        var filePicker = new FileOpenPicker();
        filePicker.FileTypeFilter.Add(".zip");
        filePicker.FileTypeFilter.Add(".rar");
        filePicker.FileTypeFilter.Add(".7z");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);
        var file = await filePicker.PickSingleFileAsync();
        if (file is null)
        {
            _logger.Debug("User cancelled file picker.");
            return;
        }

        try
        {
            IsAddingModFolder = true;
            await Task.Run(
                async () => await _modDragAndDropService.AddStorageItemFoldersAsync(_modList, new[] { file }));
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error while adding archive.");
            _notificationService.ShowNotification("Error while adding storage items.",
                $"An error occurred while adding the storage items.\n{e.Message}",
                TimeSpan.FromSeconds(5));
        }
        finally
        {
            IsAddingModFolder = false;
        }
    }

    [RelayCommand]
    private async Task RefreshMods()
    {
        var selectedModPaths = ModListVM.SelectedMods.Select(mod => mod.FolderName).ToArray();
        await _refreshMods();
        var selectedMods = ModListVM.Mods.Where(mod => selectedModPaths.Any(oldModPath =>
            ModFolderHelpers.FolderNameEquals(mod.FolderName, oldModPath))).ToArray();
        ModListVM.SelectionChanged(selectedMods, new List<ModModel>());
    }

    private async Task _refreshMods()
    {
        var refreshResult = await Task.Run(() => _skinManagerService.RefreshModsAsync(ShownCharacter.InternalName));
        var modList = new List<ModModel>();

        var mods = _gameService.IsMultiMod(_modList.Character) || !MultipleInGameSkins
            ? _modList.Mods
            : await FilterModsToSkin(_modList.Mods, SelectedInGameSkin);

        foreach (var skinEntry in mods)
        {
            var newModModel = ModModel.FromMod(skinEntry);
            newModModel.WithToggleModDelegate(ToggleMod);

            var modSettings = await LoadModSettings(skinEntry);

            if (modSettings != null)
                newModModel.WithModSettings(modSettings);

            var notifications = await _modNotificationManager.GetNotificationsAsync();

            var modNotifications = notifications.Where(x =>
                    x.ModId == skinEntry.Id)
                .ToArray();

            modNotifications.ForEach(newModModel.ModNotifications.Add);


            modList.Add(newModModel);
        }


        if (refreshResult.ModsDuplicate.Any())
        {
            var message = $"Duplicate mods were detected in {ShownCharacter.DisplayName}'s mod folder.\n";

            message = refreshResult.ModsDuplicate.Aggregate(message,
                (current, duplicateMod) =>
                    current +
                    $"Mod: '{duplicateMod.ExistingFolderName}' was renamed to '{duplicateMod.RenamedFolderName}' to avoid conflicts.\n");

            _notificationService.ShowNotification("Duplicate Mods Detected",
                message,
                TimeSpan.FromSeconds(10));
        }


        ModListVM.SetBackendMods(modList);
        ModListVM.ResetContent(SortMethod);
    }

    [RelayCommand]
    private async Task SwitchCharacterSkin(SelectCharacterTemplate? characterTemplate)
    {
        if (characterTemplate is null)
            return;

        if (ShownCharacter is not ICharacter character || SelectedInGameSkin is null)
        {
            return;
        }

        if (characterTemplate.InternalName.Equals(
                SelectedInGameSkin.InternalName,
                StringComparison.OrdinalIgnoreCase))
            return;

        var characterSkin = character.Skins.FirstOrDefault(skin =>
            skin.InternalName.Equals(characterTemplate.InternalName));


        if (characterSkin is null)
        {
            _logger.Error("Could not find character skin {SkinName} for character {CharacterName}",
                characterTemplate.DisplayName, ShownCharacter.DisplayName);
            _notificationService.ShowNotification("Error while switching character skin.", "", TimeSpan.FromSeconds(5));
            return;
        }

        SelectedInGameSkin = characterSkin;
        ModdableObjectImage = SelectedInGameSkin.ImageUri ?? _imageHandlerService.PlaceholderImageUri;

        MoveModsFlyoutVM.SetActiveSkin(characterSkin);


        foreach (var selectableInGameSkin in SelectableInGameSkins)
            selectableInGameSkin.IsSelected = selectableInGameSkin.InternalName.Equals(characterTemplate.InternalName,
                StringComparison.CurrentCultureIgnoreCase);

        _lastSelectedSkin[character] = SelectedInGameSkin.InternalName;
        await RefreshMods().ConfigureAwait(false);
    }

    private async Task<IReadOnlyCollection<CharacterSkinEntry>> FilterModsToSkin(IEnumerable<CharacterSkinEntry> mods,
        ICharacterSkin skin)
    {
        if (ShownCharacter is not ICharacter character)
            return mods.ToList();

        var filteredMods = new List<CharacterSkinEntry>();
        foreach (var mod in mods)
        {
            var modSkin = (await LoadModSettings(mod))?.CharacterSkinOverride;

            // Has skin override and is a match for the shown skin
            if (modSkin is not null && skin.InternalNameEquals(modSkin))
            {
                filteredMods.Add(mod);
                continue;
            }

            // Has override skin, but does not match any of the characters skins
            if (modSkin is not null && !character.Skins.Any(skinVm =>
                    skinVm.InternalNameEquals(modSkin)))
            {
                // In this case, the override skin is not a valid skin for this character, so we just add it.
                filteredMods.Add(mod);
                continue;
            }


            var detectedSkin =
                _modCrawlerService.GetFirstSubSkinRecursive(mod.Mod.FullPath, ShownCharacter.InternalName);

            // If we can detect the skin, and the mod has no override skin, check if the detected skin matches the shown skin.
            if (modSkin is null && detectedSkin is not null && detectedSkin.InternalNameEquals(skin.InternalName))
            {
                filteredMods.Add(mod);
                continue;
            }

            // If we can't detect the skin, and the mod has no override skin, we add it.
            if (detectedSkin is null && modSkin is null)
                filteredMods.Add(mod);
        }

        return filteredMods;
    }

    private async Task<ModSettings?> LoadModSettings(CharacterSkinEntry characterSkinEntry)
    {
        var result = await _modSettingsService.GetSettingsAsync(characterSkinEntry.Mod.Id);


        ModSettings? modSettings = null;

        modSettings = result.Match<ModSettings?>(
            modSettings => modSettings,
            notFound => null,
            modNotFound => null,
            error => null);


        return modSettings;
    }

    [RelayCommand]
    private async Task DragAndDropAsync(IReadOnlyList<IStorageItem>? storageItems)
    {
        if (storageItems is null)
        {
            _logger.Warning("Drag and drop files called with null storage items.");
            return;
        }

        try
        {
            IsAddingModFolder = true;
            await Task.Run(async () => await _modDragAndDropService.AddStorageItemFoldersAsync(_modList, storageItems));
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error while adding storage items.");
            _notificationService.ShowNotification("Drag And Drop operation failed",
                $"An error occurred while adding the storage items. Reason:\n{e.Message}",
                TimeSpan.FromSeconds(5));
        }
        finally
        {
            IsAddingModFolder = false;
        }
    }

    [RelayCommand]
    private async Task OpenModsFolderAsync()
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
    private async Task OpenGIMIRootFolderAsync()
    {
        var options = await _localSettingsService.ReadSettingAsync<ModManagerOptions>(ModManagerOptions.Section) ??
                      new ModManagerOptions();
        if (string.IsNullOrWhiteSpace(options.GimiRootFolderPath)) return;
        await Launcher.LaunchFolderAsync(
            await StorageFolder.GetFolderFromPathAsync(options.GimiRootFolderPath));
    }

    [RelayCommand]
    private async Task DisableAllMods()
    {
        if (ModListVM.Mods.Count == 0) return;
        var shownMods = ModListVM.Mods.Where(mod => mod.IsEnabled).ToArray();
        await Task.Run(() =>
        {
            foreach (var modEntry in shownMods)
                _modList.DisableMod(modEntry.Id);
        });
        await RefreshMods().ConfigureAwait(false);
    }

    public void ChangeModDetails(ModModel modModel)
    {
        var oldMod = _modList.Mods.FirstOrDefault(mod => mod.Id == modModel.Id);
        if (oldMod == null)
        {
            _logger.Warning("Could not find mod with id {ModId} to change details.", modModel.Id);
            return;
        }

        var oldModModel = ModModel.FromMod(oldMod);


        if (oldModModel.Name != modModel.Name)
            NotImplemented.Show("Setting custom mod names are not persisted between sessions",
                TimeSpan.FromSeconds(10));
    }

    public async Task ModList_KeyHandler(IEnumerable<Guid> modEntryId, VirtualKey key)
    {
        var selectedMods = ModListVM.Mods.Where(mod => modEntryId.Contains(mod.Id)).ToArray();

        if (key == VirtualKey.Space)
            foreach (var newModModel in selectedMods)
                await newModModel.ToggleModCommand.ExecuteAsync(newModModel);
    }


    private IEnumerable<ModModel> GetNewModModels()
    {
        return _modList.Mods.Select(mod => ModModel.FromMod(mod).WithToggleModDelegate(ToggleMod));
    }


    [RelayCommand]
    private async Task OpenNewModsWindowAsync(object? modNotification)
    {
        if (modNotification is not ModNotification notification)
        {
            _logger.Warning("OpenNewModsWindowAsync called with null modModel.");
            return;
        }

        var skinEntry = _modList.Mods.FirstOrDefault(mod => mod.Id == notification.ModId);

        if (skinEntry is null)
        {
            return;
        }

        var existingWindow = _windowManagerService.GetWindow(notification.Id);
        if (existingWindow is not null)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            {
                await Task.Delay(100);
                existingWindow.BringToFront();
            });
            return;
        }


        var modWindow = new ModUpdateAvailableWindow(notification.Id)
        {
            Title =
                $"New Mod Files Available: {ModFolderHelpers.GetFolderNameWithoutDisabledPrefix(skinEntry.Mod.Name)}"
        };
        _windowManagerService.CreateWindow(modWindow, identifier: notification.Id);
        await Task.Delay(100);
        modWindow.BringToFront();
    }


    [RelayCommand]
    private void GoBackToGrid()
    {
        var gridLastStack = _navigationService.GetBackStackItems().FirstOrDefault(backStackItem =>
            backStackItem.SourcePageType == typeof(CharactersPage));

        if (gridLastStack is not null)
        {
            _navigationService.GoBack();
            return;
        }

        _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!);
    }

    [RelayCommand]
    private void GoToCharacterEditScreen()
    {
        _navigationService.NavigateTo(typeof(CharacterManagerViewModel).FullName!, ShownCharacter.InternalName);
    }

    public void OnNavigatedFrom()
    {
        if (_modList is not null)
            _modList.ModsChanged -= ModListOnModsChanged;
    }


    private void ErrorNavigateBack()
    {
        Task.Run(async () =>
        {
            await Task.Delay(500);
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (_navigationService.CanGoBack)
                    _navigationService.GoBack();
                else
                    _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!);
            });
        });
    }


    public Task AddNewModAddedNotificationAsync(AttentionType attentionType, string newModFolderName, string? message)
    {
        var inMemoryModNotification = new ModNotification()
        {
            CharacterInternalName = ShownCharacter.InternalName,
            ShowOnOverview = false,
            ModFolderName = newModFolderName,
            AttentionType = attentionType,
            Message = message ?? string.Empty
        };

        return _modNotificationManager.AddModNotification(inMemoryModNotification);
    }
}