using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Windows.Graphics;
using CommunityToolkit.WinUI;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.WinUI.Activation;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Models.Options;
using GIMI_ModManager.WinUI.Models.Settings;
using GIMI_ModManager.WinUI.Services.AppManagment;
using GIMI_ModManager.WinUI.Services.AppManagment.Updating;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GIMI_ModManager.WinUI.Services;

public class ActivationService : IActivationService
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ILogger _logger;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IGameService _gameService;
    private readonly ILanguageLocalizer _languageLocalizer;
    private readonly ElevatorService _elevatorService;
    private readonly GenshinProcessManager _genshinProcessManager;
    private readonly ThreeDMigtoProcessManager _threeDMigtoProcessManager;
    private readonly UpdateChecker _updateChecker;
    private readonly IWindowManagerService _windowManagerService;
    private readonly AutoUpdaterService _autoUpdaterService;
    private readonly SelectedGameService _selectedGameService;
    private readonly ModUpdateAvailableChecker _modUpdateAvailableChecker;
    private readonly ModNotificationManager _modNotificationManager;
    private UIElement? _shell = null;

    private readonly bool IsMsix = RuntimeHelper.IsMSIX;


    public ActivationService(ActivationHandler<LaunchActivatedEventArgs> defaultHandler,
        IEnumerable<IActivationHandler> activationHandlers, IThemeSelectorService themeSelectorService,
        ILocalSettingsService localSettingsService,
        ElevatorService elevatorService, GenshinProcessManager genshinProcessManager,
        ThreeDMigtoProcessManager threeDMigtoProcessManager, UpdateChecker updateChecker,
        IWindowManagerService windowManagerService, AutoUpdaterService autoUpdaterService, IGameService gameService,
        ILanguageLocalizer languageLocalizer, SelectedGameService selectedGameService,
        ModUpdateAvailableChecker modUpdateAvailableChecker, ILogger logger,
        ModNotificationManager modNotificationManager)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
        _localSettingsService = localSettingsService;
        _elevatorService = elevatorService;
        _genshinProcessManager = genshinProcessManager;
        _threeDMigtoProcessManager = threeDMigtoProcessManager;
        _updateChecker = updateChecker;
        _windowManagerService = windowManagerService;
        _autoUpdaterService = autoUpdaterService;
        _gameService = gameService;
        _languageLocalizer = languageLocalizer;
        _selectedGameService = selectedGameService;
        _modUpdateAvailableChecker = modUpdateAvailableChecker;
        _modNotificationManager = modNotificationManager;
        _logger = logger.ForContext<ActivationService>();
    }

    public async Task ActivateAsync(object activationArgs)
    {
#if DEBUG
        _logger.Information("JASM starting up in DEBUG mode...");
#elif RELEASE
        _logger.Information("JASM starting up in RELEASE mode...");
#endif
        // Check if there is another instance of JASM running
        await CheckIfAlreadyRunningAsync();

        // Execute tasks before activation.
        await InitializeAsync();

        // Set the MainWindow Content.
        if (App.MainWindow.Content == null)
        {
            _shell = App.GetService<ShellPage>();
            App.MainWindow.Content = _shell ?? new Frame();
        }

        // Handle activation via ActivationHandlers.
        await HandleActivationAsync(activationArgs);

        // Activate the MainWindow.
        App.MainWindow.Activate();

        // Set MainWindow Cleanup on Close.
        App.MainWindow.Closed += OnApplicationExit;

        // Execute tasks after activation.
        await StartupAsync();

        // Show admin warning if running as admin.
        AdminWarningPopup();
    }

    private async Task HandleActivationAsync(object activationArgs)
    {
        var activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));


        if (activationHandler is not null)
        {
            _logger.Debug("Handling activation: {ActivationName}",
                activationHandler?.ActivationName);

            await activationHandler?.HandleAsync(activationArgs)!;
        }

        if (_defaultHandler.CanHandle(activationArgs))
        {
            _logger.Debug("Handling activation: {ActivationName}", _defaultHandler.ActivationName);
            await _defaultHandler.HandleAsync(activationArgs);
        }
    }

    private async Task InitializeAsync()
    {
        await _selectedGameService.InitializeAsync();
        await SetLanguage();
        await SetWindowSettings();
        await _themeSelectorService.InitializeAsync().ConfigureAwait(false);
    }


    private async Task StartupAsync()
    {
        await _themeSelectorService.SetRequestedThemeAsync();
        await InitCharacterOverviewSettings();
        await _genshinProcessManager.TryInitialize();
        await _threeDMigtoProcessManager.TryInitialize();
        await _updateChecker.InitializeAsync();
        await _modUpdateAvailableChecker.InitializeAsync().ConfigureAwait(false);
        await Task.Run(() => _autoUpdaterService.UpdateAutoUpdater()).ConfigureAwait(false);
    }

    const int MinimizedPosition = -32000;
    private async Task SetWindowSettings()
    {
        var screenSize = await _localSettingsService.ReadSettingAsync<ScreenSizeSettings>(ScreenSizeSettings.Key);
        if (screenSize != null)
        {
            _logger.Debug($"Window size loaded: {screenSize.Width}x{screenSize.Height}");
            App.MainWindow.SetWindowSize(screenSize.Width, screenSize.Height);

            if (screenSize.XPosition != 0 && screenSize.YPosition != 0 &&
                screenSize.XPosition != MinimizedPosition && screenSize.YPosition != MinimizedPosition)
                App.MainWindow.AppWindow.Move(new PointInt32(screenSize.XPosition, screenSize.YPosition));
            else
                App.MainWindow.CenterOnScreen();

            if (screenSize.IsFullScreen)
                App.MainWindow.Maximize();
        }
    }

    private async Task InitCharacterOverviewSettings()
    {
        var characterOverviewSettings =
            await _localSettingsService.ReadSettingAsync<CharacterOverviewSettings>(CharacterOverviewSettings.Key);
        if (characterOverviewSettings == null)
            await _localSettingsService.SaveSettingAsync(CharacterOverviewSettings.Key,
                new CharacterOverviewSettings());
    }

// To allow jasm to save settings before exiting, we need to handle the first close event.
// Once finished saving the handler calls itself again, but this time it will exit.
    private bool _isExiting;

    private async void OnApplicationExit(object sender, WindowEventArgs args)
    {
        if (App.OverrideShutdown)
        {
            _logger.Information("Shutdown override enabled, skipping shutdown...");
            _logger.Information("Shutdown override will be disabled in at most 1 second.");
            Task.Run(async () =>
            {
                await Task.Delay(500);
                App.OverrideShutdown = false;
                _logger.Information("Shutdown override disabled.");
            });
            args.Handled = true;
            return;
        }

        if (_isExiting)
            return;

        try
        {
            args.Handled = true;

            var notificationCleanup = Task.Run(_modNotificationManager.CleanupAsync).ConfigureAwait(false);
            var saveWindowSettingsTask = SaveWindowSettingsAsync().ConfigureAwait(false);

            _logger.Debug("JASM shutting down...");
            _modUpdateAvailableChecker.CancelAndStop();
            _updateChecker.CancelAndStop();


            await _windowManagerService.CloseWindowsAsync().ConfigureAwait(false);

            var tmpDir = new DirectoryInfo(App.TMP_DIR);
            if (tmpDir.Exists)
            {
                _logger.Debug("Deleting temporary directory: {Path}", tmpDir.FullName);
                tmpDir.Delete(true);
            }

            await saveWindowSettingsTask;
            await notificationCleanup;
            _logger.Debug("JASM shutdown complete.");
        }
        finally
        {
            // Call the handler again, this time it will exit.
            await Task.Run(() =>
            {
                _isExiting = true;
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    Application.Current.Exit();
                    App.MainWindow.Close();
                });
            }).ConfigureAwait(false);
        }
    }

    private async Task SaveWindowSettingsAsync()
    {
        if (App.MainWindow is null || App.MainWindow.AppWindow is null)
            return;


        var windowSettings = await _localSettingsService
            .ReadOrCreateSettingAsync<ScreenSizeSettings>(ScreenSizeSettings.Key);


        var isFullScreen = App.MainWindow.WindowState == WindowState.Maximized;

        var width = windowSettings.Width;
        var height = windowSettings.Height;
        var xPosition = windowSettings.XPosition;
        var yPosition = windowSettings.YPosition;


        if (!isFullScreen && App.MainWindow.WindowState != WindowState.Minimized)
        {
            width = App.MainWindow.AppWindow.Size.Width;
            height = App.MainWindow.AppWindow.Size.Height;
        }

        if (App.MainWindow.WindowState != WindowState.Minimized)
        {
            xPosition = App.MainWindow.AppWindow.Position.X;
            yPosition = App.MainWindow.AppWindow.Position.Y;
        }

        _logger.Debug($"Saving Window size: {width}x{height} | IsFullscreen: {isFullScreen}");

        var newWindowSettings = new ScreenSizeSettings(width, height)
            { IsFullScreen = isFullScreen, XPosition = xPosition, YPosition = yPosition };

        await _localSettingsService.SaveSettingAsync(ScreenSizeSettings.Key, newWindowSettings).ConfigureAwait(false);
    }

    // Declared here for now, might move to a different class later.
    private const string IgnoreAdminWarningKey = "IgnoreAdminPrivelegesWarning";

    private void AdminWarningPopup()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);

        if (!principal.IsInRole(WindowsBuiltInRole.Administrator)) return;

        App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            await Task.Delay(1000);

            var ignoreWarning = await _localSettingsService.ReadSettingAsync<bool>(IgnoreAdminWarningKey);

            if (ignoreWarning) return;

            var stackPanel = new StackPanel();
            var textWarning = new TextBlock()
            {
                Text = "You are running JASM as an administrator. This is not recommended.\n" +
                       "JASM was NOT designed to run with administrator privileges.\n" +
                       "Simple bugs, though unlikely, can potentially cause serious damage to your file system.\n\n" +
                       "Please consider running JASM without administrator privileges.\n\n" +
                       "Use at your own risk, you have been warned",
                TextWrapping = TextWrapping.WrapWholeWords
            };
            stackPanel.Children.Add(textWarning);

            var doNotShowAgain = new CheckBox()
            {
                IsChecked = false,
                Content = "Do not show this warning again",
                Margin = new Thickness(0, 10, 0, 0)
            };

            stackPanel.Children.Add(doNotShowAgain);


            var dialog = new ContentDialog
            {
                Title = "Running as Administrator Warning",
                Content = stackPanel,
                PrimaryButtonText = "I understand",
                SecondaryButtonText = "Exit",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await _windowManagerService.ShowDialogAsync(dialog);

            if (result == ContentDialogResult.Secondary) Application.Current.Exit();

            if (doNotShowAgain.IsChecked == true)
                await _localSettingsService.SaveSettingAsync(IgnoreAdminWarningKey, true);
        });
    }


    private async Task CheckIfAlreadyRunningAsync()
    {
        var currentProcess = Process.GetCurrentProcess();
        var processes = Process.GetProcessesByName(currentProcess.ProcessName);

        if (processes.Length <= 1) return;

        var currentProcessId = currentProcess.Id;
        var currentProcessName = currentProcess.MainModule?.ModuleName;

        foreach (var process in processes)
        {
            if (process.Id == currentProcessId) continue;

            var processName = process.MainModule?.ModuleName;


            if (currentProcessName!.Equals(processName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Information("JASM is already running, exiting...");
                var hWnd = process.MainWindowHandle;

                Win32.ShowWindowAsync(new HandleRef(null, hWnd), Win32.SW_RESTORE);
                Win32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOZORDER);
                Win32.SetForegroundWindow(hWnd);


                Application.Current.Exit();
                await Task.Delay(-1);
            }
        }
    }

    private async Task SetLanguage()
    {
        var selectedLanguage = (await _localSettingsService.ReadOrCreateSettingAsync<AppSettings>(AppSettings.Key))
            .Language?.ToLower().Trim();
        if (selectedLanguage == null)
        {
            return;
        }

        var supportedLanguages = _languageLocalizer.AvailableLanguages;
        var language = supportedLanguages.FirstOrDefault(lang =>
            lang.LanguageCode.Equals(selectedLanguage, StringComparison.CurrentCultureIgnoreCase));

        if (language != null)
            await _languageLocalizer.SetLanguageAsync(language);
    }
}