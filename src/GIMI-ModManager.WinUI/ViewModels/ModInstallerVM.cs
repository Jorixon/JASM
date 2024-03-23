using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkitWrapper;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities.Mods.Helpers;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Models.Settings;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Dispatching;
using Serilog;
using Constants = GIMI_ModManager.Core.Helpers.Constants;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class ModInstallerVM : ObservableRecipient, INavigationAware, IDisposable
{
    private readonly ILogger _logger;
    private readonly ImageHandlerService _imageHandlerService;
    private readonly NotificationManager _notificationManager;
    private readonly ModNotificationManager _modNotificationManager;
    private readonly IWindowManagerService _windowManagerService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly CharacterSkinService _characterSkinService;
    private readonly ModSettingsService _modSettingsService;
    private readonly Uri _placeholderImageUri;
    private ICharacterModList _characterModList = null!;
    private ICharacterSkin? _inGameSkin = null;
    private DispatcherQueue? _dispatcherQueue;


    private IAsyncRelayCommand? _addModDialogCommand;

    public IAsyncRelayCommand AddModDialogCommand
    {
        get => _addModDialogCommand ??= new AsyncRelayCommand(AddModAsync, canExecute: canAddMod);
        set => SetProperty(ref _addModDialogCommand, value);
    }

    public event EventHandler? DuplicateModDialog;
    public event EventHandler? InstallerFinished;
    public event EventHandler? CloseRequested;

    [ObservableProperty] private string _modCharacterName = string.Empty;
    [ObservableProperty] private bool _isRetrievingModInfo;
    [ObservableProperty] private FileSystemItem? _lastSelectedShaderFixesFolder;


    private ModInstallation? _modInstallation;

    [ObservableProperty] private string _modFolderName = string.Empty;

    private ISkinMod? _duplicateMod;

    [ObservableProperty] private Uri? _duplicateModPath;

    [ObservableProperty] private string _duplicateModFolderName = string.Empty;
    [ObservableProperty] private string _duplicateModCustomName = string.Empty;

    [ObservableProperty] private Uri _modPreviewImagePath = App.GetService<ImageHandlerService>().PlaceholderImageUri;
    [ObservableProperty] private string _customName = string.Empty;
    [ObservableProperty] private string _modUrl = string.Empty;
    [ObservableProperty] private string _author = string.Empty;
    [ObservableProperty] private string _description = string.Empty;

    [ObservableProperty] private ObservableCollection<RootFolder> _rootFolder = new();

    private const string AddRenameText = "Rename and Add mod";
    private const string AddReplaceText = "Overwrite old mod";
    [ObservableProperty] private string _primaryButtonText = AddRenameText;

    [ObservableProperty] private bool _overwriteExistingMod;

    [ObservableProperty] private bool _canExecuteDialogCommand;

    [ObservableProperty] private bool _enableThisMod;
    [ObservableProperty] private bool _alwaysOnTop;

    public readonly string RootFolderIcon = "\uF89A";
    public readonly string ShaderFixesFolderIcon = "\uE710";
    public readonly string SelectedImageIcon = "\uE8B9";
    public readonly string SelectedMergeIniIcon = "\uE8A5";

    [ObservableProperty] private FileSystemItem? _lastSelectedRootFolder;

    [ObservableProperty] private FileSystemItem? _lastSelectedImageFile;

    public ModInstallerVM(ILogger logger, ImageHandlerService imageHandlerService,
        NotificationManager notificationManager, IWindowManagerService windowManagerService,
        ModNotificationManager modNotificationManager, ILocalSettingsService localSettingsService,
        CharacterSkinService characterSkinService, ModSettingsService modSettingsService)
    {
        _logger = logger;
        _imageHandlerService = imageHandlerService;
        _placeholderImageUri = imageHandlerService.PlaceholderImageUri;
        _notificationManager = notificationManager;
        _windowManagerService = windowManagerService;
        _modNotificationManager = modNotificationManager;
        _localSettingsService = localSettingsService;
        _characterSkinService = characterSkinService;
        _modSettingsService = modSettingsService;
        PropertyChanged += OnPropertyChanged;
    }

    public async Task InitializeAsync(ICharacterModList characterModList, DirectoryInfo modToInstall,
        DispatcherQueue dispatcherQueue, ICharacterSkin? inGameSkin = null)
    {
        _characterModList = characterModList;
        ModCharacterName = characterModList.Character.DisplayName;
        _modInstallation = ModInstallation.Start(modToInstall, _characterModList);
        _dispatcherQueue = dispatcherQueue;

        RootFolder.Clear();
        RootFolder.Add(new RootFolder(modToInstall));

        var installerSettings = await _localSettingsService
            .ReadOrCreateSettingAsync<ModInstallerSettings>(ModInstallerSettings.Key);


        EnableThisMod = installerSettings.EnableModOnInstall;
        AlwaysOnTop = installerSettings.ModInstallerWindowOnTop;

        await Task.Run(() =>
        {
            var modDir = _modInstallation.AutoSetModRootFolder();
            if (modDir is not null)
            {
                var fileSystemItem = RootFolder.First().GetByPath(modDir.FullName);
                if (fileSystemItem is not null)
                    dispatcherQueue.EnqueueAsync(async () =>
                    {
                        await SetRootFolderAsync(fileSystemItem);
                        if (LastSelectedRootFolder is not null)
                        {
                            LastSelectedRootFolder.IsExpanded = true;
                            LastSelectedRootFolder.IsSelected = true;
                        }
                    });
            }

            var shaderFixesDir = _modInstallation.AutoSetShaderFixesFolder();
            if (shaderFixesDir is not null)
            {
                var fileSystemItem = RootFolder.First().GetByPath(shaderFixesDir.FullName);
                if (fileSystemItem is not null)
                    dispatcherQueue.TryEnqueue(() => { SetShaderFixesFolder(fileSystemItem); });
            }

            var autoFoundImages = SkinModHelpers.DetectModPreviewImages(_modInstallation.ModFolder.FullName);

            if (autoFoundImages.Any())
                dispatcherQueue.TryEnqueue(() =>
                {
                    ModPreviewImagePath = autoFoundImages.First();
                    var fileSystemItem = RootFolder.FirstOrDefault()
                        ?.GetByPath(autoFoundImages.FirstOrDefault()?.LocalPath ?? "");
                    SetModPreviewImage(fileSystemItem);
                });
        }).ConfigureAwait(false);
    }

    private async void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModUrl))
        {
            await GetModInfo(ModUrl);
        }
        else if (e.PropertyName == nameof(OverwriteExistingMod))
        {
            OnOverwriteExistingModChanged();
        }
        else if (e.PropertyName is nameof(ModFolderName) or
                 nameof(DuplicateModFolderName) or
                 nameof(CustomName) or
                 nameof(DuplicateModCustomName))
        {
            if (!OverwriteExistingMod)
                CanExecuteDialogCommand = canAddModAndRename();
            else
                CanExecuteDialogCommand = true;
        }
    }

    public void OnNavigatedTo(object parameter)
    {
    }

    public void OnNavigatedFrom()
    {
        _modInstallation?.Dispose();
    }


    private void SuccessfulInstall(ISkinMod newMod)
    {
        var modName = newMod.GetDisplayName();

        InstallerFinished?.Invoke(this, EventArgs.Empty);
        _logger.Debug("Mod {newModPath} was added to {modListPath}", newMod.FullPath,
            _characterModList.AbsModsFolderPath);
        _notificationManager.ShowNotification($"Mod '{modName}' installed",
            $"Mod '{modName}' ({newMod.Name}), was successfully added to {_characterModList.Character.DisplayName} ModList",
            TimeSpan.FromSeconds(5));

        if (EnableThisMod)
            Task.Run(() => EnableOnlyMod(newMod));

        _dispatcherQueue?.TryEnqueue(() => { _windowManagerService.GetWindow(_characterModList)?.Close(); });

        App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            _modNotificationManager.AddModNotification(new ModNotification()
            {
                ModId = newMod.Id,
                CharacterInternalName = _characterModList.Character.InternalName,
                ModCustomName = newMod.Settings.TryGetSettings(out var settings)
                    ? settings.CustomName ?? newMod.Name
                    : newMod.Name,
                ModFolderName = newMod.Name,
                ShowOnOverview = true,
                AttentionType = AttentionType.Added,
                Message = "Mod was successfully added"
            }));
    }


    private bool _canSetRootFolder(object? fileSystemObject)
    {
        if (fileSystemObject is not FileSystemItem fileSystemItem || _modInstallation is null)
            return false;

        if (!fileSystemItem.IsFolder)
            return false;

        if (fileSystemItem.Path == LastSelectedRootFolder?.Path ||
            fileSystemItem.Path == LastSelectedShaderFixesFolder?.Path)
            return false;

        return true;
    }

    [RelayCommand(CanExecute = nameof(_canSetRootFolder))]
    private async Task SetRootFolderAsync(object? fileSystemObject)
    {
        if (fileSystemObject is not FileSystemItem fileSystemItem || _modInstallation is null)
            return;

        if (!fileSystemItem.IsFolder)
            return;

        if (LastSelectedRootFolder is not null)
            LastSelectedRootFolder.RightIcon = null;


        _modInstallation.SetRootModFolder(new DirectoryInfo(fileSystemItem.Path));
        var modSettings = await _modInstallation.TryReadModSettingsAsync();

        if (modSettings is not null)
        {
            if (CustomName.IsNullOrEmpty())
                CustomName = modSettings.CustomName ?? string.Empty;
            if (Author.IsNullOrEmpty())
                Author = modSettings.Author ?? string.Empty;
            if (Description.IsNullOrEmpty())
                Description = modSettings.Description ?? string.Empty;
            if (ModUrl.IsNullOrEmpty())
                ModUrl = modSettings.ModUrl?.ToString() ?? string.Empty;
        }

        fileSystemItem.RightIcon = RootFolderIcon;
        LastSelectedRootFolder = fileSystemItem;
        AddModCommand.NotifyCanExecuteChanged();
    }


    private bool _canSetShaderFixesFolder(object? fileSystemObject)
    {
        //TODO: Enable later
        return false;

        if (fileSystemObject is not FileSystemItem fileSystemItem || _modInstallation is null)
            return false;

        if (!fileSystemItem.IsFolder)
            return false;

        if (fileSystemItem.Path == LastSelectedRootFolder?.Path ||
            fileSystemItem.Path == LastSelectedShaderFixesFolder?.Path)
            return false;

        return true;
    }

    [RelayCommand(CanExecute = nameof(_canSetShaderFixesFolder))]
    private void SetShaderFixesFolder(object? fileSystemObject)
    {
        //TODO: Enable later
        return;
        if (fileSystemObject is not FileSystemItem fileSystemItem || _modInstallation is null)
            return;

        if (!fileSystemItem.IsFolder)
            return;

        if (LastSelectedShaderFixesFolder is not null)
            LastSelectedShaderFixesFolder.RightIcon = null;

        _modInstallation.SetShaderFixesFolder(new DirectoryInfo(fileSystemItem.Path));
        fileSystemItem.RightIcon = ShaderFixesFolderIcon;
        LastSelectedShaderFixesFolder = fileSystemItem;
    }

    private bool _canSetModPreviewImage(object? fileSystemObject)
    {
        if (fileSystemObject is not FileSystemItem fileSystemItem || _modInstallation is null || !fileSystemItem.IsFile)
            return false;

        if (!Constants.SupportedImageExtensions.Contains(Path.GetExtension(fileSystemItem.Name)))
            return false;

        if (fileSystemItem.Path == LastSelectedImageFile?.Path)
            return false;

        return true;
    }

    [RelayCommand(CanExecute = nameof(_canSetModPreviewImage))]
    private void SetModPreviewImage(object? fileSystemObject)
    {
        if (fileSystemObject is not FileSystemItem fileSystemItem || _modInstallation is null || !fileSystemItem.IsFile)
            return;

        if (!Constants.SupportedImageExtensions.Contains(Path.GetExtension(fileSystemItem.Name)))
            return;

        ModPreviewImagePath = new Uri(fileSystemItem.Path);
        fileSystemItem.RightIcon = SelectedImageIcon;
        if (LastSelectedImageFile is not null)
        {
            LastSelectedImageFile.RightIcon = null;
        }

        LastSelectedImageFile = fileSystemItem;
    }

    [RelayCommand]
    private void ClearModPreviewImage()
    {
        ModPreviewImagePath = _placeholderImageUri;
        if (LastSelectedImageFile is not null)
        {
            LastSelectedImageFile.RightIcon = null;
            LastSelectedImageFile = null;
        }
    }

    private bool _canCopyImage()
    {
        return ModPreviewImagePath != _placeholderImageUri &&
               File.Exists(ModPreviewImagePath.LocalPath);
    }

    [RelayCommand(CanExecute = nameof(_canCopyImage))]
    private async Task CopyImageAsync()
    {
        await ImageHandlerService
            .CopyImageToClipboardAsync(await StorageFile.GetFileFromPathAsync(ModPreviewImagePath.LocalPath))
            .ConfigureAwait(false);
    }


    private bool _canPasteModImage()
    {
        var package = Clipboard.GetContent();

        if (package is null)
            return false;

        if (package.Contains(StandardDataFormats.Bitmap))
            return true;

        if (!package.Contains(StandardDataFormats.StorageItems))
            return false;

        return true;
    }

    [RelayCommand(CanExecute = nameof(_canPasteModImage))]
    private async Task PasteModImageAsync()
    {
        Uri? imageUri;
        try
        {
            imageUri = await Task.Run(() => _imageHandlerService.GetImageFromClipboardAsync());
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed retrieve image from clipboard");
            _notificationManager.ShowNotification("Failed retrieve image from clipboard", e.Message,
                TimeSpan.FromSeconds(5));
            return;
        }

        if (imageUri is not null)
        {
            ModPreviewImagePath = imageUri;
            if (LastSelectedImageFile is not null)
            {
                LastSelectedImageFile.RightIcon = null;
                LastSelectedImageFile = null;
            }
        }
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        var window = _windowManagerService.GetWindow(_characterModList);

        var imageUri = await _imageHandlerService.PickImageAsync(window: window);
        if (imageUri is null || !File.Exists(imageUri.Path)) return;


        ModPreviewImagePath = new Uri(imageUri.Path);
        if (LastSelectedImageFile is not null)
        {
            LastSelectedImageFile.RightIcon = null;
            LastSelectedImageFile = null;
        }
    }


    [MemberNotNullWhen(true, nameof(_modInstallation))]
    private bool canAddMod()
    {
        if (_modInstallation is null)
            return false;

        return true;
    }

    [RelayCommand(CanExecute = nameof(canAddMod))]
    private async Task AddModAsync()
    {
        if (!canAddMod())
            return;

        var skinModDupe = _modInstallation.AnyDuplicateName();

        if (skinModDupe is not null)
        {
            _duplicateMod = skinModDupe;
            DuplicateModFolderName = ModFolderHelpers.GetFolderNameWithoutDisabledPrefix(skinModDupe.Name);
            DuplicateModPath = new Uri(skinModDupe.FullPath);
            OverwriteExistingMod = false;
            skinModDupe.Settings.TryGetSettings(out var skinSettings);

            if (skinSettings is not null && !skinSettings.CustomName.IsNullOrEmpty())
            {
                DuplicateModCustomName = skinSettings.CustomName;
            }

            ModFolderName = _modInstallation.ModFolder.Name;

            AddModDialogCommand = new AsyncRelayCommand(AddModAndRenameAsync, canExecute: canAddModAndRename);
            DuplicateModDialog?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            var skinMod = await Task.Run(() => _modInstallation.AddModAsync(GetModOptions()));
            SuccessfulInstall(skinMod);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to add mod");
            ErrorOccurred();
        }
        finally
        {
            Finally();
        }
    }

    private async Task AddModAndReplaceAsync()
    {
        if (_modInstallation is null)
            return;

        if (_duplicateMod is null)
            return;

        try
        {
            var skinMod = await Task.Run(() => _modInstallation.AddAndReplaceAsync(_duplicateMod, GetModOptions()));
            SuccessfulInstall(skinMod);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to add mod");
            ErrorOccurred();
        }
        finally
        {
            Finally();
        }
    }

    [MemberNotNullWhen(true, nameof(_modInstallation), nameof(_duplicateMod))]
    private bool canAddModAndRename()
    {
        if (_modInstallation is null)
            return false;

        if (ModFolderName.IsNullOrEmpty() || DuplicateModFolderName.IsNullOrEmpty())
            return false;

        if (ModFolderName == DuplicateModFolderName)
            return false;

        if (_duplicateMod is null)
            return false;

        if (DuplicateModFolderName != _duplicateMod?.Name)
            foreach (var skinEntry in _characterModList.Mods)
            {
                if (ModFolderHelpers.FolderNameEquals(skinEntry.Mod.Name, DuplicateModFolderName))
                    return false;
            }

        if (ModFolderName != _modInstallation.ModFolder.Name)
            foreach (var skinEntry in _characterModList.Mods)
            {
                if (ModFolderHelpers.FolderNameEquals(skinEntry.Mod.Name, ModFolderName))
                    return false;
            }


        return true;
    }

    private async Task AddModAndRenameAsync()
    {
        if (!canAddModAndRename())
            return;
        try
        {
            var skinMod = await Task.Run(() => _modInstallation.RenameAndAddAsync(GetModOptions(ModFolderName),
                _duplicateMod, DuplicateModFolderName, DuplicateModCustomName));
            SuccessfulInstall(skinMod);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to add mod");
            ErrorOccurred();
        }
        finally
        {
            Finally();
        }
    }

    private static readonly Dictionary<Uri, ModPageDataResult> _modPageDataCache = new();

    private async Task GetModInfo(string url)
    {
        if (url.IsNullOrEmpty() || IsRetrievingModInfo || (!CustomName.IsNullOrEmpty() && !Author.IsNullOrEmpty()))
            return;

        var isValidUrl = Uri.TryCreate(url, UriKind.Absolute, out var modPageUrl) &&
                         (modPageUrl.Scheme == Uri.UriSchemeHttps &&
                          modPageUrl.Host.Equals("gamebanana.com", StringComparison.OrdinalIgnoreCase));

        if (!isValidUrl || modPageUrl is null)
            return;

        IsRetrievingModInfo = true;

        try
        {
            if (!_modPageDataCache.TryGetValue(modPageUrl, out var modInfo))
            {
                modInfo = await Task.Run(() =>
                    App.GetService<IModUpdateChecker>().GetModPageDataAsync(modPageUrl, CancellationToken.None));
                _modPageDataCache.Add(modPageUrl, modInfo);
            }

            if (CustomName.IsNullOrEmpty() && !modInfo.ModName.IsNullOrEmpty())
                CustomName = modInfo.ModName;

            if (Author.IsNullOrEmpty() && !modInfo.AuthorName.IsNullOrEmpty())
                Author = modInfo.AuthorName;

            if (ModPreviewImagePath == _placeholderImageUri)
            {
                var newImageUrl = modInfo.PreviewImages?.FirstOrDefault();

                try
                {
                    if (newImageUrl is not null)
                    {
                        var newImage = await Task.Run(() => _imageHandlerService.DownloadImageAsync(newImageUrl));
                        ModPreviewImagePath = new Uri(newImage.Path);
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Failed to download image");
                    _notificationManager.ShowNotification("Failed to download image from modUrl", e.Message,
                        TimeSpan.FromSeconds(5));
                }
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to retrieve mod info");
        }
        finally
        {
            IsRetrievingModInfo = false;
        }
    }

    private void OnOverwriteExistingModChanged()
    {
        if (OverwriteExistingMod)
        {
            PrimaryButtonText = AddReplaceText;
            AddModDialogCommand = new AsyncRelayCommand(AddModAndReplaceAsync);
            CanExecuteDialogCommand = true;
        }
        else
        {
            PrimaryButtonText = AddRenameText;
            AddModDialogCommand = new AsyncRelayCommand(AddModAndRenameAsync, canExecute: canAddModAndRename);
            CanExecuteDialogCommand = canAddModAndRename();
        }
    }

    private AddModOptions GetModOptions(string? newModFolderName = null)
    {
        return new AddModOptions
        {
            NewModFolderName = newModFolderName,
            ModName = CustomName,
            ModUrl = ModUrl,
            Author = Author,
            Description = Description,
            ModImage = ModPreviewImagePath == _placeholderImageUri ? null : ModPreviewImagePath
        };
    }

    public void Dispose()
    {
        _modInstallation?.Dispose();
        _modInstallation = null;
    }

    private void ErrorOccurred()
    {
        InstallerFinished?.Invoke(this, EventArgs.Empty);
        Win32.PlaySound("SystemAsterisk", nuint.Zero,
            (uint)(Win32.SoundFlags.SND_ALIAS | Win32.SoundFlags.SND_NODEFAULT));
        _notificationManager.ShowNotification("An error occurred",
            "An error occurred while adding the mod. See logs for more details",
            TimeSpan.FromSeconds(10));
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }


    [RelayCommand]
    private async Task EnableOnlyToggleAsync()
    {
        var settings = await _localSettingsService
            .ReadOrCreateSettingAsync<ModInstallerSettings>(ModInstallerSettings.Key)
            .ConfigureAwait(false);
        settings.EnableModOnInstall = EnableThisMod;
        await _localSettingsService.SaveSettingAsync(ModInstallerSettings.Key, settings)
            .ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ToggleAlwaysOnTopAsync()
    {
        var settings = await _localSettingsService
            .ReadOrCreateSettingAsync<ModInstallerSettings>(ModInstallerSettings.Key)
            .ConfigureAwait(false);
        settings.ModInstallerWindowOnTop = AlwaysOnTop;
        await _localSettingsService.SaveSettingAsync(ModInstallerSettings.Key, settings)
            .ConfigureAwait(false);

        var window = _windowManagerService.GetWindow(_characterModList);

        _dispatcherQueue?.TryEnqueue(() => window?.SetIsAlwaysOnTop(AlwaysOnTop));
    }

    private async Task EnableOnlyMod(ISkinMod installedMod)
    {
        if (_characterModList.Character is ICharacter { Skins.Count: <= 1 } ||
            _characterModList.Character is not ICharacter character)
        {
            var enabledMods = _characterModList.Mods
                .Where(mod => mod.IsEnabled && mod.Mod.Id != installedMod.Id).ToArray();
            foreach (var mod in enabledMods)
            {
                DisableMod(mod.Mod);
            }

            EnableMod(installedMod);

            _logger.Debug("Disabled {disabledMods} and enabled {enabledMod}",
                string.Join(',', enabledMods.Select(mod => mod.Mod.Name)),
                installedMod.Name);
            return;
        }

        var detectedSkin = await _characterSkinService.GetFirstSkinForModAsync(installedMod, character)
            .ConfigureAwait(false);

        if (_inGameSkin is null)
        {
            if (detectedSkin is not null)
            {
                _inGameSkin = detectedSkin;
            }
            else
            {
                _notificationManager.QueueNotification("Could not determine skin for new mod",
                    "JASM could not determine what ingame skin this mod is for, therefore it can't determine what mods to disable.");
                return;
            }
        }

        var disabledMods = new List<ISkinMod>();
        await foreach (var skinMod in _characterSkinService.GetModsForSkinAsync(_inGameSkin).ConfigureAwait(false))
        {
            if (skinMod.Id == installedMod.Id)
                continue;
            DisableMod(skinMod);
            disabledMods.Add(skinMod);
        }

        if (detectedSkin is not null && !detectedSkin.InternalNameEquals(_inGameSkin.InternalName))
        {
            await _modSettingsService.SetCharacterSkinOverride(installedMod.Id, _inGameSkin.InternalName)
                .ConfigureAwait(false);
        }


        EnableMod(installedMod);


        _logger.Debug("Disabled {disabledMods} and enabled {enabledMod}, also set skin override for mod to {SkinName}",
            string.Join(',', disabledMods.Select(mod => mod.Name)),
            installedMod.Name, _inGameSkin.InternalName.Id);
    }


    private void DisableMod(ISkinMod mod)
    {
        if (_characterModList.IsModEnabled(mod))
            _characterModList.DisableMod(mod.Id);
    }

    private void EnableMod(ISkinMod mod)
    {
        if (!_characterModList.IsModEnabled(mod))
            _characterModList.EnableMod(mod.Id);
    }

    private void Finally()
    {
        _windowManagerService.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            _windowManagerService.MainWindow.BringToFront();
        });
    }
}

public partial class RootFolder : ObservableObject
{
    private readonly DirectoryInfo _folder;
    public string Path => _folder.FullName;
    public string Name => _folder.Name;

    public RootFolder(DirectoryInfo folder)
    {
        _folder = folder;
        _folder.EnumerateFileSystemInfos().ForEach(fse => FileSystemItems.Add(new FileSystemItem(fse)));
    }

    [ObservableProperty] private ObservableCollection<FileSystemItem> _fileSystemItems = new();

    public FileSystemItem? GetByPath(string path)
    {
        foreach (var fileSystemItem in FileSystemItems)
        {
            if (fileSystemItem.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                return fileSystemItem;

            var fs = fileSystemItem.GetByPath(path);

            if (fs is not null)
                return fs;
        }

        return null;
    }
}

public partial class FileSystemItem : ObservableObject
{
    private readonly FileSystemInfo _fileSystemInfo;
    public string Path => _fileSystemInfo.FullName;
    public string Name => _fileSystemInfo.Name;

    public bool IsFolder => _fileSystemInfo is DirectoryInfo;

    public bool IsFile => _fileSystemInfo is FileInfo;

    [ObservableProperty] private bool _isExpanded;

    [ObservableProperty] private bool _isSelected;

    [ObservableProperty] private ObservableCollection<FileSystemItem> _fileSystemItems = new();


    [ObservableProperty] private string? _leftIcon;
    [ObservableProperty] private string? _rightIcon;

    public FileSystemItem(FileSystemInfo fileSystemInfo, int recursionCount = 0)
    {
        _fileSystemInfo = fileSystemInfo;

        if (recursionCount < 2)
        {
            _isExpanded = true;
        }

        if (recursionCount > 5)
        {
            return;
        }

        if (fileSystemInfo is DirectoryInfo dir)
        {
            LeftIcon = "\uE8B7";
            dir.EnumerateFileSystemInfos()
                .ForEach(fse => FileSystemItems.Add(new FileSystemItem(fse, recursionCount + 1)));
        }
    }

    public FileSystemItem? GetByPath(string path)
    {
        foreach (var fileSystemItem in FileSystemItems)
        {
            if (fileSystemItem.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                return fileSystemItem;

            var found = fileSystemItem.GetByPath(path);
            if (found != null) return found;
        }

        return null;
    }
}