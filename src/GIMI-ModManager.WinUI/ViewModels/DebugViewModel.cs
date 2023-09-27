using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Services;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class DebugViewModel : ObservableRecipient, INavigationAware
{
    private readonly ILogger _logger;
    private readonly NotificationManager _notificationManager;
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGenshinService _genshinService;
    private readonly ModCrawlerService _modCrawlerService;
    private readonly IModUpdateCheckerService _modUpdateCheckerService;


    public DebugViewModel(ILogger logger, NotificationManager notificationManager,
        ISkinManagerService skinManagerService, IGenshinService genshinService, ModCrawlerService modCrawlerService,
        IModUpdateCheckerService modUpdateCheckerService)
    {
        _logger = logger;
        _notificationManager = notificationManager;
        _skinManagerService = skinManagerService;
        _genshinService = genshinService;
        _modCrawlerService = modCrawlerService;
        _modUpdateCheckerService = modUpdateCheckerService;
    }

    [ObservableProperty] private string _url = string.Empty;

    [RelayCommand]
    private async Task CheckAsync()
    {
        ModsRetrievedResult result;
        try
        {
            result =
                await _modUpdateCheckerService.CheckForUpdatesAsync(Url, DateTime.MinValue, CancellationToken.None);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to check for updates");
            return;
        }

        _logger.Information("Result: {result}", result);

        Results.Clear();
        foreach (var updateCheckResult in result.Mods) Results.Add(updateCheckResult);
    }

    public ObservableCollection<UpdateCheckResult> Results { get; } = new();


    public void OnNavigatedTo(object parameter)
    {
    }

    public void OnNavigatedFrom()
    {
    }
}