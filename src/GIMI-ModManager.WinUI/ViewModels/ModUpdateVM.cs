using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities.Mods.Exceptions;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.Core.Services.GameBanana;
using GIMI_ModManager.Core.Services.GameBanana.Models;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Dispatching;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class ModUpdateVM : ObservableRecipient
{
    private readonly GameBananaCoreService _gameBananaCoreService = App.GetService<GameBananaCoreService>();
    private readonly ISkinManagerService _skinManagerService = App.GetService<ISkinManagerService>();
    private readonly ModNotificationManager _modNotificationManager = App.GetService<ModNotificationManager>();
    private readonly NotificationManager _notificationManager = App.GetService<NotificationManager>();
    private readonly ModInstallerService _modInstallerService = App.GetService<ModInstallerService>();
    private readonly ArchiveService _archiveService = App.GetService<ArchiveService>();

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Guid _notificationId;
    private ICharacterModList _characterModList = null!;
    private readonly WindowEx _window;
    private ModNotification? _notification;
    private List<ModFileInfo> _modFiles = new();
    private readonly CancellationToken _ct;

    private ModPageInfo? _modPageInfo;

    [ObservableProperty] private string _initializing = "true";

    [ObservableProperty] private string _modName = string.Empty;

    [ObservableProperty] private Uri _modPage = new("https://gamebanana.com/");

    [ObservableProperty] private Uri? _modPath = null;

    [ObservableProperty] private DateTime _lastUpdateCheck = DateTime.Now;

    [ObservableProperty] private bool _isOpenDownloadButtonEnabled = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyCanExecuteChangedFor(nameof(IgnoreAndCloseCommand), nameof(StartDownloadCommand),
        nameof(StartInstallCommand))]
    private bool _isWindowBusy = false;

    public bool IsNotBusy => !IsWindowBusy;


    public readonly ObservableCollection<ModFileInfoVm> ModFileInfos = new();
    private readonly ILogger _logger = App.GetService<ILogger>().ForContext<ModUpdateVM>();

    public ModUpdateVM(Guid notificationId, WindowEx window, CancellationToken ctsToken)
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _notificationId = notificationId;
        _window = window;
        _ct = ctsToken;
        Initialize();
    }

    private async void Initialize()
    {
        try
        {
            await InternalInitialize();
            Initializing = "false";
        }
        catch (Exception e)
        {
            await LogErrorAndClose(e);
        }
    }


    private async Task InternalInitialize()
    {
        IsWindowBusy = true;
        _notification =
            await _modNotificationManager.GetNotificationById(_notificationId);

        if (_notification?.ModsRetrievedResult is null)
        {
            await LogErrorAndClose(new InvalidOperationException("Failed to get mod page info, mod info is missing"));
            return;
        }

        var characterSkinEntry = _skinManagerService.GetModEntryById(_notification.ModId);

        if (characterSkinEntry is null)
        {
            await LogErrorAndClose(new InvalidOperationException($"Mod with id {_notification.ModId} not found"));
            return;
        }

        _characterModList = characterSkinEntry.ModList;
        var mod = characterSkinEntry.Mod;

        var modSettings = mod.Settings.GetSettingsLegacy().TryPickT0(out var settings, out _) ? settings : null;

        if (modSettings is null)
        {
            await LogErrorAndClose(new ModSettingsNotFoundException($"Mod settings not found for mod {mod.FullPath}"));
            return;
        }

        if (!await _gameBananaCoreService.HealthCheckAsync(_ct))
        {
            await LogErrorAndClose(
                new InvalidOperationException("Failed to get mod page info, GameBanana Api is not available"),
                removeNotification: false);
            return;
        }

        _modPageInfo =
            await _gameBananaCoreService.GetModProfileAsync(new GbModId(_notification.ModsRetrievedResult.ModId), _ct);

        if (_modPageInfo is null)
        {
            await LogErrorAndClose(new InvalidOperationException("Failed to get mod page info, mod does not exist"));
            return;
        }

        ModName = modSettings.CustomName ?? mod.Name;
        if (_modPageInfo.ModPageUrl is not null)
            ModPage = _modPageInfo.ModPageUrl;

        ModPath = new Uri(mod.FullPath);
        LastUpdateCheck = _notification.ModsRetrievedResult.LastCheck;


        _modFiles = _modPageInfo.Files.ToList();

        foreach (var modFile in _modFiles)
        {
            var vm = new ModFileInfoVm(modFile, StartDownloadCommand, StartInstallCommand)
            {
                IsNew = modFile.DateAdded > LastUpdateCheck,
                IsBusy = true
            };
            ModFileInfos.Add(vm);
            await InitializeModFileVmAsync(vm);
        }

        IsWindowBusy = false;
    }

    private async Task LogErrorAndClose(Exception e, bool removeNotification = true)
    {
        _logger.Error(e, "Failed to get mod update info");
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            App.GetService<NotificationManager>().ShowNotification("Failed to get mod update info",
                e.Message, TimeSpan.FromSeconds(10));
        });
        _window.Close();

        if (!removeNotification)
            return;

        var notification = await _modNotificationManager.GetNotificationById(_notificationId);
        if (notification is null)
        {
            return;
        }

        await _modNotificationManager.RemoveModNotificationAsync(notification.Id);
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task IgnoreAndCloseAsync()
    {
        var notification = await _modNotificationManager.GetNotificationById(_notificationId);
        if (notification is null)
        {
            return;
        }

        await _modNotificationManager.RemoveModNotificationAsync(notification.Id);
        _window.Close();
    }


    private async Task InitializeModFileVmAsync(ModFileInfoVm fileInfoVm)
    {
        ArgumentNullException.ThrowIfNull(fileInfoVm);

        var existingArchive = await _gameBananaCoreService.GetLocalModArchiveByMd5HashAsync(fileInfoVm.Md5Hash, _ct);

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (existingArchive is not null)
            {
                fileInfoVm.Status = ModFileInfoVm.InstallStatus.Downloaded;
                fileInfoVm.DownloadProgress = 100;
                fileInfoVm.ArchiveFile = new FileInfo(existingArchive.FullName);
            }

            fileInfoVm.IsBusy = false;
        });
    }

    private bool CanStartDownload(ModFileInfoVm? fileInfoVm)
    {
        if (fileInfoVm is null)
            return false;

        var anyOtherDownloading = ModFileInfos.Any(x =>
            fileInfoVm.FileId != x.FileId && x.Status == ModFileInfoVm.InstallStatus.Downloading);

        var canDownload = IsNotBusy && !fileInfoVm.IsBusy && fileInfoVm.Status == ModFileInfoVm.InstallStatus.NotStarted
                          && !anyOtherDownloading && fileInfoVm.ArchiveFile is null &&
                          !fileInfoVm.ModId.IsNullOrEmpty() && !fileInfoVm.FileId.IsNullOrEmpty();


        return canDownload;
    }

    [RelayCommand(CanExecute = nameof(CanStartDownload))]
    private async Task StartDownload(ModFileInfoVm fileInfoVm)
    {
        try
        {
            fileInfoVm.Status = ModFileInfoVm.InstallStatus.Downloading;
            fileInfoVm.IsBusy = true;

            var identifier = new GbModFileIdentifier(new GbModId(fileInfoVm.ModId), new GbModFileId(fileInfoVm.FileId));

            var archivePath =
                await Task.Run(() => _gameBananaCoreService.DownloadModAsync(identifier, fileInfoVm.Progress, _ct),
                    _ct);

            fileInfoVm.ArchiveFile = new FileInfo(archivePath);
            fileInfoVm.Status = ModFileInfoVm.InstallStatus.Downloaded;
        }
        catch (Exception) when (_ct.IsCancellationRequested)
        {
            Reset();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to download mod file");

            _notificationManager.ShowNotification("Failed to download mod file",
                e.Message, TimeSpan.FromSeconds(10));

            Reset();
        }
        finally
        {
            fileInfoVm.IsBusy = false;
        }

        void Reset()
        {
            fileInfoVm.Status = ModFileInfoVm.InstallStatus.NotStarted;
            fileInfoVm.ArchiveFile = null;
            fileInfoVm.DownloadProgress = 0;
        }
    }

    private bool CanInstall(ModFileInfoVm? fileInfoVm)
    {
        if (fileInfoVm is null)
            return false;

        var anyOtherInstalling = ModFileInfos.Any(x =>
            fileInfoVm.FileId != x.FileId && x.Status == ModFileInfoVm.InstallStatus.Installing);

        var canInstall = IsNotBusy && !fileInfoVm.IsBusy &&
                         fileInfoVm.Status == ModFileInfoVm.InstallStatus.Downloaded &&
                         !anyOtherInstalling && fileInfoVm.ArchiveFile is not null;

        return canInstall;
    }


    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task StartInstall(ModFileInfoVm fileInfoVm)
    {
        IsWindowBusy = true;
        fileInfoVm.IsBusy = true;

        fileInfoVm.Status = ModFileInfoVm.InstallStatus.Installing;

        try
        {
            var result = await Task.Run(async () =>
            {
                var modFolder = _archiveService.ExtractArchive(fileInfoVm.ArchiveFile!.FullName,
                    App.GetUniqueTmpFolder().FullName);

                var archiveNameSections = Path.GetFileName(modFolder.Name).Split(ModArchiveRepository.Separator);
                if (archiveNameSections.Length != 4)
                    throw new InvalidArchiveNameFormatException();

                var modFolderName = archiveNameSections[0];
                var modFolderExt = Path.GetExtension(modFolder.Name);

                var modFolderParent = modFolder.Parent!;

                var zipRoot = Directory.CreateDirectory(Path.Combine(modFolderParent.FullName, "ArchiveRoot"));

                modFolder.MoveTo(Path.Combine(zipRoot.FullName, $"{modFolderName}{modFolderExt}"));

                var modUrl = _modPageInfo?.ModPageUrl;

                using var task = await _modInstallerService.StartModInstallationAsync(zipRoot, _characterModList,
                    setup: options => options.ModUrl = modUrl).ConfigureAwait(false);

                return await task.WaitForCloseAsync(_ct).ConfigureAwait(false);
            }, _ct);

            if (result.CloseReason == CloseRequestedArgs.CloseReasons.Error)
            {
                if (result.Exception is not null)
                {
                    throw new Exception("An error occured during mod install, see logs and inner exception",
                        result.Exception);
                }

                throw new Exception("An error occured during mod install, see logs");
            }

            if (result.CloseReason == CloseRequestedArgs.CloseReasons.Canceled)
            {
                fileInfoVm.Status = ModFileInfoVm.InstallStatus.Downloaded;
                return;
            }

            fileInfoVm.Status = ModFileInfoVm.InstallStatus.Installed;
        }
        catch (TaskCanceledException)
        {
            fileInfoVm.Status = ModFileInfoVm.InstallStatus.Downloaded;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to install mod file");

            _notificationManager.ShowNotification("Failed to install mod file",
                e.InnerException?.Message ?? e.Message, TimeSpan.FromSeconds(10));

            fileInfoVm.Status = ModFileInfoVm.InstallStatus.Downloaded;
        }
        finally
        {
            IsWindowBusy = false;
            fileInfoVm.IsBusy = false;
        }
    }
}

public partial class ModFileInfoVm : ObservableObject
{
    private readonly ModFileInfo _modFileInfo;

    public string ModId => _modFileInfo.ModId;
    public string FileId => _modFileInfo.FileId;
    public string FileName => _modFileInfo.FileName;

    public DateTime DateAdded => _modFileInfo.DateAdded;

    public string DateAddedTooltipFormat => $"Submitted: {DateAdded}";

    public TimeSpan Age => DateTime.Now - DateAdded;

    public string AgeFormated => FormaterHelpers.FormatTimeSinceAdded(Age);

    public string Description => _modFileInfo.Description;

    public string Md5Hash => _modFileInfo.Md5Checksum;
    [ObservableProperty] private bool _isNew;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand), nameof(InstallCommand))]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand), nameof(InstallCommand))]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton), nameof(ShowInstallButton))]
    private InstallStatus _status = InstallStatus.NotStarted;

    [ObservableProperty] private int _downloadProgress;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(DownloadCommand))]
    private FileInfo? _archiveFile;

    public IProgress<int> Progress { get; }


    public bool ShowDownloadButton => Status is InstallStatus.NotStarted or InstallStatus.Downloading;

    public bool ShowInstallButton =>
        Status is InstallStatus.Downloaded or InstallStatus.Installing or InstallStatus.Installed;

    public ModFileInfoVm(ModFileInfo modFileInfo,
        IAsyncRelayCommand downloadCommand, IAsyncRelayCommand installCommand)
    {
        _modFileInfo = modFileInfo;
        DownloadCommand = downloadCommand;
        InstallCommand = installCommand;
        Progress = new Progress<int>(i => DownloadProgress = i);
    }

    public IAsyncRelayCommand DownloadCommand { get; }
    public IAsyncRelayCommand InstallCommand { get; }


    public enum InstallStatus
    {
        NotStarted,
        Downloading, // Downloading from GameBanana
        Downloaded, // Downloaded from GameBanana
        Installing, // Installing to the game, mod installation window open
        Installed // Installed to the game
    }
}