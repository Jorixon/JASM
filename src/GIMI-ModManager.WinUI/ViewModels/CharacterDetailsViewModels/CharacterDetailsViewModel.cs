using System.Collections.Concurrent;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Models.Settings;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;
using Microsoft.UI.Dispatching;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;

public partial class CharacterDetailsViewModel : ObservableObject, INavigationAware
{
    private readonly INavigationService _navigationService = App.GetService<INavigationService>();
    private readonly IGameService _gameService = App.GetService<IGameService>();
    private readonly ILogger _logger = App.GetService<ILogger>().ForContext<CharacterDetailsViewModel>();
    private readonly ISkinManagerService _skinManagerService = App.GetService<ISkinManagerService>();
    private readonly NotificationManager _notificationService = App.GetService<NotificationManager>();
    private readonly ILocalSettingsService _localSettingsService = App.GetService<ILocalSettingsService>();

    public Func<Task>? GridLoadedAwaiter { get; set; }

    private readonly CancellationTokenSource _navigationCancellationTokenSource = new();
    public CancellationToken CancellationToken;

    private bool IsReturning => CancellationToken.IsCancellationRequested || _isErrorNavigateBack;
    private bool _isErrorNavigateBack;
    private ICharacterModList _modList = null!;
    [ObservableProperty] private bool _isNavigationFinished;

    private readonly BusySetter _busySetter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSoftBusy), nameof(IsWorking))]
    private bool _isSoftBusy; // App is doing something, but the user can still do other things


    private bool _isHardBusy; // App is doing something, and the user can't do anything on the page
    public bool IsHardBusy
    {
        get => _isHardBusy;
        set
        {
            if (value == _isHardBusy) return;
            _isHardBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotHardBusy));
            OnPropertyChanged(nameof(IsWorking));
            OnPropertyChanged(nameof(CanChangeInGameSkins));
            NotifyCommands();
        }
    }

    public bool IsNotSoftBusy => !IsSoftBusy;
    public bool IsNotHardBusy => !IsHardBusy;

    public bool IsWorking => IsSoftBusy || IsHardBusy;


    public CharacterDetailsViewModel()
    {
        _busySetter = new BusySetter(this);
    }


    public ModGridVM ModGridVM { get; private set; } = App.GetService<ModGridVM>();
    public ModPaneVM ModPaneVM { get; private set; } = App.GetService<ModPaneVM>();

    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            await InitAsync(parameter, _busySetter.StartHardBusy()).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            ErrorNavigateBack(e);
            return;
        }
    }

    private async Task InitAsync(object parameter, BusySetter.CommandTracker commandTracker)
    {
#if DEBUG
        var stopwatch = new Stopwatch();
        stopwatch.Start();
#endif
        CancellationToken = _navigationCancellationTokenSource.Token;
        if (IsReturning)
            return;
        OnInitializingStarted?.Invoke(this, EventArgs.Empty);

        // Init character card
        InitCharacterCard(parameter);

        // Yield to UI, render character card, specifically the image
        await Task.Delay(100, CancellationToken);
        if (IsReturning)
            return;

        // Load mods
        await InitModGridAsync();
        if (IsReturning)
            return;

        // Yield to UI, render grid
        await Task.Delay(50, CancellationToken);
        commandTracker.Finish();
        if (IsReturning)
            return;

#if DEBUG
        stopwatch.Stop();
        Log.Logger.Information("Grid loading time {ElapsedMilliseconds} ms",
            stopwatch.ElapsedMilliseconds);
#endif

        // Init Mod Pane
        await InitModPaneAsync();
        if (IsReturning)
            return;

        await InitToolbarAsync();

        // Wait for the grid to load the datasource
        if (GridLoadedAwaiter is not null)
            await GridLoadedAwaiter();
        GridLoadedAwaiter = null;

        // Now that the grid is loaded, we can select the first mod
        AutoSelectFirstMod();

        // Finished initializing
        IsNavigationFinished = true;
        OnInitializingFinished?.Invoke(this, EventArgs.Empty);
    }

    private void AutoSelectFirstMod()
    {
        var modToSelect = ModGridVM.GridMods.FirstOrDefault(m => m.IsEnabled) ?? ModGridVM.GridMods.FirstOrDefault();

        if (modToSelect is null)
            return;

        ModGridVM.SetSelectedMod(modToSelect.Id);
    }

    private async Task SetSortOrder()
    {
        var settings = await ReadSettingsAsync();
        if (settings.SortByDescending == null || settings.SortingMethod == null)
            return;

        ModGridVM.SetModSorting(settings.SortingMethod, settings.SortByDescending.Value);
    }


    private async Task InitModGridAsync()
    {
        ModGridVM.BusySetter = _busySetter;
        ModGridVM.OnModsReloaded += OnModsReloaded;
        await SetSortOrder();
        await ModGridVM.InitializeAsync(CreateContext(), CancellationToken);
        ModGridVM.OnModsSelected += OnModsSelected;
        if (IsReturning)
            return;


        ModGridVM.IsBusy = false;
        OnModsLoaded?.Invoke(this, EventArgs.Empty);
    }

    private void OnModsReloaded(object? sender, EventArgs e) => UpdateTrackedMods();

    private void OnModsSelected(object? sender, ModGridVM.ModRowSelectedEventArgs args)
    {
        var selectedMod = args.Mods.FirstOrDefault();

        var queued = ModPaneVM.QueueLoadMod(selectedMod?.Id);

        if (selectedMod is null)
            return;

        // TODO: Handle notification
    }

    private async Task InitModPaneAsync()
    {
        // Init Mod Pane
        await ModPaneVM.OnNavigatedToAsync(DispatcherQueue.GetForCurrentThread(), CancellationToken);
        if (IsReturning)
            return;
    }

    public void OnNavigatedFrom()
    {
        _navigationCancellationTokenSource.Cancel();
        ModGridVM.OnModsSelected -= OnModsSelected;
        ModGridVM.OnNavigateFrom();
        ModPaneVM.OnNavigatedFrom();


        Task.Run(async () =>
        {
            var delay = Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);

            var settings = await ReadSettingsAsync().ConfigureAwait(false);
            settings.SortingMethod = ModGridVM.CurrentSortingMethod.SortingMethodType;
            settings.SortByDescending = ModGridVM.IsDescendingSort;
            await SaveSettingsAsync(settings).ConfigureAwait(false);
            await delay.ConfigureAwait(false);

            try
            {
                _navigationCancellationTokenSource?.Dispose();
            }
            catch (Exception e)
            {
                // ignored
            }
        }, CancellationToken.None);
    }


    private InternalName? ParseNavigationArg(object parameter)
    {
        return parameter switch
        {
            CharacterGridItemModel characterGridItemModel => characterGridItemModel.Character.InternalName,
            INameable iInternalName => iInternalName.InternalName,
            string internalNameString => new InternalName(internalNameString),
            InternalName internalName1 => internalName1,
            _ => null
        };
    }

    private void ErrorNavigateBack(Exception? exception = null)
    {
        if (_isErrorNavigateBack)
            return;
        _isErrorNavigateBack = true;

        Task.Run(async () =>
        {
            await Task.Delay(500);
            if (exception is not null)
                _notificationService.ShowNotification("An error occurred while loading the character details page.",
                    exception.Message, null);
            else
                _notificationService.ShowNotification("An error occurred while loading the character details page.", "",
                    null);
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (_navigationService.CanGoBack)
                    _navigationService.GoBack();
                else
                    _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!);
            });
        });
    }


    private Task<CharacterDetailsSettings> ReadSettingsAsync() =>
        _localSettingsService.ReadOrCreateSettingAsync<CharacterDetailsSettings>(CharacterDetailsSettings.Key,
            SettingScope.App);

    private Task SaveSettingsAsync(CharacterDetailsSettings settings) =>
        _localSettingsService.SaveSettingAsync(CharacterDetailsSettings.Key, settings, SettingScope.App);


    private async Task CommandWrapperAsync(bool startHardBusy, Func<Task> command)
    {
        using var _ = startHardBusy ? _busySetter.StartHardBusy() : _busySetter.StartSoftBusy();
        try
        {
            await command();
        }
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _notificationService.ShowNotification("An unknown error occured while executing the command", e.Message,
                TimeSpan.FromSeconds(5));
        }
    }

    private IRelayCommand[]? _viewModelCommands;
    private void NotifyCommands()
    {

        if (_viewModelCommands is null)
        {
            var commands = new List<IRelayCommand>();
            foreach (var propertyInfo in GetType()
                         .GetProperties()
                         .Where(p=> p.PropertyType.IsAssignableTo(typeof(IRelayCommand))))
            {
                var value = propertyInfo.GetValue(this);

                if (value is IRelayCommand relayCommand)
                    commands.Add(relayCommand);
            }

            _viewModelCommands = commands.ToArray();
        }
        _viewModelCommands.ForEach(c => c.NotifyCanExecuteChanged());
    }
}

public partial class BusySetter(CharacterDetailsViewModel viewModel) : ObservableObject
{
    private readonly CharacterDetailsViewModel _viewModel = viewModel;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSoftBusy), nameof(IsWorking))]
    private bool _isSoftBusy; // App is doing something, but the user can still do other things

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotHardBusy), nameof(IsWorking))]
    private bool _isHardBusy; // App is doing something, and the user can't do anything on the page

    public bool IsNotSoftBusy => !IsSoftBusy;
    public bool IsNotHardBusy => !IsHardBusy;

    public bool IsWorking => IsSoftBusy || IsHardBusy;

    public event EventHandler? SoftBusyChanged;
    public event EventHandler? HardBusyChanged;


    private readonly ConcurrentDictionary<Guid, byte> _trackedSoftCommands = [];
    private readonly ConcurrentDictionary<Guid, byte> _trackedHardCommands = [];

    private void Refresh()
    {
        var oldSoftBusy = IsSoftBusy;
        var oldHardBusy = IsHardBusy;

        IsSoftBusy = !_trackedSoftCommands.IsEmpty;
        IsHardBusy = !_trackedHardCommands.IsEmpty;
        _viewModel.IsHardBusy = IsHardBusy;
        _viewModel.IsSoftBusy = IsSoftBusy;

        if (oldSoftBusy != IsSoftBusy)
            SoftBusyChanged?.Invoke(this, EventArgs.Empty);

        if (oldHardBusy != IsHardBusy)
            HardBusyChanged?.Invoke(this, EventArgs.Empty);
    }

    public CommandTracker StartSoftBusy()
    {
        var tracker = new CommandTracker(this, false);
        _trackedSoftCommands.TryAdd(tracker.CommandId, 0);
        Refresh();
        return tracker;
    }

    public CommandTracker StartHardBusy()
    {
        var tracker = new CommandTracker(this, true);
        _trackedHardCommands.TryAdd(tracker.CommandId, 0);
        Refresh();
        return tracker;
    }


    public readonly struct CommandTracker(BusySetter busySetter, bool isHardBusy) : IDisposable
    {
        public readonly Guid CommandId = Guid.NewGuid();

        public void Dispose() => Finish();

        public void Finish()
        {
            if (isHardBusy)
                busySetter._trackedHardCommands.TryRemove(CommandId, out _);
            else
                busySetter._trackedSoftCommands.TryRemove(CommandId, out _);

            busySetter.Refresh();
        }
    }
}