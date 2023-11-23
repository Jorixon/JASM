using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities.Mods.Helpers;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.AppManagment;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Dispatching;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class ModInstallerVM : ObservableRecipient, INavigationAware, IDisposable
{
    private readonly ILogger _logger;
    private readonly ImageHandlerService _imageHandlerService;
    private readonly NotificationManager _notificationManager;
    private readonly ModNotificationManager _modNotificationManager;
    private readonly IWindowManagerService _windowManagerService;
    private readonly Uri _placeholderImageUri;
    private ICharacterModList _characterModList = null!;
    private DispatcherQueue? _dispatcherQueue;


    private IAsyncRelayCommand? _addModDialogCommand;

    public IAsyncRelayCommand AddModDialogCommand
    {
        get => _addModDialogCommand ??= new AsyncRelayCommand(AddModAsync, canExecute: canAddMod);
        set => SetProperty(ref _addModDialogCommand, value);
    }

    public event EventHandler? DuplicateModDialog;
    public event EventHandler? InstallerFinished;

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

    public readonly string RootFolderIcon = "\uF89A";
    public readonly string ShaderFixesFolderIcon = "\uE710";
    public readonly string SelectedImageIcon = "\uE8B9";
    public readonly string SelectedMergeIniIcon = "\uE8A5";

    [ObservableProperty] private FileSystemItem? _lastSelectedRootFolder;

    [ObservableProperty] private FileSystemItem? _lastSelectedImageFile;


    public ModInstallerVM(ILogger logger, ImageHandlerService imageHandlerService,
        NotificationManager notificationManager, IWindowManagerService windowManagerService,
        ModNotificationManager modNotificationManager)
    {
        _logger = logger;
        _imageHandlerService = imageHandlerService;
        _placeholderImageUri = imageHandlerService.PlaceholderImageUri;
        _notificationManager = notificationManager;
        _windowManagerService = windowManagerService;
        _modNotificationManager = modNotificationManager;
        PropertyChanged += OnPropertyChanged;
    }

    public Task InitializeAsync(ICharacterModList characterModList, DirectoryInfo modToInstall,
        DispatcherQueue dispatcherQueue)
    {
        _characterModList = characterModList;
        ModCharacterName = characterModList.Character.DisplayName;
        _modInstallation = ModInstallation.Start(modToInstall, _characterModList);
        _dispatcherQueue = dispatcherQueue;

        RootFolder.Clear();
        RootFolder.Add(new RootFolder(modToInstall));

        return Task.Run(() =>
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
        });
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
        InstallerFinished?.Invoke(this, EventArgs.Empty);
        _notificationManager.ShowNotification("Mod installed", "Mod was successfully added",
            TimeSpan.FromSeconds(5));
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

    private bool canAddMod()
    {
        if (_modInstallation is null)
            return false;

        return true;
    }

    [RelayCommand(CanExecute = nameof(canAddMod))]
    private async Task AddModAsync()
    {
        if (_modInstallation is null)
            return;

        throw new InvalidOperationException();
        var skinModDupe = _modInstallation.AnyDuplicateName();

        if (skinModDupe is not null)
        {
            _duplicateMod = skinModDupe;
            DuplicateModFolderName = skinModDupe.Name;
            DuplicateModPath = new Uri(skinModDupe.FullPath);
            OverwriteExistingMod = false;
            skinModDupe.Settings.TryGetSettings(out var skinSettings);

            if (skinSettings is not null)
            {
                DuplicateModCustomName =
                    skinSettings.CustomName.IsNullOrEmpty() ? skinModDupe.Name : skinSettings.CustomName;
            }

            ModFolderName = _modInstallation.ModFolder.Name;

            AddModDialogCommand = new AsyncRelayCommand(AddModAndRenameAsync, canExecute: canAddModAndRename);
            DuplicateModDialog?.Invoke(this, EventArgs.Empty);
            return;
        }


        var skinMod = await Task.Run(() => _modInstallation.AddModAsync(GetModOptions())).ConfigureAwait(false);

        SuccessfulInstall(skinMod);
    }

    private async Task AddModAndReplaceAsync()
    {
        if (_modInstallation is null)
            return;

        if (_duplicateMod is null)
            return;

        var skinMod = await Task.Run(() => _modInstallation.AddAndReplaceAsync(_duplicateMod, GetModOptions()))
            .ConfigureAwait(false);

        SuccessfulInstall(skinMod);
    }

    private bool canAddModAndRename()
    {
        if (_modInstallation is null)
            return false;

        if (ModFolderName.IsNullOrEmpty() || DuplicateModFolderName.IsNullOrEmpty())
            return false;

        if (ModFolderName == DuplicateModFolderName)
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
        if (_modInstallation is null || _duplicateMod is null || !canAddModAndRename())
            return;

        var skinMod = await Task.Run(() => _modInstallation.RenameAndAddAsync(GetModOptions(ModFolderName),
            _duplicateMod, DuplicateModFolderName, DuplicateModCustomName)).ConfigureAwait(false);
        SuccessfulInstall(skinMod);
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