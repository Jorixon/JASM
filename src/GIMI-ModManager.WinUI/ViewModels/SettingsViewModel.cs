using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Helpers;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using GIMI_ModManager.WinUI.Validators.PreConfigured;
using Windows.ApplicationModel.Core;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Models.Options;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class SettingsViewModel : ObservableRecipient
{
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ILogger _logger;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly INavigationViewService _navigationViewService;
    private readonly IWindowManagerService _windowManagerService;

    private readonly NotificationManager _notificationManager;
    private readonly UpdateChecker _updateChecker;

    [ObservableProperty] private ElementTheme _elementTheme;

    [ObservableProperty] private string _versionDescription;

    [ObservableProperty] private string _latestVersion = string.Empty;
    [ObservableProperty] private bool _showNewVersionAvailable = false;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(IgnoreNewVersionCommand))]
    private bool _CanIgnoreUpdate = false;


    public PathPicker PathToGIMIFolderPicker { get; }
    public PathPicker PathToModsFolderPicker { get; }

    public ElevatorService ElevatorService;

    private static bool _showElevatorStartDialog = true;

    private ModManagerOptions? _modManagerOptions = null!;
    private readonly ISkinManagerService _skinManagerService;

    public SettingsViewModel(IThemeSelectorService themeSelectorService, ILocalSettingsService localSettingsService,
        ElevatorService elevatorService, ILogger logger, NotificationManager notificationManager,
        INavigationViewService navigationViewService, IWindowManagerService windowManagerService,
        ISkinManagerService skinManagerService, UpdateChecker updateChecker)
    {
        _themeSelectorService = themeSelectorService;
        _localSettingsService = localSettingsService;
        ElevatorService = elevatorService;
        _notificationManager = notificationManager;
        _navigationViewService = navigationViewService;
        _windowManagerService = windowManagerService;
        _skinManagerService = skinManagerService;
        _updateChecker = updateChecker;
        _logger = logger.ForContext<SettingsViewModel>();
        _elementTheme = _themeSelectorService.Theme;
        _versionDescription = GetVersionDescription();

        _updateChecker.NewVersionAvailable += UpdateCheckerOnNewVersionAvailable;

        if (_updateChecker.LatestRetrievedVersion is not null &&
            _updateChecker.LatestRetrievedVersion != _updateChecker.CurrentVersion)
        {
            LatestVersion = VersionFormatter(_updateChecker.LatestRetrievedVersion);
            ShowNewVersionAvailable = true;
            if (_updateChecker.LatestRetrievedVersion != _updateChecker.IgnoredVersion)
                CanIgnoreUpdate = true;
        }


        _modManagerOptions = localSettingsService.ReadSetting<ModManagerOptions>(ModManagerOptions.Section);
        PathToGIMIFolderPicker = new PathPicker(GimiFolderRootValidators.Validators);
        PathToModsFolderPicker = new PathPicker(ModsFolderValidator.Validators);

        PathToGIMIFolderPicker.Path = _modManagerOptions?.GimiRootFolderPath;
        PathToModsFolderPicker.Path = _modManagerOptions?.ModsFolderPath;


        PathToGIMIFolderPicker.IsValidChanged += (sender, args) => SaveSettingsCommand.NotifyCanExecuteChanged();
        PathToModsFolderPicker.IsValidChanged +=
            (sender, args) => SaveSettingsCommand.NotifyCanExecuteChanged();


        PathToGIMIFolderPicker.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(PathPicker.Path))
                SaveSettingsCommand.NotifyCanExecuteChanged();
        };

        PathToModsFolderPicker.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(PathPicker.Path))
                SaveSettingsCommand.NotifyCanExecuteChanged();
        };
        ElevatorService.CheckStatus();
    }


    [RelayCommand]
    private async Task SwitchThemeAsync(ElementTheme param)
    {
        if (ElementTheme != param)
        {
            var result = await _windowManagerService.ShowDialogAsync(new ContentDialog()
            {
                Title = "Restart required",
                Content = new TextBlock()
                {
                    Text =
                        "You'll need to restart the application for the theme to take effect or else the application will become unstable. " +
                        "This is most likely me not configuring the theming correctly. Dark Mode is the recommended theme.\n\n" +
                        "Sorry for the inconvenience.",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = "Restart",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            });

            if (result != ContentDialogResult.Primary) return;

            ElementTheme = param;
            await _themeSelectorService.SetThemeAsync(param);
            _notificationManager.ShowNotification("Restarting...", "The application will restart now.",
                null);
            await RestartApp();
        }
    }

    private static string GetVersionDescription()
    {
        Version version;

        if (RuntimeHelper.IsMSIX)
        {
            var packageVersion = Package.Current.Id.Version;

            version = new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        }

        return
            $"{"AppDisplayName".GetLocalized()} - {VersionFormatter(version)}";
    }


    private bool ValidFolderSettings() => PathToGIMIFolderPicker.IsValid && PathToModsFolderPicker.IsValid &&
                                          PathToGIMIFolderPicker.Path != PathToModsFolderPicker.Path &&
                                          (PathToGIMIFolderPicker.Path != _modManagerOptions?.GimiRootFolderPath ||
                                           PathToModsFolderPicker.Path != _modManagerOptions?.ModsFolderPath);


    [RelayCommand(CanExecute = nameof(ValidFolderSettings))]
    private async Task SaveSettings()
    {
        var dialog = new ContentDialog();
        dialog.XamlRoot = App.MainWindow.Content.XamlRoot;
        dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        dialog.Title = "Update Folder Paths?";
        dialog.CloseButtonText = "Cancel";
        dialog.PrimaryButtonText = "Save";
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.Content = "Do you want to save the new folder paths? The App will restart afterwards.";

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var modManagerOptions = await _localSettingsService.ReadSettingAsync<ModManagerOptions>(
                ModManagerOptions.Section) ?? new ModManagerOptions();

            modManagerOptions.GimiRootFolderPath = PathToGIMIFolderPicker.Path;
            modManagerOptions.ModsFolderPath = PathToModsFolderPicker.Path;

            await _localSettingsService.SaveSettingAsync(ModManagerOptions.Section,
                modManagerOptions);
            _logger.Information("Saved startup settings: {@ModManagerOptions}", modManagerOptions);
            _notificationManager.ShowNotification("Settings saved. Restarting App...", "", TimeSpan.FromSeconds(2));


            await RestartApp();
        }
    }

    [RelayCommand]
    private async Task BrowseGimiFolderAsync()
    {
        await PathToGIMIFolderPicker.BrowseFolderPathAsync(App.MainWindow);
        if (PathToGIMIFolderPicker.PathHasValue &&
            !PathToModsFolderPicker.PathHasValue)
            PathToModsFolderPicker.Path = Path.Combine(PathToGIMIFolderPicker.Path!, "Mods");
    }


    [RelayCommand]
    private async Task BrowseModsFolderAsync()
        => await PathToModsFolderPicker.BrowseFolderPathAsync(App.MainWindow);

    [RelayCommand]
    private async Task ReorganizeModsAsync()
    {
        var result = await _windowManagerService.ShowDialogAsync(new ContentDialog()
        {
            Title = "Reorganize Mods?",
            Content = new TextBlock()
            {
                Text =
                    "Do you want to reorganize the Mods folder?\n" +
                    "This will prompt the application to sort existing mods that are directly in the Mods folder and Others folder, into folders assigned to their respective characters.\n\n" +
                    "Any mods that can't be reasonably matched will be placed in an 'Others' folder. While the mods already in 'Others' folder will remain there.",
                TextWrapping = TextWrapping.WrapWholeWords,
                IsTextSelectionEnabled = true
            },
            PrimaryButtonText = "Yes",
            DefaultButton = ContentDialogButton.Primary,
            CloseButtonText = "Cancel",
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        });

        if (result == ContentDialogResult.Primary)
        {
            _navigationViewService.IsEnabled = false;
            var genshinService = App.GetService<IGenshinService>();

            try
            {
                var movedModsCount = await Task.Run(() =>
                    _skinManagerService.ReorganizeMods()); // Mods folder

                movedModsCount += await Task.Run(() =>
                    _skinManagerService.ReorganizeMods(
                        genshinService.GetCharacter(genshinService.OtherCharacterId))); // Others folder

                if (movedModsCount == -1)
                    _notificationManager.ShowNotification("Mods reorganization failed.",
                        "See logs for more details.", TimeSpan.FromSeconds(5));

                else
                    _notificationManager.ShowNotification("Mods reorganized.",
                        $"Moved {movedModsCount} mods to character folders", TimeSpan.FromSeconds(5));
            }
            finally
            {
                _navigationViewService.IsEnabled = true;
            }
        }
    }


    private async Task RestartApp()
    {
        _navigationViewService.IsEnabled = false;

        if (RuntimeHelper.IsMSIX)
        {
            _logger.Information("Restarting in MSIX mode not supported. Shutting down...");
            App.Current.Exit();
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(3));


        var exePath = Assembly.GetEntryAssembly()!.Location;
        exePath = Path.ChangeExtension(exePath, ".exe");

        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
        });

        App.Current.Exit();
    }

    private bool CanStartElevator() => ElevatorService.ElevatorStatus == ElevatorStatus.NotRunning;

    [RelayCommand(CanExecute = nameof(CanStartElevator))]
    private async Task StartElevator()
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.WrapWholeWords,
            Text =
                "Press Start to launch the Elevator. The Elevator is an elevated (admin) process that is used for communication with the Genshin game process.\n\n" +
                "While the Elevator is active, you can press F10 within this App to refresh active mods in Genshin. " +
                "The Elevator process should automatically close when this program is closed.\n\n" +
                "After pressing Start, a User Account Control (UAC) prompt will appear to confirm the elevation.\n\n" +
                "(This requires that Genshin and that 3Dmigoto is running, when pressing F10",
            Margin = new Thickness(0, 0, 0, 12),
        };


        var doNotShowAgainCheckBox = new CheckBox
        {
            Content = "Don't Show this Again",
            IsChecked = false
        };

        var stackPanel = new StackPanel
        {
            Children =
            {
                text,
                doNotShowAgainCheckBox
            }
        };


        var dialog = new ContentDialog
        {
            Title = "Start Elevator Process?",
            Content = stackPanel,
            DefaultButton = ContentDialogButton.Primary,
            CloseButtonText = "Cancel",
            PrimaryButtonText = "Start",
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        var start = true;

        if (_showElevatorStartDialog)
        {
            var result = await dialog.ShowAsync();
            start = result == ContentDialogResult.Primary;
            if (start)
                _showElevatorStartDialog = !doNotShowAgainCheckBox.IsChecked == true;
        }

        if (start && ElevatorService.ElevatorStatus == ElevatorStatus.NotRunning)
        {
            try
            {
                ElevatorService.StartElevator();
            }
            catch (Win32Exception e)
            {
                _notificationManager.ShowNotification("Unable to start Elevator", e.Message, TimeSpan.FromSeconds(10));
                _showElevatorStartDialog = true;
            }
        }
    }

    [RelayCommand]
    private async Task ResetGenshinExePath()
    {
        var processSettings = await _localSettingsService.ReadSettingAsync<ProcessOptions>(ProcessOptions.Key) ??
                              new ProcessOptions();
        processSettings.GenshinExePath = null;
        await _localSettingsService.SaveSettingAsync(ProcessOptions.Key, processSettings);
    }

    [RelayCommand]
    private async Task Reset3DmigotoPath()
    {
        var processSettings = await _localSettingsService.ReadSettingAsync<ProcessOptions>(ProcessOptions.Key) ??
                              new ProcessOptions();
        processSettings.MigotoExePath = null;
        await _localSettingsService.SaveSettingAsync(ProcessOptions.Key, processSettings);
    }

    private void UpdateCheckerOnNewVersionAvailable(object? sender, UpdateChecker.NewVersionEventArgs e)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (e.Version == new Version())
            {
                CanIgnoreUpdate = _updateChecker.LatestRetrievedVersion != _updateChecker.IgnoredVersion;
                return;
            }

            LatestVersion = VersionFormatter(e.Version);
        });
    }

    private static string VersionFormatter(Version version)
    {
        return $"v{version.Major}.{version.Minor}.{version.Build}";
    }

    [RelayCommand(CanExecute = nameof(CanIgnoreUpdate))]
    private async Task IgnoreNewVersion()
    {
        await _updateChecker.IgnoreCurrentVersionAsync();
    }
}