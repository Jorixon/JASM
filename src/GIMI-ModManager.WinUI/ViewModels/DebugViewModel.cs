using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.ModHandling;
using Serilog;
using NotificationManager = GIMI_ModManager.WinUI.Services.Notifications.NotificationManager;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class DebugViewModel : ObservableRecipient, INavigationAware
{
    private readonly ILogger _logger;
    private readonly NotificationManager _notificationManager;
    private readonly ISkinManagerService _skinManagerService;
    private readonly ModCrawlerService _modCrawlerService;
    private readonly IModUpdateChecker _modUpdateChecker;
    private readonly ModUpdateAvailableChecker _modUpdateAvailableChecker;
    private readonly ImageHandlerService _imageHandlerService;
    private readonly GameBananaService _gameBananaService;
    private readonly IWindowManagerService _windowManagerService;


    public DebugViewModel(ILogger logger, NotificationManager notificationManager,
        ISkinManagerService skinManagerService, ModCrawlerService modCrawlerService,
        IModUpdateChecker modUpdateChecker, ModUpdateAvailableChecker modUpdateAvailableChecker,
        ImageHandlerService imageHandlerService, GameBananaService gameBananaService,
        IWindowManagerService windowManagerService)
    {
        _logger = logger;
        _notificationManager = notificationManager;
        _skinManagerService = skinManagerService;
        _modCrawlerService = modCrawlerService;
        _modUpdateChecker = modUpdateChecker;
        _modUpdateAvailableChecker = modUpdateAvailableChecker;
        _imageHandlerService = imageHandlerService;
        _gameBananaService = gameBananaService;
        _windowManagerService = windowManagerService;
    }

    public void OnNavigatedTo(object parameter)
    {
    }

    public void OnNavigatedFrom()
    {
    }
}