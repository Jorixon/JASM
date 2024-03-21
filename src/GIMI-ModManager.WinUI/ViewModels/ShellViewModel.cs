using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.AppManagement.Updating;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.Views;
using Microsoft.UI.Xaml.Navigation;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class ShellViewModel : ObservableRecipient
{
    private readonly UpdateChecker _updateChecker;
    public readonly SelectedGameService SelectedGameService;
    [ObservableProperty] private bool isBackEnabled;
    [ObservableProperty] private bool isNotFirstTimeStartupPage = true;
    [ObservableProperty] private object? selected;
    [ObservableProperty] private int settingsInfoBadgeOpacity = 0;
    public readonly IGameService GameService;

    public INavigationService NavigationService { get; }
    public INavigationViewService NavigationViewService { get; }
    public NotificationManager NotificationManager { get; }
    public ElevatorService ElevatorService { get; }

    public ShellViewModel(INavigationService navigationService, INavigationViewService navigationViewService,
        NotificationManager notificationManager, ElevatorService elevatorService, UpdateChecker updateChecker,
        IGameService gameService, SelectedGameService selectedGameService)
    {
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
        NavigationViewService = navigationViewService;
        NotificationManager = notificationManager;
        ElevatorService = elevatorService;
        _updateChecker = updateChecker;
        GameService = gameService;
        SelectedGameService = selectedGameService;
        _updateChecker.NewVersionAvailable += OnNewVersionAvailable;
    }

    public event EventHandler<bool>? ShowSettingsInfoBadge;

    private void OnNewVersionAvailable(object? sender, UpdateChecker.NewVersionEventArgs e)
    {
        if (_updateChecker.IgnoredVersion == e.Version)
            return;


        var show = e.Version != new Version();
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            SettingsInfoBadgeOpacity = show ? 1 : 0;
            ShowSettingsInfoBadge?.Invoke(this, show);
        });
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;
        if (e.SourcePageType == typeof(StartupPage) || e.SourcePageType == typeof(StartupViewModel))
            IsNotFirstTimeStartupPage = false; // On the StartupPage => Hide the MenuBar
        else
            IsNotFirstTimeStartupPage = true; // Not on the StartupPage => Show the MenuBar

        if (e.SourcePageType == typeof(SettingsPage))
        {
            Selected = NavigationViewService.SettingsItem;
            return;
        }

        var selectedItem = NavigationViewService.GetSelectedItem(e.SourcePageType, e.Parameter);
        if (selectedItem != null)
        {
            Selected = selectedItem;
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotFirstTimeStartupPage))]
    private void OnMenuSettings()
    {
        if (NavigationService.Frame!.Content is SettingsPage)
        {
            NavigationService.NavigateTo(typeof(CharactersViewModel).FullName!);
            return;
        }

        NavigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
    }

    public async Task RefreshGenshinMods()
    {
        if (!IsNotFirstTimeStartupPage)
        {
            return;
        }

        if (ElevatorService.ElevatorStatus == ElevatorStatus.NotRunning)
        {
            NotificationManager.ShowNotification("Elevator is not running",
                "Please start the Elevator first in the Settings page",
                TimeSpan.FromSeconds(5));
        }
        else
            await Task.Run(async () => await ElevatorService.RefreshGenshinMods());
    }
}