using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Windows.ApplicationModel;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErrorOr;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities.Genshin;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Models.Options;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Validators.PreConfigured;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class SettingsViewModel : ObservableRecipient
{
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ILogger _logger;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly INavigationViewService _navigationViewService;
    private readonly IWindowManagerService _windowManagerService;
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGenshinService _genshinService;
    private readonly AutoUpdaterService _autoUpdaterService;


    private readonly NotificationManager _notificationManager;
    private readonly UpdateChecker _updateChecker;
    public ElevatorService ElevatorService;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ResetGenshinExePathCommand))]
    public GenshinProcessManager _genshinProcessManager;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(Reset3DmigotoPathCommand))]
    public ThreeDMigtoProcessManager _threeDMigtoProcessManager;


    [ObservableProperty] private ElementTheme _elementTheme;

    [ObservableProperty] private string _versionDescription;

    [ObservableProperty] private string _latestVersion = string.Empty;
    [ObservableProperty] private bool _showNewVersionAvailable = false;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(IgnoreNewVersionCommand))]
    private bool _CanIgnoreUpdate = false;

    [ObservableProperty] private ObservableCollection<string> _languages = new();
    [ObservableProperty] private string _selectedLanguage = string.Empty;
    private Dictionary<string, string> _nameToLangCode = new();

    public PathPicker PathToGIMIFolderPicker { get; }
    public PathPicker PathToModsFolderPicker { get; }


    private static bool _showElevatorStartDialog = true;

    private ModManagerOptions? _modManagerOptions = null!;

    public SettingsViewModel(
        IThemeSelectorService themeSelectorService, ILocalSettingsService localSettingsService,
        ElevatorService elevatorService, ILogger logger, NotificationManager notificationManager,
        INavigationViewService navigationViewService, IWindowManagerService windowManagerService,
        ISkinManagerService skinManagerService, UpdateChecker updateChecker,
        GenshinProcessManager genshinProcessManager, ThreeDMigtoProcessManager threeDMigtoProcessManager,
        IGenshinService genshinService, AutoUpdaterService autoUpdaterService
    )
    {
        _themeSelectorService = themeSelectorService;
        _localSettingsService = localSettingsService;
        ElevatorService = elevatorService;
        _notificationManager = notificationManager;
        _navigationViewService = navigationViewService;
        _windowManagerService = windowManagerService;
        _skinManagerService = skinManagerService;
        _updateChecker = updateChecker;
        _genshinService = genshinService;
        _autoUpdaterService = autoUpdaterService;
        GenshinProcessManager = genshinProcessManager;
        ThreeDMigtoProcessManager = threeDMigtoProcessManager;
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


        var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
        cultures = cultures.Append(new CultureInfo("zh-cn")).ToArray();


        var supportedCultures = App.Localizer.GetAvailableLanguages().ToArray();

        foreach (var culture in cultures)
        {
            if (!supportedCultures.Contains(culture.Name.ToLower())) continue;

            Languages.Add(culture.NativeName);
            _nameToLangCode.Add(culture.NativeName, culture.Name.ToLower());

            if (culture.Name.ToLower() == App.Localizer.GetCurrentLanguage())
                SelectedLanguage = culture.NativeName;
        }
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

            version = new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build,
                packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        }

        return
            $"{"AppDisplayName".GetLocalized()} - {VersionFormatter(version)}";
    }


    private bool ValidFolderSettings()
    {
        return PathToGIMIFolderPicker.IsValid && PathToModsFolderPicker.IsValid &&
               PathToGIMIFolderPicker.Path != PathToModsFolderPicker.Path &&
               (PathToGIMIFolderPicker.Path != _modManagerOptions?.GimiRootFolderPath ||
                PathToModsFolderPicker.Path != _modManagerOptions?.ModsFolderPath);
    }


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
    {
        await PathToModsFolderPicker.BrowseFolderPathAsync(App.MainWindow);
    }

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
            Application.Current.Exit();
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(3));


        var exePath = Assembly.GetEntryAssembly()!.Location;
        exePath = Path.ChangeExtension(exePath, ".exe");

        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true
        });

        Application.Current.Exit();
    }

    private bool CanStartElevator()
    {
        return ElevatorService.ElevatorStatus == ElevatorStatus.NotRunning;
    }

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
            Margin = new Thickness(0, 0, 0, 12)
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

    private bool CanResetGenshinExePath()
    {
        return GenshinProcessManager.ProcessStatus != ProcessStatus.NotInitialized;
    }

    [RelayCommand(CanExecute = nameof(CanResetGenshinExePath))]
    private async Task ResetGenshinExePath()
    {
        await GenshinProcessManager.ResetProcessOptions();
    }

    private bool CanReset3DmigotoPath()
    {
        return ThreeDMigtoProcessManager.ProcessStatus != ProcessStatus.NotInitialized;
    }

    [RelayCommand(CanExecute = nameof(CanReset3DmigotoPath))]
    private async Task Reset3DmigotoPath()
    {
        await ThreeDMigtoProcessManager.ResetProcessOptions();
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

    [ObservableProperty] private bool _exportingMods = false;
    [ObservableProperty] private int _exportProgress = 0;
    [ObservableProperty] private string _exportProgressText = string.Empty;
    [ObservableProperty] private string? _currentModName;

    [RelayCommand]
    private async Task ExportMods(ContentDialog contentDialog)
    {
        var dialog = new ContentDialog()
        {
            PrimaryButtonText = "Export",
            IsPrimaryButtonEnabled = true,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.Title = "Export Mods";

        dialog.ContentTemplate = contentDialog.ContentTemplate;

        var model = new ExportModsDialogModel(_genshinService.GetCharacters());
        dialog.DataContext = model;
        var result = await _windowManagerService.ShowDialogAsync(dialog);

        if (result != ContentDialogResult.Primary)
            return;

        var folderPicker = new FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        folderPicker.FileTypeFilter.Add("*");
        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder == null)
            return;

        ExportingMods = true;
        _navigationViewService.IsEnabled = false;

        var charactersToExport =
            model.CharacterModsToBackup.Where(modList => modList.IsChecked).Select(ch => ch.Character);
        var modsList = new List<ICharacterModList>();
        foreach (var character in charactersToExport) modsList.Add(_skinManagerService.GetCharacterModList(character));

        try
        {
            _skinManagerService.ModExportProgress += HandleProgressEvent;
            await Task.Run(() =>
            {
                _skinManagerService.ExportMods(modsList, folder.Path,
                    removeLocalJasmSettings: model.RemoveJasmSettings, zip: false,
                    keepCharacterFolderStructure: model.KeepFolderStructure, setModStatus: model.SetModStatus);
            });
            _notificationManager.ShowNotification("Mods exported", $"Mods exported to {folder.Path}",
                TimeSpan.FromSeconds(5));
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error exporting mods");
            _notificationManager.ShowNotification("Error exporting mods", e.Message, TimeSpan.FromSeconds(10));
        }
        finally
        {
            _skinManagerService.ModExportProgress -= HandleProgressEvent;
            ExportingMods = false;
            _navigationViewService.IsEnabled = true;
        }
    }

    private void HandleProgressEvent(object? sender, ExportProgress args)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            ExportProgress = args.Progress;
            ExportProgressText = args.Operation;
            CurrentModName = args.ModName;
        });
    }


    [RelayCommand]
    private async Task SelectLanguage(string selectedLanguageName)
    {
        if (_nameToLangCode.TryGetValue(selectedLanguageName, out var langCode))
        {
            if (langCode == App.Localizer.GetCurrentLanguage())
                return;

            await App.Localizer.SetLanguage(langCode);

            var appSettings = await _localSettingsService.ReadOrCreateSettingAsync<AppSettings>(AppSettings.Key);
            appSettings.Language = langCode;
            await _localSettingsService.SaveSettingAsync(AppSettings.Key, appSettings);
        }
    }

    [RelayCommand]
    private void UpdateJasm()
    {
        var errors = Array.Empty<Error>();
        try
        {
            errors = _autoUpdaterService.StartSelfUpdateProcess();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error starting update process");
            _notificationManager.ShowNotification("Error starting update process", e.Message, TimeSpan.FromSeconds(10));
        }

        if (errors is not null && errors.Any())
        {
            var errorMessages = errors.Select(e => e.Description).ToArray();
            _notificationManager.ShowNotification("Could not start update process", string.Join('\n', errorMessages),
                TimeSpan.FromSeconds(10));
        }
    }
}

public partial class ExportModsDialogModel : ObservableObject
{
    [ObservableProperty] private bool _zipMods = false;
    [ObservableProperty] private bool _keepFolderStructure = true;

    [ObservableProperty] private bool _removeJasmSettings = false;

    public ObservableCollection<CharacterCheckboxModel> CharacterModsToBackup { get; set; } = new();

    public ObservableCollection<SetModStatus> SetModStatuses { get; set; } = new()
    {
        SetModStatus.KeepCurrent,
        SetModStatus.EnableAllMods,
        SetModStatus.DisableAllMods
    };

    [ObservableProperty] private SetModStatus _setModStatus = SetModStatus.KeepCurrent;

    public ExportModsDialogModel(IEnumerable<GenshinCharacter> characters)
    {
        SetModStatus = SetModStatus.KeepCurrent;
        foreach (var character in characters) CharacterModsToBackup.Add(new CharacterCheckboxModel(character));
    }
}

public partial class CharacterCheckboxModel : ObservableObject
{
    [ObservableProperty] private bool _isChecked = true;
    [ObservableProperty] private GenshinCharacter _character;

    public CharacterCheckboxModel(GenshinCharacter character)
    {
        _character = character;
    }
}