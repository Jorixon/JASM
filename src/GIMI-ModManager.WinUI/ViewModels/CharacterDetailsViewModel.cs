using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Windows.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using Serilog;
using GIMI_ModManager.WinUI.Services;
using Windows.System;
using Windows.Storage.Pickers;
using CommunityToolkit.WinUI;
using GIMI_ModManager.Core.Contracts.Entities;

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

    private ICharacterModList _modList = null!;
    public ModListVM ModListVM { get; } = null!;
    public ModPaneVM ModPaneVM { get; } = null!;

    public MoveModsFlyoutVM MoveModsFlyoutVM { get; private set; }

    [ObservableProperty] private GenshinCharacter _shownCharacter = null!;

    public CharacterDetailsViewModel(IGenshinService genshinService, ILogger logger,
        INavigationService navigationService, ISkinManagerService skinManagerService,
        NotificationManager notificationService, ILocalSettingsService localSettingsService,
        ModDragAndDropService modDragAndDropService)
    {
        _genshinService = genshinService;
        _logger = logger.ForContext<CharacterDetailsViewModel>();
        _navigationService = navigationService;
        _skinManagerService = skinManagerService;
        _notificationService = notificationService;
        _localSettingsService = localSettingsService;
        _modDragAndDropService = modDragAndDropService;
        MoveModsFlyoutVM = new(_genshinService, _skinManagerService);
        MoveModsFlyoutVM.ModsMoved += async (sender, args) => await _refreshMods();
        MoveModsFlyoutVM.ModsDeleted += async (sender, args) => await _refreshMods();

        ModListVM = new(skinManagerService);
        ModListVM.OnModsSelected += async (sender, args) =>
        {
            var selectedMod = args.Mods.FirstOrDefault();
            var mod = _modList.Mods.FirstOrDefault(x => x.Id == selectedMod?.Id);
            if (mod is null || selectedMod is null)
            {
                ModPaneVM.UnloadMod();
                return;
            }

            await ModPaneVM.LoadMod(selectedMod);
        };

        ModPaneVM = new(skinManagerService, notificationService);
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
            await Task.Delay(TimeSpan.FromSeconds(1)); // Wait for file system to finish moving files
            await RefreshMods().ConfigureAwait(false);
        });
    }

    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is not CharacterGridItemModel characterGridItemModel)
        {
            if (_navigationService.CanGoBack)
                _navigationService.GoBack();
            else
                _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!);
            return;
        }

        var character = _genshinService.GetCharacter(characterGridItemModel.Character.Id);
        if (character is null)
        {
            if (_navigationService.CanGoBack)
                _navigationService.GoBack();
            else
                _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!);
            return;
        }

        ShownCharacter = character;
        MoveModsFlyoutVM.SetShownCharacter(ShownCharacter);
        _modList = _skinManagerService.GetCharacterModList(character);
        if (_genshinService.IsMultiModCharacter(ShownCharacter))
            ModListVM.DisableInfoBar = true;

        try
        {
            await _refreshMods();
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
                await _modDragAndDropService.AddStorageItemFoldersAsync(_modList,
                    new ReadOnlyCollection<IStorageItem>(new List<IStorageItem> { folder })));
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
        await Task.Run(() => _skinManagerService.RefreshModsAsync(ShownCharacter));
        var modList = new List<NewModModel>();
        foreach (var skinEntry in _modList.Mods)
        {
            var newModModel = NewModModel.FromMod(skinEntry);
            newModModel.WithToggleModDelegate(ToggleMod);
            try
            {
                var modSettings = await skinEntry.Mod.ReadSkinModSettings();

                newModModel.WithModSettings(modSettings);
            }
            catch (JsonException e)
            {
                _logger.Error(e, "Error while reading mod settings for {ModName}", skinEntry.Mod.Name);
                _notificationService.ShowNotification("Error while reading mod settings.",
                    $"An error occurred while reading the mod settings for {skinEntry.Mod.Name}, See logs for details.\n{e.Message}",
                    TimeSpan.FromSeconds(10));
            }

            modList.Add(newModModel);
        }

        ModListVM.SetBackendMods(modList);
        ModListVM.ResetContent();
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
        => await Launcher.LaunchFolderAsync(
            await StorageFolder.GetFolderFromPathAsync(_modList.AbsModsFolderPath));

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
        {
            NotImplemented.Show("Setting custom mod names are not persisted between sessions",
                TimeSpan.FromSeconds(10));
        }
    }

    public void ModList_KeyHandler(IEnumerable<Guid> modEntryId, VirtualKey key)
    {
        var selectedMods = ModListVM.Mods.Where(mod => modEntryId.Contains(mod.Id)).ToArray();

        if (key == VirtualKey.Space)
        {
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
    }


    private IEnumerable<NewModModel> GetNewModModels()
        => _modList.Mods.Select(mod => NewModModel.FromMod(mod).WithToggleModDelegate(ToggleMod));


    [RelayCommand]
    private void GoBack()
        => _navigationService.GoBack();

    public void OnNavigatedFrom()
        => _modList.ModsChanged -= ModListOnModsChanged;
}