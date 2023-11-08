using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.ModHandling;
using Microsoft.UI.Dispatching;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class DebugViewModel : ObservableRecipient, INavigationAware
{
    private readonly ILogger _logger;
    private readonly NotificationManager _notificationManager;
    private readonly ISkinManagerService _skinManagerService;
    private readonly ModCrawlerService _modCrawlerService;
    private readonly IModUpdateChecker _modUpdateChecker;
    private readonly ModUpdateAvailableChecker _modUpdateAvailableChecker;


    public DebugViewModel(ILogger logger, NotificationManager notificationManager,
        ISkinManagerService skinManagerService, ModCrawlerService modCrawlerService,
        IModUpdateChecker modUpdateChecker, ModUpdateAvailableChecker modUpdateAvailableChecker)
    {
        _logger = logger;
        _notificationManager = notificationManager;
        _skinManagerService = skinManagerService;
        _modCrawlerService = modCrawlerService;
        _modUpdateChecker = modUpdateChecker;
        _modUpdateAvailableChecker = modUpdateAvailableChecker;
    }

    [ObservableProperty] private string _url = string.Empty;

    [ObservableProperty] private int _modsToCheckCount = 0;
    [ObservableProperty] private int _modsCheckedCount = 0;
    [ObservableProperty] private bool _isChecking = false;


    [RelayCommand(IncludeCancelCommand = true)]
    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        var characterModLists = _skinManagerService.CharacterModLists;

        var mods = characterModLists.SelectMany(x => x.Mods);

        var modsWithUrls = new List<ModSettings>();
        foreach (var characterSkinEntry in mods)
        {
            var settings = await characterSkinEntry.Mod.Settings.ReadSettingsAsync();

            if (settings.ModUrl is not null)
                modsWithUrls.Add(settings);
        }

        ModsToCheckCount = modsWithUrls.Count;
        Results.Clear();

        var tasks = new List<Task<ModsRetrievedResult>>();
        //var concurrentQueue = new ConcurrentQueue<UpdateCheckResult>();

        IsChecking = true;
        foreach (var mod in modsWithUrls)
        {
            if (mod.ModUrl is null)
                continue;

            var fetchTask = Task.Run(() => _modUpdateChecker.CheckForUpdatesAsync(
                mod.ModUrl,
                DateTime.MinValue, cancellationToken), cancellationToken);
            tasks.Add(fetchTask);
        }

        while (tasks.Any() && !cancellationToken.IsCancellationRequested)
        {
            var finishedTask = await Task.WhenAny(tasks);
            tasks.Remove(finishedTask);
            try
            {
                var result = await finishedTask;

                foreach (var updateCheckResult in result.Mods) Results.Insert(0, updateCheckResult);

                ModsCheckedCount++;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to check for updates");
            }
        }


        IsChecking = false;
    }

    [RelayCommand]
    private void CheckNow()
    {
        var lumineMods = _skinManagerService.GetCharacterModList(new InternalName("traveler (female)")).Mods
            .Select(ske => ske.Mod.Id);
        _modUpdateAvailableChecker.CheckNow(true);
    }

    public ObservableCollection<UpdateCheckResult> Results { get; } = new();

    [RelayCommand]
    private async Task TestAsync()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();

        var test = Task.Run(async () =>
        {
            await Task.Delay(200);
            var dispatcher3 = DispatcherQueue.GetForCurrentThread();

            return 123;
        });
        await Test2Async();

        await test;
        var dispatcher2 = DispatcherQueue.GetForCurrentThread();
    }

    private async Task Test2Async()
    {
        var before = DispatcherQueue.GetForCurrentThread();

        var stopper = Task.Delay(2000).ConfigureAwait(false);
        var after = DispatcherQueue.GetForCurrentThread();

        await using var file = File.Create("test");

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        await stopper;
        await using var writer = new StreamWriter(file);
        await writer.WriteLineAsync("test").ConfigureAwait(true);

        var dispatcher5 = DispatcherQueue.GetForCurrentThread();

        await writer.FlushAsync().ConfigureAwait(false);

        var afterwrite = DispatcherQueue.GetForCurrentThread();

        var test = await Task.Run(async () =>
        {
            await Task.Delay(200);
            var dispatcher3 = DispatcherQueue.GetForCurrentThread();

            return 123;
        }).ConfigureAwait(false);
        var dispatcher2 = DispatcherQueue.GetForCurrentThread();
        await Task.Delay(2000).ConfigureAwait(false);
    }

    public void OnNavigatedTo(object parameter)
    {
    }

    public void OnNavigatedFrom()
    {
        Task.Run(() => { CheckCancelCommand?.Execute(null); });
    }
}