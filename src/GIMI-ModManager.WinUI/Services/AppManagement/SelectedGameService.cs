using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models.Options;
using Newtonsoft.Json;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.AppManagement;

public class SelectedGameService
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ILogger _logger;

    private const string _defaultApplicationDataFolder = "ApplicationData";

    private readonly string _jasmAppDataPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JASM");

    private const string ConfigFile = "game.json";
    private readonly string _configPath;

    public const string Genshin = "Genshin";
    public const string Honkai = "Honkai";


    public SelectedGameService(ILocalSettingsService localSettingsService, ILogger logger)
    {
        _localSettingsService = localSettingsService;
        _logger = logger;
        Directory.CreateDirectory(_jasmAppDataPath);
        _configPath = Path.Combine(_jasmAppDataPath, ConfigFile);
    }

    private string GetGameSpecificSettingsFolderName(string game)
    {
        return Path.Combine(_defaultApplicationDataFolder + "_" + game);
    }

    public async Task SetSelectedGame(string game)
    {
        if (!IsValidGame(game))
            throw new ArgumentException("Invalid game name.");

        if (await GetSelectedGameAsync() == game)
            return;

        _localSettingsService.SetApplicationDataFolderName(GetGameSpecificSettingsFolderName(game));
        await SaveSelectedGameAsync(game).ConfigureAwait(false);
    }

    public async Task InitializeAsync()
    {
        if (!File.Exists(_configPath))
        {
            CopyOldAppFolder(Genshin);
            await SaveSelectedGameAsync(Genshin);
        }


        var selectedGame = await GetSelectedGameAsync();

        _localSettingsService.SetApplicationDataFolderName(GetGameSpecificSettingsFolderName(selectedGame));
    }

    public async Task<string> GetSelectedGameAsync()
    {
        if (!File.Exists(_configPath))
            return Genshin;

        var selectedGame = JsonConvert.DeserializeObject<SelectedGameModel>(await File.ReadAllTextAsync(_configPath));

        if (selectedGame == null || !IsValidGame(selectedGame.SelectedGame))
            return Genshin;


        return selectedGame.SelectedGame;
    }


    public Task SaveSelectedGameAsync(string game)
    {
        if (!IsValidGame(game))
            throw new ArgumentException("Invalid game name.");


        var selectedGame = new SelectedGameModel
        {
            SelectedGame = game
        };

        return File.WriteAllTextAsync(_configPath, JsonConvert.SerializeObject(selectedGame, Formatting.Indented));
    }

    public async Task<bool> IsJasmInitializedForGameAsync(string game)
    {
        if (!IsValidGame(game))
            throw new ArgumentException("Invalid game name.");

        string? oldGame = null;
        if (!_localSettingsService.SettingsLocation.Equals(GetGameSpecificSettingsFolderName(game),
                StringComparison.OrdinalIgnoreCase))
        {
            oldGame = game;
            _localSettingsService.SetApplicationDataFolderName(GetGameSpecificSettingsFolderName(game));
        }


        var modManagerOptions = await Task
            .Run(() => _localSettingsService.ReadSettingAsync<ModManagerOptions>(ModManagerOptions.Section));

        var ret = modManagerOptions is not null && !string.IsNullOrEmpty(modManagerOptions.GimiRootFolderPath) &&
                  !string.IsNullOrEmpty(modManagerOptions.ModsFolderPath);

        if (oldGame != null)
            _localSettingsService.SetApplicationDataFolderName(GetGameSpecificSettingsFolderName(oldGame));
        return ret;
    }

    private bool IsValidGame(string game)
    {
        if (Enum.TryParse<SupportedGames>(game, out var supportedGame))
            return supportedGame is SupportedGames.Genshin or SupportedGames.Honkai;

        return game == Genshin || game == Honkai;
    }


    private void CopyOldAppFolder(string game)
    {
        var oldAppSettingsFolder = new DirectoryInfo(Path.Combine(_jasmAppDataPath, _defaultApplicationDataFolder));

        if (!oldAppSettingsFolder.Exists)
        {
            _logger.Information("Could not find old app folder. Skipping copy.");
            return;
        }

        _logger.Information("Copying old app settings folder to new name.");

        var newAppSettingsFolder =
            new DirectoryInfo(Path.Combine(_jasmAppDataPath, GetGameSpecificSettingsFolderName(game)));
        newAppSettingsFolder.Create();

        if (newAppSettingsFolder.GetFiles().Any())
        {
            _logger.Information("New app settings folder is not empty. Skipping copy.");
            return;
        }

        foreach (var file in oldAppSettingsFolder.GetFiles())
        {
            file.CopyTo(Path.Combine(newAppSettingsFolder.FullName, file.Name));
        }
    }
}

public class SelectedGameModel
{
    public string SelectedGame { get; set; } = "Genshin";
}