using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.Exceptions;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class ModUpdateVM : ObservableRecipient
{
    private static string DownloadsFolder = Path.Combine(App.TMP_DIR, "Downloads");
    private readonly CancellationToken _cancellationToken;
    private readonly GameBananaService _gameBananaService = App.GetService<GameBananaService>();
    private readonly ISkinManagerService _skinManagerService = App.GetService<ISkinManagerService>();
    private readonly ModNotificationManager _modNotificationManager = App.GetService<ModNotificationManager>();
    private readonly ModInstallerService _modInstallerService = App.GetService<ModInstallerService>();
    private readonly DownloadCacheService _downloadCacheService = App.GetService<DownloadCacheService>();

    private readonly Guid _notificationId;
    private readonly WindowEx _window;
    private ModNotification? _notification;

    [ObservableProperty] private string _modName = string.Empty;

    [ObservableProperty] private Uri _modPage = new("https://gamebanana.com/");

    [ObservableProperty] private Uri? _modPath = null;

    [ObservableProperty] private DateTime _lastUpdateCheck = DateTime.Now;

    [ObservableProperty] private bool _isOpenDownloadButtonEnabled = false;

    [ObservableProperty] private bool _isGameBananaOk = false;


    public ObservableCollection<UpdateCheckResultVm> Results = new();
    private readonly ILogger _logger = App.GetService<ILogger>().ForContext<ModUpdateVM>();

    public ModUpdateVM(Guid notificationId, WindowEx window, CancellationToken cancellationToken)
    {
        _notificationId = notificationId;
        _window = window;
        Initialize();
        _cancellationToken = cancellationToken;
    }

    private async Task InternalInitialize()
    {
        ModsRetrievedResult? modResult = null;
        try
        {
            _notification =
                await _modNotificationManager.GetNotificationById(_notificationId) ??
                throw new InvalidOperationException();
            modResult = _notification.ModsRetrievedResult ??
                        await _gameBananaService.GetAvailableMods(_notification.ModId, _cancellationToken);
        }
        catch (Exception e)
        {
            LogErrorAndClose(e);
            return;
        }

        var mod = _skinManagerService.GetModById(_notification.ModId);
        if (mod is null)
        {
            LogErrorAndClose(new InvalidOperationException($"Mod with id {_notification.ModId} not found"));
            return;
        }

        var modSettings = mod.Settings.GetSettingsLegacy().TryPickT0(out var settings, out _) ? settings : null;

        if (modSettings is null)
        {
            LogErrorAndClose(new ModSettingsNotFoundException($"Mod settings not found for mod {mod.FullPath}"));
            return;
        }

        ModName = modSettings.CustomName ?? mod.Name;
        ModPage = modResult.SitePageUrl;
        ModPath = new Uri(mod.FullPath);
        LastUpdateCheck = modResult.LastCheck;

        modResult.Mods.ForEach(x => Results.Add(new UpdateCheckResultVm(x, StartDownload, InstallAsync)));
        var client = App.GetService<IModUpdateChecker>();
        var isAvailable = await client.CheckSiteStatusAsync(_cancellationToken);

        if (!isAvailable) return;

        IsGameBananaOk = true;


        var availableMods = await _gameBananaService.GetAvailableMods(_notification.ModId, _cancellationToken);

        Results.ForEach(x =>
        {
            if (availableMods.Mods.Any(y => y.FileId == x.UpdateCheckResult.FileId))
                x.IsDownloadButtonEnabled = true;
        });
    }

    private async void Initialize()
    {
        try
        {
            await InternalInitialize();
        }
        catch (Exception e)
        {
            LogErrorAndClose(e);
        }
    }

    private void LogErrorAndClose(Exception e)
    {
        _logger.Error(e, "Failed to get mod update info");
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            App.GetService<NotificationManager>().ShowNotification("Failed to get mod update info",
                $"Failed to get mod update info", TimeSpan.FromSeconds(10));
        });
        _window.DispatcherQueue.TryEnqueue(() => _window.Close());
    }

    [RelayCommand]
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


    private async Task StartDownload(UpdateCheckResultVm updateCheckResultObject)
    {
        if (updateCheckResultObject is not { IsDownloadButtonEnabled: true } updateCheckResult)
        {
            Debugger.Break();
            return;
        }

        updateCheckResult.IsDownloadButtonEnabled = false;

        var downloadClient = App.GetService<IModUpdateChecker>();

        var fileName = updateCheckResult.UpdateCheckResult.FileName;
        var fileId = updateCheckResult.UpdateCheckResult.FileId;

        if (_downloadCacheService.GetCachedArchive(fileId) is { } cachedEntry)
        {
            updateCheckResult.DownloadFilePath = cachedEntry.ArchivePath;
            updateCheckResult.ShowDownloadButton = false;
            updateCheckResult.CanInstall = true;
            updateCheckResult.ShowInstallButton = true;
            updateCheckResult.DownloadProgress = 100;
            return;
        }

        var tmpFile = File.Create(Path.Combine(DownloadsFolder, fileName));
        updateCheckResult.DownloadFilePath = tmpFile.Name;

        try
        {
            updateCheckResult.IsDownloading = true;
            await Task.Run(
                () => downloadClient.DownloadModAsync(fileId, tmpFile, updateCheckResult.Progress, _cancellationToken),
                _cancellationToken);
            updateCheckResult.IsDownloading = false;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to download mod");
            await tmpFile.DisposeAsync();
            File.Delete(tmpFile.Name);
            return;
        }

        await tmpFile.DisposeAsync();

        await Task.Run(() => _downloadCacheService.CacheArchive(tmpFile.Name, fileId), _cancellationToken);

        updateCheckResult.ShowDownloadButton = false;
        updateCheckResult.CanInstall = true;
        updateCheckResult.ShowInstallButton = true;
    }


    private DirectoryInfo GetDownloadFolder(UpdateCheckResultVm updateCheckResultVm)
    {
        var apiResult = updateCheckResultVm.UpdateCheckResult;
        var downloadFolder = new DirectoryInfo(Path.Combine(DownloadsFolder,
            apiResult.FileName + "__" + apiResult.FileId + "__" + apiResult.Md5Checksum));

        if (!downloadFolder.Exists)
            downloadFolder.Create();

        return downloadFolder;
    }

    private (string fileName, string fileId, string checksum)? GetDownloadInfo(DirectoryInfo directoryInfo)
    {
        try
        {
            var fileName = directoryInfo.Name.Split("__")[0];
            var fileId = directoryInfo.Name.Split("__")[1];
            var checksum = directoryInfo.Name.Split("__")[2];
            return (fileName, fileId, checksum);
        }
        catch (Exception e)
        {
            return null;
        }
    }

    private async Task InstallAsync(UpdateCheckResultVm updateCheckResultVm)
    {
        var archivePath = updateCheckResultVm.DownloadFilePath!;

        var extractor = App.GetService<ArchiveService>();


        var downloadedModCacheEntry =
            _downloadCacheService.GetCachedArchive(updateCheckResultVm.UpdateCheckResult.FileId);

        if (downloadedModCacheEntry is null)
        {
            updateCheckResultVm.CanInstall = false;
            return;
        }


        DirectoryInfo? extractedFolder;

        if (downloadedModCacheEntry.ExtractedFolder is { Exists: true } &&
            downloadedModCacheEntry.ExtractedFolder.EnumerateFileSystemInfos().Any())
        {
            extractedFolder = downloadedModCacheEntry.ExtractedFolder;
        }
        else
        {
            var downloadFolder = GetDownloadFolder(updateCheckResultVm);


            if (!downloadFolder.Exists || !downloadFolder.EnumerateFileSystemInfos().Any())
                    await Task.Run(() => extractor.ExtractArchive(archivePath, downloadFolder.FullName),
                        _cancellationToken);


            extractedFolder = downloadFolder;
            downloadedModCacheEntry.SetExtractedFolder(extractedFolder.FullName);
        }


        // TODO: Pass modlist when creating window instead

        var monitor = await Task.Run(() =>
        {
            var characterModList =
                _skinManagerService.CharacterModLists.First(modList =>
                    modList.Mods.Any(mod => mod.Id == _notification!.ModId));

            return _modInstallerService.StartModInstallationAsync(extractedFolder, characterModList);
        }, _cancellationToken);


        monitor!.Finished += (_, args) =>
        {
            if (args is { IsFinished: true, Installed: true })
                updateCheckResultVm.IsInstalled = true;

            if (args is { IsFinished: true, Installed: false, IsFailed: false })
                updateCheckResultVm.CanInstall = true;
        };
    }
}

public partial class UpdateCheckResultVm : ObservableObject
{
    public UpdateCheckResultVm(UpdateCheckResult updateCheckResult, Func<UpdateCheckResultVm, Task> downloadCommand,
        Func<UpdateCheckResultVm, Task> installCommand)
    {
        UpdateCheckResult = updateCheckResult;
        _downloadCommand = downloadCommand;
        _installCommand = installCommand;
        Progress = new Progress<int>(i => DownloadProgress = i);
    }

    private readonly Func<UpdateCheckResultVm, Task> _downloadCommand;
    private readonly Func<UpdateCheckResultVm, Task> _installCommand;

    public UpdateCheckResult UpdateCheckResult { get; }
    public IProgress<int> Progress { get; }

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(StartDownloadCommand))]
    private bool _isDownloadButtonEnabled;

    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private int _downloadProgress;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    private bool _canInstall;

    [ObservableProperty] private bool _isInstalled;

    [ObservableProperty] private bool _showDownloadButton = true;
    [ObservableProperty] private bool _showInstallButton;

    public string? DownloadFilePath { get; set; }

    [RelayCommand(CanExecute = nameof(IsDownloadButtonEnabled))]
    private async Task StartDownloadAsync()
    {
        var task = _downloadCommand(this);
        await task.ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        CanInstall = false;
        var task = _installCommand(this);
        await task.ConfigureAwait(false);
    }
}