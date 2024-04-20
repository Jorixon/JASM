using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.Core.Services.GameBanana;
using GIMI_ModManager.Core.Services.ModPresetService;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models.Options;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.Validators.PreConfigured;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class StartupViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly ILogger _logger = Log.ForContext<StartupViewModel>();
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IWindowManagerService _windowManagerService;
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGameService _gameService;
    private readonly ModPresetService _modPresetService;
    private readonly UserPreferencesService _userPreferencesService;
    private readonly SelectedGameService _selectedGameService;
    private readonly ModArchiveRepository _modArchiveRepository;


    private const string _genshinModelImporterName = "Genshin-Impact-Model-Importer";
    private const string _genshinModelImporterShortName = "GIMI";
    private readonly Uri _genshinGameBananaUrl = new("https://gamebanana.com/games/8552");
    private readonly Uri _genshinModelImporterUrl = new("https://github.com/SilentNightSound/GI-Model-Importer");

    private const string _honkaiModelImporterName = "Star-Rail-Model-Importer";
    private const string _honkaiModelImporterShortName = "SRMI";
    private readonly Uri _honkaiGameBananaUrl = new("https://gamebanana.com/games/18366");
    private readonly Uri _honkaiModelImporterUrl = new("https://github.com/SilentNightSound/SR-Model-Importer");

    public PathPicker PathToGIMIFolderPicker { get; }
    public PathPicker PathToModsFolderPicker { get; }
    [ObservableProperty] private bool _reorganizeModsOnStartup;
    [ObservableProperty] private bool _disableMods;

    [ObservableProperty] private string _selectedGame = SelectedGameService.Genshin;

    [ObservableProperty] private string _modelImporterName = _genshinModelImporterName;
    [ObservableProperty] private string _modelImporterShortName = _genshinModelImporterShortName;
    [ObservableProperty] private Uri _gameBananaUrl = new("https://gamebanana.com/games/8552");

    [ObservableProperty] private Uri _modelImporterUrl = new("https://github.com/SilentNightSound");

    public ObservableCollection<string> Games { get; } = new()
    {
        SelectedGameService.Genshin,
        SelectedGameService.Honkai
    };

    public StartupViewModel(INavigationService navigationService, ILocalSettingsService localSettingsService,
        IWindowManagerService windowManagerService, ISkinManagerService skinManagerService,
        SelectedGameService selectedGameService, IGameService gameService, ModPresetService modPresetService,
        UserPreferencesService userPreferencesService, ModArchiveRepository modArchiveRepository)
    {
        _navigationService = navigationService;
        _localSettingsService = localSettingsService;
        _windowManagerService = windowManagerService;
        _skinManagerService = skinManagerService;
        _selectedGameService = selectedGameService;
        _gameService = gameService;
        _modPresetService = modPresetService;
        _userPreferencesService = userPreferencesService;
        _modArchiveRepository = modArchiveRepository;

        PathToGIMIFolderPicker = new PathPicker(GimiFolderRootValidators.Validators);

        PathToModsFolderPicker =
            new PathPicker(ModsFolderValidator.Validators);

        PathToGIMIFolderPicker.IsValidChanged += (sender, args) => SaveStartupSettingsCommand.NotifyCanExecuteChanged();
        PathToModsFolderPicker.IsValidChanged +=
            (sender, args) => SaveStartupSettingsCommand.NotifyCanExecuteChanged();
    }


    private bool ValidStartupSettings() => PathToGIMIFolderPicker.IsValid && PathToModsFolderPicker.IsValid &&
                                           PathToGIMIFolderPicker.Path != PathToModsFolderPicker.Path;


    [RelayCommand(CanExecute = nameof(ValidStartupSettings))]
    private async Task SaveStartupSettings()
    {
        var modManagerOptions = new ModManagerOptions()
        {
            GimiRootFolderPath = PathToGIMIFolderPicker.Path,
            ModsFolderPath = PathToModsFolderPicker.Path,
            UnloadedModsFolderPath = null
        };

        await _selectedGameService.SetSelectedGame(SelectedGame);

        await _gameService.InitializeAsync(
            Path.Combine(App.ASSET_DIR, "Games", await _selectedGameService.GetSelectedGameAsync()),
            _localSettingsService.ApplicationDataFolder);

        await _localSettingsService.SaveSettingAsync(ModManagerOptions.Section,
            modManagerOptions);
        _logger.Information("Saved startup settings: {@ModManagerOptions}", modManagerOptions);

        await _skinManagerService.InitializeAsync(modManagerOptions.ModsFolderPath!, null,
            modManagerOptions.GimiRootFolderPath);

        var tasks = new List<Task>
        {
            _userPreferencesService.InitializeAsync(),
            _modPresetService.InitializeAsync(_localSettingsService.ApplicationDataFolder),
            _modArchiveRepository.InitializeAsync(_localSettingsService.ApplicationDataFolder)
        };

        await Task.WhenAll(tasks);

        await _localSettingsService.SaveSettingAsync(ActivationService.IgnoreNewFolderStructureKey, true);

        if (ReorganizeModsOnStartup)
        {
            await Task.Run(() => _skinManagerService.ReorganizeModsAsync(disableMods: DisableMods));
        }


        _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!, null, true);
        _windowManagerService.ResizeWindowPercent(_windowManagerService.MainWindow, 80, 80);
        _windowManagerService.MainWindow.CenterOnScreen();
        App.GetService<NotificationManager>().ShowNotification("Startup settings saved",
            $"Startup settings saved successfully to '{_localSettingsService.SettingsLocation}'",
            TimeSpan.FromSeconds(7));
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(7));
            App.GetService<NotificationManager>().ShowNotification("JASM is still in alpha",
                "There will be bugs and things will most likely break. Anyway, hope you enjoy using Just Another Skin Manager!",
                TimeSpan.FromSeconds(20));
        });
    }


    [RelayCommand]
    private async Task BrowseGimiModFolderAsync()
    {
        await PathToGIMIFolderPicker.BrowseFolderPathAsync(App.MainWindow);
        if (PathToGIMIFolderPicker.PathHasValue &&
            !PathToModsFolderPicker.PathHasValue)
            PathToModsFolderPicker.Path = Path.Combine(PathToGIMIFolderPicker.Path!, "Mods");
    }


    [RelayCommand]
    private async Task BrowseModsFolderAsync()
        => await PathToModsFolderPicker.BrowseFolderPathAsync(App.MainWindow);

    public async void OnNavigatedTo(object parameter)
    {
        _windowManagerService.ResizeWindowPercent(_windowManagerService.MainWindow, 50, 60);
        _windowManagerService.MainWindow.CenterOnScreen();

        var settings =
            await _localSettingsService.ReadOrCreateSettingAsync<ModManagerOptions>(ModManagerOptions.Section);

        SetPaths(settings);

        SelectedGame = await _selectedGameService.GetSelectedGameAsync();
        SetGameInfo(SelectedGame);
        ReorganizeModsOnStartup = true;
    }

    [RelayCommand]
    private async Task SetGameAsync(string game)
    {
        if (game.IsNullOrEmpty())
            return;

        await _selectedGameService.SetSelectedGame(game);
        SelectedGame = game;

        var settings =
            await _localSettingsService.ReadOrCreateSettingAsync<ModManagerOptions>(ModManagerOptions.Section);

        SetPaths(settings);
        SetGameInfo(game);
    }


    private void SetGameInfo(string game)
    {
        if (game == SelectedGameService.Genshin)
        {
            ModelImporterName = _genshinModelImporterName;
            ModelImporterShortName = _genshinModelImporterShortName;
            GameBananaUrl = _genshinGameBananaUrl;
            ModelImporterUrl = _genshinModelImporterUrl;
        }
        else if (game == SelectedGameService.Honkai)
        {
            ModelImporterName = _honkaiModelImporterName;
            ModelImporterShortName = _honkaiModelImporterShortName;
            GameBananaUrl = _honkaiGameBananaUrl;
            ModelImporterUrl = _honkaiModelImporterUrl;
        }
    }


    private void SetPaths(ModManagerOptions settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.GimiRootFolderPath))
            PathToGIMIFolderPicker.Path = settings.GimiRootFolderPath;
        else
            PathToGIMIFolderPicker.Path = "";

        if (!string.IsNullOrWhiteSpace(settings.ModsFolderPath))
            PathToModsFolderPicker.Path = settings.ModsFolderPath;
        else
            PathToModsFolderPicker.Path = "";
    }


    public void OnNavigatedFrom()
    {
    }
}