using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities.Mods.Exceptions;
using GIMI_ModManager.Core.Services.GameBanana;
using GIMI_ModManager.Core.Services.GameBanana.Models;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class ModUpdateVM : ObservableRecipient
{
    private readonly GameBananaCoreService _gameBananaCoreService = App.GetService<GameBananaCoreService>();
    private readonly ISkinManagerService _skinManagerService = App.GetService<ISkinManagerService>();
    private readonly ModNotificationManager _modNotificationManager = App.GetService<ModNotificationManager>();
    private readonly GameBananaService _gameBananaService = App.GetService<GameBananaService>();

    private readonly Guid _notificationId;
    private readonly WindowEx _window;
    private ModNotification? _notification;
    private List<ModFileInfo> _modFiles = new();
    private readonly CancellationToken _ct;

    private ModPageInfo? _modPageInfo;

    [ObservableProperty] private string _modName = string.Empty;

    [ObservableProperty] private Uri _modPage = new("https://gamebanana.com/");

    [ObservableProperty] private Uri? _modPath = null;

    [ObservableProperty] private DateTime _lastUpdateCheck = DateTime.Now;

    [ObservableProperty] private bool _isOpenDownloadButtonEnabled = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyCanExecuteChangedFor(nameof(IgnoreAndCloseCommand), nameof(StartDownloadCommand))]
    private bool _isBusy = false;

    public bool IsNotBusy => !IsBusy;


    public ObservableCollection<ModFileInfoVm> ModFileInfos = new();
    private readonly ILogger _logger = App.GetService<ILogger>().ForContext<ModUpdateVM>();

    public ModUpdateVM(Guid notificationId, WindowEx window, CancellationToken ctsToken)
    {
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
        }
        catch (Exception e)
        {
            await LogErrorAndClose(e);
        }
    }


    private async Task InternalInitialize()
    {
        IsBusy = true;
        _notification =
            await _modNotificationManager.GetNotificationById(_notificationId);

        if (_notification?.ModsRetrievedResult is null)
        {
            await LogErrorAndClose(new InvalidOperationException("Failed to get mod page info, mod info is missing"));
            return;
        }

        var mod = _skinManagerService.GetModById(_notification.ModId);

        if (mod is null)
        {
            await LogErrorAndClose(new InvalidOperationException($"Mod with id {_notification.ModId} not found"));
            return;
        }

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
            ModFileInfos.Add(new ModFileInfoVm(modFile, InitializeModFileVmAsync)
            {
                IsNew = modFile.DateAdded > LastUpdateCheck,
                IsBusy = true,
                DownloadCommand = StartDownloadCommand
            });
        }

        IsBusy = false;
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


    private async void InitializeModFileVmAsync(ModFileInfoVm fileInfoVm)
    {
        ArgumentNullException.ThrowIfNull(fileInfoVm);

        var existingArchive = await _gameBananaCoreService.GetLocalModArchiveByMd5HashAsync(fileInfoVm.Md5Hash, _ct);

        if (existingArchive is not null)
        {
            fileInfoVm.Status = ModFileInfoVm.InstallStatus.Downloaded;
            fileInfoVm.DownloadProgress = 100;
        }

        fileInfoVm.IsBusy = false;
    }

    private bool CanStartDownload(ModFileInfoVm? fileInfoVm)
    {
        if (fileInfoVm is null)
            return false;
        return IsNotBusy && fileInfoVm.Status == ModFileInfoVm.InstallStatus.NotStarted;
    }

    [RelayCommand(CanExecute = nameof(CanStartDownload))]
    private async Task StartDownload(ModFileInfoVm fileInfoVm)
    {
        throw new NotImplementedException();
    }
}

public partial class ModFileInfoVm : ObservableObject
{
    private readonly ModFileInfo _modFileInfo;

    public string FileName => _modFileInfo.FileName;

    public DateTime DateAdded => _modFileInfo.DateAdded;

    public string Description => _modFileInfo.Description;

    public string Md5Hash => _modFileInfo.Md5Checksum;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    private InstallStatus _status = InstallStatus.NotStarted;

    [ObservableProperty] private int _downloadProgress;
    public IProgress<int> Progress { get; }


    public ModFileInfoVm(ModFileInfo modFileInfo, Action<ModFileInfoVm> initFunc)
    {
        _modFileInfo = modFileInfo;
        Progress = new Progress<int>(i => DownloadProgress = i);
        initFunc(this);
    }

    public required IAsyncRelayCommand DownloadCommand { get; init; }


    public enum InstallStatus
    {
        NotStarted,
        Downloading, // Downloading from GameBanana
        Downloaded, // Downloaded from GameBanana
        Installing, // Installing to the game, mod installation window open
        Installed // Installed to the game
    }
}