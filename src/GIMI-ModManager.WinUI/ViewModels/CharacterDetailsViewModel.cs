using System.Collections.ObjectModel;
using System.Text.Json;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Genshin;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Models.CustomControlTemplates;
using GIMI_ModManager.WinUI.Models.Options;
using GIMI_ModManager.WinUI.Models.ViewModels;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class CharacterDetailsViewModel : ObservableRecipient, INavigationAware
{
    private readonly IGenshinService _genshinService;
    private readonly ILogger _logger;
    private readonly INavigationService _navigationService;
    private readonly ISkinManagerService _skinManagerService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly NotificationManager _notificationService;
    private readonly ModDragAndDropService _modDragAndDropService;
    private readonly ModCrawlerService _modCrawlerService;
    private readonly ModNotificationManager _modNotificationManager;

    private ICharacterModList _modList = null!;
    public ModListVM ModListVM { get; } = null!;
    public ModPaneVM ModPaneVM { get; } = null!;

    public MoveModsFlyoutVM MoveModsFlyoutVM { get; private set; }

    [ObservableProperty] private GenshinCharacter _shownCharacter = null!;

    public ObservableCollection<SelectCharacterTemplate> SelectableInGameSkins = new();

    [ObservableProperty] public bool _multipleInGameSkins = false;

    [ObservableProperty] private SkinVM _selectedInGameSkin = new();

    private static Dictionary<GenshinCharacter, string> _lastSelectedSkin = new();


    public CharacterDetailsViewModel(IGenshinService genshinService, ILogger logger,
        INavigationService navigationService, ISkinManagerService skinManagerService,
        NotificationManager notificationService, ILocalSettingsService localSettingsService,
        ModDragAndDropService modDragAndDropService, ModCrawlerService modCrawlerService,
        ModNotificationManager modNotificationManager)
    {
        _genshinService = genshinService;
        _logger = logger.ForContext<CharacterDetailsViewModel>();
        _navigationService = navigationService;
        _skinManagerService = skinManagerService;
        _notificationService = notificationService;
        _localSettingsService = localSettingsService;
        _modDragAndDropService = modDragAndDropService;
        _modCrawlerService = modCrawlerService;
        _modNotificationManager = modNotificationManager;

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

        MoveModsFlyoutVM = new MoveModsFlyoutVM(_genshinService, _skinManagerService);
        MoveModsFlyoutVM.ModsMoved += async (sender, args) => await RefreshMods().ConfigureAwait(false);
        MoveModsFlyoutVM.ModsDeleted += async (sender, args) => await RefreshMods().ConfigureAwait(false);
        MoveModsFlyoutVM.ModCharactersSkinOverriden +=
            async (sender, args) => await RefreshMods().ConfigureAwait(false);

        ModListVM = new ModListVM(skinManagerService, modNotificationManager);
        ModListVM.OnModsSelected += OnModsSelected;

        ModPaneVM = new ModPaneVM(skinManagerService, notificationService);
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
        {
            foreach (var modNotification in recentlyAddedModNotifications)
            {
                await _modNotificationManager.RemoveModNotification(modNotification.Id);

                foreach (var newModModel in args.Mods)
                {
                    var notification = newModModel.ModNotifications.FirstOrDefault(x => x.Id == modNotification.Id);
                    if (notification is not null) newModModel.ModNotifications.Remove(notification);
                }
            }
        }


        await ModPaneVM.LoadMod(selectedMod).ConfigureAwait(false);
    }

    private void ModListOnModsChanged(object? sender, ModFolderChangedArgs e)
    {
        App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            if (!IsAddingModFolder)
            {
                _notificationService.ShowNotification(
                    $"Folder Activity Detected in {ShownCharacter.DisplayName}'s Mod Folder",
                    "Files/Folders were changed in the characters mod folder and mods have been refreshed.",
                    TimeSpan.FromSeconds(5));
            }


            if (e.ChangeType == ModFolderChangeType.Renamed)
            {
                var inMemoryModNotification = new ModNotification()
                {
                    CharacterId = ShownCharacter.Id,
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
                    },
                };

                await _modNotificationManager.AddModNotification(inMemoryModNotification);
            }

            await RefreshMods().ConfigureAwait(false);
        });
    }

    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is not CharacterGridItemModel characterGridItemModel)
        {
            ErrorNavigateBack();
            return;
        }

        var character = _genshinService.GetCharacter(characterGridItemModel.Character.Id);
        if (character is null)
        {
            ErrorNavigateBack();
            return;
        }

        ShownCharacter = character;
        MoveModsFlyoutVM.SetShownCharacter(ShownCharacter);
        _modList = _skinManagerService.GetCharacterModList(character);
        if (_genshinService.IsMultiModCharacter(ShownCharacter))
            ModListVM.DisableInfoBar = true;

        foreach (var characterInGameSkin in character.InGameSkins.Select(SkinVM.FromSkin))
            SelectableInGameSkins.Add(
                new SelectCharacterTemplate()
                {
                    DisplayName = characterInGameSkin.DisplayName,
                    ImagePath = characterInGameSkin.ImageUri,
                    IsSelected = characterInGameSkin.DefaultSkin
                }
            );

        var skin = character.InGameSkins.FirstOrDefault(skin => skin.DefaultSkin);

        switch (skin)
        {
            case null when _genshinService.IsMultiModCharacter(character):
                skin = new Skin(true, "", "", "") { Character = ShownCharacter };
                break;
            case null:
                _logger.Error("No default skin found for character {CharacterName}", character.DisplayName);
                _notificationService.ShowNotification("Error while loading character.",
                    $"An error occurred while loading the character {character.DisplayName}.\nNo default skin found for character.",
                    TimeSpan.FromSeconds(10));

                ErrorNavigateBack();

                return;
        }

        SelectedInGameSkin = SkinVM.FromSkin(skin);
        MoveModsFlyoutVM.SetActiveSkin(SelectedInGameSkin);


        MultipleInGameSkins = character.InGameSkins.Count > 1;


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
            _navigationService.GoBack();
        }

        var lastSelectedSkin = SelectableInGameSkins.FirstOrDefault(selectCharacterTemplate =>
            selectCharacterTemplate.DisplayName.Equals(
                _lastSelectedSkin.FirstOrDefault(kv => kv.Key == ShownCharacter).Value,
                StringComparison.CurrentCultureIgnoreCase));

        if (lastSelectedSkin is not null)
        {
            await SwitchCharacterSkin(lastSelectedSkin);
        }
    }


    // Does not run on UI thread
    private void ToggleMod(NewModModel mod)
    {
        var modList = _skinManagerService.GetCharacterModList(mod.Character);
        if (mod.IsEnabled)
            modList.DisableMod(mod.Id);

        else

            modList.EnableMod(mod.Id);
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
            oldModPath.Equals(mod.FolderName, StringComparison.CurrentCultureIgnoreCase))).ToArray();
        ModListVM.SelectionChanged(selectedMods, new List<NewModModel>());
    }

    private async Task _refreshMods()
    {
        var refreshResult = await Task.Run(() => _skinManagerService.RefreshModsAsync(ShownCharacter));
        var modList = new List<NewModModel>();

        var mods = _genshinService.IsMultiModCharacter(ShownCharacter) || !MultipleInGameSkins
            ? _modList.Mods
            : await FilterModsToSkin(_modList.Mods, SelectedInGameSkin);

        foreach (var skinEntry in mods)
        {
            var newModModel = NewModModel.FromMod(skinEntry);
            newModModel.WithToggleModDelegate(ToggleMod);

            var modSettings = await LoadModSettings(skinEntry);

            if (modSettings != null)
                newModModel.WithModSettings(modSettings);

            ModNotification? inMemoryModNotification =
                _modNotificationManager.InMemoryModNotifications.FirstOrDefault(x =>
                    x.ModFolderName.Equals(skinEntry.Mod.Name, StringComparison.CurrentCultureIgnoreCase) &&
                    x.CharacterId == ShownCharacter.Id);

            if (inMemoryModNotification != null)
                newModModel.ModNotifications.Add(inMemoryModNotification);

            //newModModel.ModNotifications.Add(new ModNotification()
            //{
            //    CharacterId = ShownCharacter.Id,
            //    ShowOnOverview = false,
            //    ModFolderName = Path.GetFileNameWithoutExtension(skinEntry.Mod.Name) ?? string.Empty,
            //    AttentionType = AttentionType.Added,
            //    Message = $"Mod '{skinEntry.Mod.Name}' was added to {ShownCharacter.DisplayName}'s mod folder.",
            //});


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
        ModListVM.ResetContent();
    }

    [RelayCommand]
    private async Task SwitchCharacterSkin(SelectCharacterTemplate characterTemplate)
    {
        if (characterTemplate?.DisplayName is not null && characterTemplate.DisplayName.Equals(
                SelectedInGameSkin.DisplayName,
                StringComparison.CurrentCultureIgnoreCase))
            return;

        var characterSkin = ShownCharacter.InGameSkins.FirstOrDefault(skin =>
            skin.DisplayName.Equals(characterTemplate.DisplayName, StringComparison.CurrentCultureIgnoreCase));
        if (characterSkin is null)
        {
            _logger.Error("Could not find character skin {SkinName} for character {CharacterName}",
                characterTemplate.DisplayName, ShownCharacter.DisplayName);
            _notificationService.ShowNotification("Error while switching character skin.", "", TimeSpan.FromSeconds(5));
            return;
        }

        SelectedInGameSkin = SkinVM.FromSkin(characterSkin);
        MoveModsFlyoutVM.SetActiveSkin(SelectedInGameSkin);
        foreach (var selectableInGameSkin in SelectableInGameSkins)
            selectableInGameSkin.IsSelected = selectableInGameSkin.DisplayName.Equals(characterTemplate.DisplayName,
                StringComparison.CurrentCultureIgnoreCase);
        _lastSelectedSkin[ShownCharacter] = SelectedInGameSkin.DisplayName;
        await RefreshMods().ConfigureAwait(false);
    }

    private async Task<IReadOnlyCollection<CharacterSkinEntry>> FilterModsToSkin(IEnumerable<CharacterSkinEntry> mods,
        SkinVM skin)
    {
        var filteredMods = new List<CharacterSkinEntry>();
        foreach (var mod in mods)
        {
            var modSkin = (await LoadModSettings(mod))?.CharacterSkinOverride;

            if (modSkin != null && modSkin.Equals(skin.Name, StringComparison.CurrentCultureIgnoreCase))
            {
                filteredMods.Add(mod);
                continue;
            }

            var detectedSkin = _modCrawlerService.GetFirstSubSkinRecursive(mod.Mod.FullPath, ShownCharacter);
            if (detectedSkin is null && modSkin is null)
            {
                // In this case, we don't know what skin the mod is for, so we just add it.
                filteredMods.Add(mod);
                continue;
            }

            if (modSkin == null && detectedSkin.Name.Equals(skin.Name, StringComparison.CurrentCultureIgnoreCase))
                filteredMods.Add(mod);
        }

        return filteredMods;
    }

    private async Task<SkinModSettings?> LoadModSettings(CharacterSkinEntry characterSkinEntry)
    {
        try
        {
            var modSettings = await characterSkinEntry.Mod.ReadSkinModSettings();
            return modSettings;
        }
        catch (JsonException e)
        {
            _logger.Error(e, "Error while reading mod settings for {ModName}", characterSkinEntry.Mod.Name);
            _notificationService.ShowNotification("Error while reading mod settings.",
                $"An error occurred while reading the mod settings for {characterSkinEntry.Mod.Name}, See logs for details.\n{e.Message}",
                TimeSpan.FromSeconds(10));
        }

        return null;
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
                $"An error occurred while adding the storage items. Mod may have been partially copied. Reason:\n{e.Message}",
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
        await Launcher.LaunchFolderAsync(
            await StorageFolder.GetFolderFromPathAsync(_modList.AbsModsFolderPath));
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
        await Task.Run(() =>
        {
            foreach (var modEntry in _modList.Mods.Where(mod => mod.IsEnabled))
                _modList.DisableMod(modEntry.Id);
        });
        ModListVM.SetBackendMods(_modList.Mods.Select(mod =>
            NewModModel.FromMod(mod).WithToggleModDelegate(ToggleMod)));
        ModListVM.ResetContent();
    }

    public void ChangeModDetails(NewModModel newModModel)
    {
        var oldMod = _modList.Mods.FirstOrDefault(mod => mod.Id == newModModel.Id);
        if (oldMod == null)
        {
            _logger.Warning("Could not find mod with id {ModId} to change details.", newModModel.Id);
            return;
        }

        var oldModModel = NewModModel.FromMod(oldMod);


        if (oldModModel.Name != newModModel.Name)
            NotImplemented.Show("Setting custom mod names are not persisted between sessions",
                TimeSpan.FromSeconds(10));
    }

    public void ModList_KeyHandler(IEnumerable<Guid> modEntryId, VirtualKey key)
    {
        var selectedMods = ModListVM.Mods.Where(mod => modEntryId.Contains(mod.Id)).ToArray();

        if (key == VirtualKey.Space)
            foreach (var newModModel in selectedMods)
            {
                if (newModModel.IsEnabled)
                    _modList.DisableMod(newModModel.Id);
                else
                    _modList.EnableMod(newModModel.Id);

                newModModel.IsEnabled = !newModModel.IsEnabled;
                newModModel.FolderName = _modList.Mods.First(mod => mod.Id == newModModel.Id).Mod.Name;
            }
    }


    private IEnumerable<NewModModel> GetNewModModels()
    {
        return _modList.Mods.Select(mod => NewModModel.FromMod(mod).WithToggleModDelegate(ToggleMod));
    }


    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
    }

    public void OnNavigatedFrom()
    {
        _modList.ModsChanged -= ModListOnModsChanged;
    }


    private void ErrorNavigateBack()
    {
        SelectedInGameSkin = new SkinVM();

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
            CharacterId = ShownCharacter.Id,
            ShowOnOverview = false,
            ModFolderName = newModFolderName,
            AttentionType = attentionType,
            Message = message ?? string.Empty,
        };

        return _modNotificationManager.AddModNotification(inMemoryModNotification);
    }
}