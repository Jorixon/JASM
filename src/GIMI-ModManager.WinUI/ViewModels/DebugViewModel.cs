using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Services;
using Microsoft.UI.Dispatching;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class DebugViewModel : ObservableRecipient, INavigationAware
{
    private readonly ILogger _logger;
    private readonly NotificationManager _notificationManager;
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGenshinService _genshinService;
    private readonly ModCrawlerService _modCrawlerService;
    private readonly IModUpdateChecker _modUpdateChecker;


    public DebugViewModel(ILogger logger, NotificationManager notificationManager,
        ISkinManagerService skinManagerService, IGenshinService genshinService, ModCrawlerService modCrawlerService,
        IModUpdateChecker modUpdateChecker)
    {
        _logger = logger;
        _notificationManager = notificationManager;
        _skinManagerService = skinManagerService;
        _genshinService = genshinService;
        _modCrawlerService = modCrawlerService;
        _modUpdateChecker = modUpdateChecker;
    }

    [ObservableProperty] private string _url = string.Empty;

    [ObservableProperty] private int _modsToCheckCount = 0;
    [ObservableProperty] private int _modsCheckedCount = 0;
    [ObservableProperty] private bool _isChecking = false;

    [RelayCommand]
    private async Task CheckAsync()
    {
        var characterModLists = _skinManagerService.CharacterModLists;

        var mods = characterModLists.SelectMany(x => x.Mods);

        var modsWithUrls = new List<ISkinMod>();
        foreach (var characterSkinEntry in mods)
        {
            await characterSkinEntry.Mod.ReadSkinModSettings();
            if (string.IsNullOrWhiteSpace(characterSkinEntry.Mod?.CachedSkinModSettings?.ModUrl)) continue;

            modsWithUrls.Add(characterSkinEntry.Mod);
        }

        ModsToCheckCount = modsWithUrls.Count;
        Results.Clear();

        var tasks = new List<Task>();
        //var concurrentQueue = new ConcurrentQueue<UpdateCheckResult>();
        try
        {
            IsChecking = true;
            foreach (var mod in modsWithUrls)
            {
                var fetchTask = Task.Run(async () =>
                {
                    var result = await _modUpdateChecker.CheckForUpdatesAsync(
                        new Uri(mod.CachedSkinModSettings!.ModUrl!),
                        DateTime.MinValue, CancellationToken.None);

                    //foreach (var updateCheckResult in result.Mods)
                    //    concurrentQueue.Enqueue(updateCheckResult);

                    App.MainWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                    {
                        foreach (var updateCheckResult in result.Mods) Results.Insert(0, updateCheckResult);

                        ModsCheckedCount++;
                    });
                });
                tasks.Add(fetchTask);
            }

            await Task.WhenAll(tasks);
        }
        catch (HttpRequestException e)
        {
            _logger.Error(e, "Failed to check for updates");
            return;
        }

        IsChecking = false;
    }

    public ObservableCollection<UpdateCheckResult> Results { get; } = new();


    public void OnNavigatedTo(object parameter)
    {
    }

    public void OnNavigatedFrom()
    {
    }
}