using System.Collections.ObjectModel;
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

    private ICharacterModList _modList = null!;
    public ModListVM ModListVM { get; } = null!;
    public ModPaneVM ModPaneVM { get; } = null!;

    public MoveModsFlyoutVM MoveModsFlyoutVM { get; private set; }

    [ObservableProperty] private GenshinCharacter _shownCharacter = null!;

    public CharacterDetailsViewModel(IGenshinService genshinService, ILogger logger,
        INavigationService navigationService, ISkinManagerService skinManagerService,
        NotificationManager notificationService, ILocalSettingsService localSettingsService)
    {
        _genshinService = genshinService;
        _logger = logger.ForContext<CharacterDetailsViewModel>();
        _navigationService = navigationService;
        _skinManagerService = skinManagerService;
        _notificationService = notificationService;
        _localSettingsService = localSettingsService;
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
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (!IsAddingModFolder)
                _notificationService.ShowNotification(
                    $"Folder Activity Detected in {ShownCharacter.DisplayName}'s Mod Folder",
                    "Files/Folders were changed in the characters mod folder and mods have been refreshed.",
                    TimeSpan.FromSeconds(5));
            ModListVM.SetBackendMods(_modList.Mods.Select(mod =>
                NewModModel.FromMod(mod).WithToggleModDelegate(ToggleMod)));
            ModListVM.ResetContent();
        });
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not CharacterGridItemModel characterGridItemModel)
        {
            _navigationService.GoBack();
            return;
        }

        var character = _genshinService.GetCharacter(characterGridItemModel.Character.Id);
        if (character is null)
        {
            _navigationService.GoBack();
            return;
        }

        ShownCharacter = character;
        MoveModsFlyoutVM.SetShownCharacter(ShownCharacter);
        _modList = _skinManagerService.GetCharacterModList(character);
        if (ShownCharacter.Id == _genshinService.OtherCharacterId)
            ModListVM.DisableInfoBar = true;


        ModListVM.SetBackendMods(_modList.Mods.Select(mod =>
            NewModModel.FromMod(mod).WithToggleModDelegate(ToggleMod)));
        _modList.ModsChanged += ModListOnModsChanged;
        ModListVM.ResetContent();
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
                await AddStorageItemFoldersAsync(
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
            await Task.Run(async () => await AddStorageItemFoldersAsync(new[] { file }));
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
        await Task.Run(() => _skinManagerService.RefreshMods(ShownCharacter));
        ModListVM.SetBackendMods(_modList.Mods.Select(mod =>
            NewModModel.FromMod(mod).WithToggleModDelegate(ToggleMod)));
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
            await Task.Run(async () => await AddStorageItemFoldersAsync(storageItems));
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

    // Drag and drop directly from 7zip is REALLY STRANGE, I don't know why 7zip 'usually' deletes the files before we can copy them
    // Sometimes only a few folders are copied, sometimes only a single file is copied, but usually 7zip removes them and the app just crashes
    // This code is a mess, but it works.
    private async Task AddStorageItemFoldersAsync(IReadOnlyList<IStorageItem>? storageItems)
    {
        if (storageItems is null || !storageItems.Any())
        {
            _logger.Warning("Drag and drop files called with null/0 storage items.");
            return;
        }

        var showFileWarning = false;

        if (storageItems.Count > 1)
        {
            _notificationService.ShowNotification(
                "Drag and drop called with more than one storage item, this is currently not supported", "",
                TimeSpan.FromSeconds(5));
            return;
        }

        // Warning mess below
        foreach (var storageItem in storageItems)
        {
            var destDirectoryInfo = new DirectoryInfo(_modList.AbsModsFolderPath);
            destDirectoryInfo.Create();


            if (storageItem is StorageFile)
            {
                using var scanner = new DragAndDropScanner();
                var extractResult = scanner.Scan(storageItem.Path);
                extractResult.ExtractedMod.MoveTo(destDirectoryInfo.FullName);
                if (extractResult.IgnoredMods.Any())
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        _notificationService.ShowNotification(
                            "Multiple folders detected during extraction, first one was extracted",
                            $"Ignored Folders: {string.Join(" | ", extractResult.IgnoredMods)}",
                            TimeSpan.FromSeconds(7)));
                continue;
            }

            if (storageItem is not StorageFolder sourceFolder)
            {
                _logger.Information("Unknown storage item type from drop: {StorageItemType}", storageItem.GetType());
                continue;
            }


            _logger.Debug("Source destination folder for drag and drop: {Source}", sourceFolder.Path);
            _logger.Debug("Copying folder {FolderName} to {DestinationFolder}", sourceFolder.Path,
                destDirectoryInfo.FullName);


            var sourceFolderPath = sourceFolder.Path;


            if (sourceFolderPath is null)
            {
                _logger.Warning("Source folder path is null, skipping.");
                continue;
            }

            var tmpFolder = Path.GetTempPath();

            Action<StorageFolder, StorageFolder> recursiveCopy = null!;

            if (sourceFolderPath.Contains(tmpFolder)) // Is 7zip
                recursiveCopy = RecursiveCopy7z;
            else // StorageFolder from explorer
            {
                destDirectoryInfo = new DirectoryInfo(Path.Combine(_modList.AbsModsFolderPath, sourceFolder.Name));
                destDirectoryInfo.Create();
                recursiveCopy = RecursiveCopy;
            }


            IsAddingModFolder = true;
            recursiveCopy.Invoke(sourceFolder,
                await StorageFolder.GetFolderFromPathAsync(destDirectoryInfo.FullName));
        }

        if (showFileWarning)
        {
            _notificationService.ShowNotification("Only folders are supported for drag and drop.",
                "Any files not in the dropped in folders are ignored.", TimeSpan.FromSeconds(10));
        }
    }

    // ReSharper disable once InconsistentNaming
    private static void RecursiveCopy7z(StorageFolder sourceFolder, StorageFolder destinationFolder)
    {
        var tmpFolder = Path.GetTempPath();
        var parentDir = new DirectoryInfo(Path.GetDirectoryName(sourceFolder.Path)!);
        parentDir.MoveTo(Path.Combine(tmpFolder, "JASM_TMP", Guid.NewGuid().ToString("N")));
        var mod = new Mod(parentDir.GetDirectories().First()!);
        mod.MoveTo(destinationFolder.Path);
    }

    private void RecursiveCopy(StorageFolder sourceFolder, StorageFolder destinationFolder)
    {
        if (sourceFolder == null || destinationFolder == null)
        {
            throw new ArgumentNullException("Source and destination folders cannot be null.");
        }

        var sourceDir = new DirectoryInfo(sourceFolder.Path);

        // Copy files
        foreach (var file in sourceDir.GetFiles())
        {
            _logger.Debug("Copying file {FileName} to {DestinationFolder}", file.FullName, destinationFolder.Path);
            if (!File.Exists(file.FullName))
            {
                _logger.Warning("File {FileName} does not exist.", file.FullName);
                continue;
            }

            file.CopyTo(Path.Combine(destinationFolder.Path, file.Name), true);
        }
        // Recursively copy subfolders

        foreach (var subFolder in sourceDir.GetDirectories())
        {
            _logger.Debug("Copying subfolder {SubFolderName} to {DestinationFolder}", subFolder.FullName,
                destinationFolder.Path);
            if (!Directory.Exists(subFolder.FullName))
            {
                _logger.Warning("Subfolder {SubFolderName} does not exist.", subFolder.FullName);
                continue;
            }

            var newSubFolder = new DirectoryInfo(Path.Combine(destinationFolder.Path, subFolder.Name));
            newSubFolder.Create();
            RecursiveCopy(StorageFolder.GetFolderFromPathAsync(subFolder.FullName).GetAwaiter().GetResult(),
                StorageFolder.GetFolderFromPathAsync(newSubFolder.FullName).GetAwaiter().GetResult());
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