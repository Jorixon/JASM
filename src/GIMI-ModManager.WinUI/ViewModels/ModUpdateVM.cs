using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
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
    private readonly GameBananaService _gameBananaService = App.GetService<GameBananaService>();
    private readonly ISkinManagerService _skinManagerService = App.GetService<ISkinManagerService>();
    private readonly ModNotificationManager _modNotificationManager = App.GetService<ModNotificationManager>();

    private readonly Guid _notificationId;
    private readonly WindowEx _window;
    private ModNotification? _notification;

    [ObservableProperty] private string _modName = string.Empty;

    [ObservableProperty] private Uri _modPage = new("https://gamebanana.com/");

    [ObservableProperty] private Uri? _modPath = null;

    [ObservableProperty] private DateTime _lastUpdateCheck = DateTime.Now;

    [ObservableProperty] private bool _isOpenDownloadButtonEnabled = false;


    public ObservableCollection<UpdateCheckResult> Results = new();
    private readonly ILogger _logger = App.GetService<ILogger>().ForContext<ModUpdateVM>();

    public ModUpdateVM(Guid notificationId, WindowEx window)
    {
        _notificationId = notificationId;
        _window = window;
        Initialize();
    }


    private async void Initialize()
    {
        ModsRetrievedResult? modResult = null;
        try
        {
            _notification =
                await _modNotificationManager.GetNotificationById(_notificationId) ??
                throw new InvalidOperationException();
            modResult = _notification.ModsRetrievedResult ??
                        await _gameBananaService.GetAvailableMods(_notification.ModId);
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

        var modSettings = mod.Settings.GetSettings().TryPickT0(out var settings, out _) ? settings : null;

        if (modSettings is null)
        {
            LogErrorAndClose(new ModSettingsNotFoundException($"Mod settings not found for mod {mod.FullPath}"));
            return;
        }

        ModName = modSettings.CustomName ?? mod.Name;
        ModPage = modResult.SitePageUrl;
        ModPath = new Uri(mod.FullPath);
        LastUpdateCheck = modResult.LastCheck;

        modResult.Mods.ForEach(x => Results.Add(x));
    }

    private void LogErrorAndClose(Exception e)
    {
        _logger.Error(e, "Failed to get mod update info");
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            App.GetService<NotificationManager>().ShowNotification("Failed to get mod update info",
                $"Failed to get mod update info", TimeSpan.FromSeconds(10));
        });
        _window.Close();
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
}