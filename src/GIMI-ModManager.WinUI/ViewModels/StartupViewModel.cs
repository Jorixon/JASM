using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.WinUI.Contracts.Services;
using Windows.Storage.Pickers;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services;
using Serilog;
using FluentValidation;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Validators;
using GIMI_ModManager.WinUI.Validators.PreConfigured;
using PathPicker = GIMI_ModManager.WinUI.ViewModels.SubVms.PathPicker;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class StartupViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly ILogger _logger = Log.ForContext<StartupViewModel>();
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IWindowManagerService _windowManagerService;
    private readonly ISkinManagerService _skinManagerService;

    public PathPicker PathToGIMIFolderPicker { get; }
    public PathPicker PathToModsFolderPicker { get; }
    [ObservableProperty] private bool _reorganizeModsOnStartup = false;

    public StartupViewModel(INavigationService navigationService, ILocalSettingsService localSettingsService,
        IWindowManagerService windowManagerService, ISkinManagerService skinManagerService)
    {
        _navigationService = navigationService;
        _localSettingsService = localSettingsService;
        _windowManagerService = windowManagerService;
        _skinManagerService = skinManagerService;

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
        await _localSettingsService.SaveSettingAsync(ModManagerOptions.Section,
            modManagerOptions);
        _logger.Information("Saved startup settings: {@ModManagerOptions}", modManagerOptions);

        await _skinManagerService.Initialize(modManagerOptions.ModsFolderPath!, null, modManagerOptions.GimiRootFolderPath);

        if (ReorganizeModsOnStartup)
        {
            await Task.Run(() => _skinManagerService.ReorganizeMods());
        }


        _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!, null, true);
        _windowManagerService.ResizeWindowPercent(_windowManagerService.MainWindow, 80, 80);
        _windowManagerService.MainWindow.CenterOnScreen();
        App.GetService<NotificationManager>().ShowNotification("Startup settings saved",
            $"Startup settings saved successfully to '{_localSettingsService.SettingsLocation}'",
            TimeSpan.FromSeconds(7));
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

    public void OnNavigatedTo(object parameter)
    {
        _windowManagerService.ResizeWindowPercent(_windowManagerService.MainWindow, 40, 60);
        _windowManagerService.MainWindow.CenterOnScreen();
    }

    public void OnNavigatedFrom()
    {
    }
}