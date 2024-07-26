using GIMI_ModManager.Core.CommandService;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.Core.Services.GameBanana;
using GIMI_ModManager.Core.Services.ModPresetService;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models.Options;
using GIMI_ModManager.WinUI.Models.Settings;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;

namespace GIMI_ModManager.WinUI.Activation;

/// <summary>
/// FirstTimeStartupActivationHandler is completely wrong name for this class. This is the default startup handler.
/// </summary>
public class FirstTimeStartupActivationHandler : ActivationHandler<LaunchActivatedEventArgs>
{
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGameService _gameService;
    private readonly ModPresetService _modPresetService;
    private readonly UserPreferencesService _userPreferencesService;
    private readonly SelectedGameService _selectedGameService;
    private readonly ModArchiveRepository _modArchiveRepository;
    private readonly CommandService _commandService;
    public override string ActivationName { get; } = "RegularStartup";

    public FirstTimeStartupActivationHandler(INavigationService navigationService,
        ILocalSettingsService localSettingsService,
        ISkinManagerService skinManagerService, IGameService gameService, SelectedGameService selectedGameService,
        ModPresetService modPresetService, UserPreferencesService userPreferencesService,
        ModArchiveRepository modArchiveRepository, CommandService commandService)
    {
        _navigationService = navigationService;
        _localSettingsService = localSettingsService;
        _skinManagerService = skinManagerService;
        _gameService = gameService;
        _selectedGameService = selectedGameService;
        _modPresetService = modPresetService;
        _userPreferencesService = userPreferencesService;
        _modArchiveRepository = modArchiveRepository;
        _commandService = commandService;
    }

    protected override bool CanHandleInternal(LaunchActivatedEventArgs args)
    {
        var options = Task
            .Run(async () => await _localSettingsService.ReadSettingAsync<ModManagerOptions>(ModManagerOptions.Section))
            .GetAwaiter().GetResult();

        return Directory.Exists(options?.ModsFolderPath) && Directory.Exists(options?.GimiRootFolderPath);
    }

    protected override async Task HandleInternalAsync(LaunchActivatedEventArgs args)
    {
        var modManagerOptions =
            await _localSettingsService.ReadSettingAsync<ModManagerOptions>(ModManagerOptions.Section);

        var gameServiceOptions = new InitializationOptions
        {
            AssetsDirectory = Path.Combine(App.ASSET_DIR, "Games",
                await _selectedGameService.GetSelectedGameAsync()),
            LocalSettingsDirectory = _localSettingsService.ApplicationDataFolder,
            CharacterSkinsAsCharacters = modManagerOptions?.CharacterSkinsAsCharacters ?? false
        };

        await Task.Run(async () =>
        {
            var modArchiveSettings =
                await _localSettingsService.ReadOrCreateSettingAsync<ModArchiveSettings>(ModArchiveSettings.Key);

            await _gameService.InitializeAsync(gameServiceOptions).ConfigureAwait(false);

            await _skinManagerService.InitializeAsync(modManagerOptions!.ModsFolderPath!, null,
                modManagerOptions.GimiRootFolderPath).ConfigureAwait(false);

            var tasks = new List<Task>
            {
                _userPreferencesService.InitializeAsync(),
                _modPresetService.InitializeAsync(_localSettingsService.ApplicationDataFolder),
                _modArchiveRepository.InitializeAsync(_localSettingsService.ApplicationDataFolder,
                    o => o.MaxDirectorySizeGb = modArchiveSettings.MaxLocalArchiveCacheSizeGb),
                _commandService.InitializeAsync(_localSettingsService.ApplicationDataFolder)
            };

            await Task.WhenAll(tasks).ConfigureAwait(false);
        });


        _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!,
            _gameService.GetCategories().First(c => c.InternalNameEquals("Character")), true);
    }
}