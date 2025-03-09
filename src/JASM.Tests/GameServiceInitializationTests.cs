using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using Serilog;
using Serilog.Events;

namespace JASM.Tests;

public class GameServiceInitializationTests : IDisposable
{
    private const string GameAssetsPath = @"..\..\..\..\GIMI-ModManager.WinUI\Assets\Games";

    private readonly string _tmpDataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Guid.NewGuid().ToString());

    private readonly MockLogger _logger = new();


    private DirectoryInfo GetGameDirectory(string gameName)
    {
        var path = Path.Combine(GameAssetsPath, gameName);
        if (!Directory.Exists(path))
            throw new FileNotFoundException("Assets file not found.", path);
        return new DirectoryInfo(path);
    }

    private async Task<IGameService> InitGameService(string gamePath, bool characterSkinsAsCharacters = false)
    {
        Log.Logger = _logger;
        var path = GetGameDirectory(gamePath).FullName;

        var genshinService = new GameService(_logger, new MockLocalizer());
        await genshinService.InitializeAsync(new InitializationOptions
        {
            AssetsDirectory = path,
            LocalSettingsDirectory = _tmpDataDirectory,
            CharacterSkinsAsCharacters = characterSkinsAsCharacters
        });
        return genshinService;
    }


    public static IEnumerable<object[]> GetGameFolderNames
    {
        get
        {
            var gameDirs = new DirectoryInfo(GameAssetsPath);

            foreach (var gameDir in gameDirs.EnumerateDirectories())
            {
                yield return [gameDir.Name];
            }
        }
    }

    /// <summary>
    /// Ensure that all internal names are unique
    /// </summary>
    [Theory]
    [MemberData(nameof(GetGameFolderNames))]
    public async Task CheckFor_DuplicateIds(string gameName)
    {
        var genshinService = await InitGameService(gameName);
        var characters = genshinService.GetAllModdableObjects();

        var duplicateIds = characters.GroupBy(character => character.InternalName.Id).Where(g => g.Count() > 1);

        Assert.Empty(duplicateIds);
    }

    /// <summary>
    /// Also ensure that internal names are unique if the setting is set to use character skins as characters
    /// </summary>
    [Theory]
    [MemberData(nameof(GetGameFolderNames))]
    public async Task CheckFor_DuplicateIds_WithCharacterSkinsAsCharacters(string gameName)
    {
        var genshinService = await InitGameService(gameName, true);
        var characters = genshinService.GetAllModdableObjects();

        var duplicateIds = characters.GroupBy(character => character.InternalName.Id).Where(g => g.Count() > 1);

        Assert.Empty(duplicateIds);
    }


    /// <summary>
    /// Display name should be unique within a category
    /// </summary>
    [Theory]
    [MemberData(nameof(GetGameFolderNames))]
    public async Task CheckFor_DuplicateDisplayNames_WithinCategory(string gameName)
    {
        var genshinService = await InitGameService(gameName);

        var categories = genshinService.GetCategories();
        var moddableObjects = genshinService.GetAllModdableObjects();

        foreach (var category in categories)
        {
            var categoryModObjects = moddableObjects.Where(c => c.ModCategory == category);

            var duplicateNames = categoryModObjects.GroupBy(character => character.DisplayName.ToLower())
                .Where(g => g.Count() > 1);

            Assert.Empty(duplicateNames);
        }
    }


    [Theory]
    [MemberData(nameof(GetGameFolderNames))]
    public async Task CheckFor_DuplicateSubSkinNames(string gameName)
    {
        var genshinService = await InitGameService(gameName);
        var characters = genshinService.GetCharacters();

        var subSkins = characters.SelectMany(character => character.Skins)
            .ToArray();

        foreach (var characterSkins in subSkins.GroupBy(subSkin => subSkin.InternalName))
        {
            Assert.True(
                characterSkins.Count() == 1,
                $"Duplicate subskin name: {characterSkins.First().InternalName} in characters: {string.Join(", ", characterSkins.Select(skin => skin.Character.InternalName))}");
        }

        Assert.DoesNotContain(subSkins.GroupBy(subSkin => subSkin.InternalName.Id), g => g.Count() > 1);
    }

    /// <summary>
    /// Ensure that there are no errors or warnings in the logs. Should be fixed before merging.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetGameFolderNames))]
    public async Task CheckFor_ErrorsOrWarnings(string gameName)
    {
        await InitGameService(gameName);

        var logs = _logger.LogEvents;

        Assert.DoesNotContain(logs, log => log.Level == LogEventLevel.Warning);
        Assert.DoesNotContain(logs, log => log.Level == LogEventLevel.Error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tmpDataDirectory))
            Directory.Delete(_tmpDataDirectory, true);
    }
}

public class MockLogger : ILogger
{
    public List<LogEvent> LogEvents { get; } = new();

    public void Write(LogEvent logEvent)
    {
        LogEvents.Add(logEvent);
    }
}

public class MockLocalizer : ILanguageLocalizer
{
    public event EventHandler? LanguageChanged;

    public Task InitializeAsync()
    {
        CurrentLanguage = new Language("en-us");
        FallbackLanguage = new Language("en-us");
        AvailableLanguages = new List<ILanguage> { CurrentLanguage };
        return Task.CompletedTask;
    }

    public ILanguage CurrentLanguage { get; private set; } = new Language("en-us");
    public ILanguage FallbackLanguage { get; private set; } = new Language("en-us");
    public IReadOnlyList<ILanguage> AvailableLanguages { get; private set; } = new List<ILanguage>();

    public Task SetLanguageAsync(ILanguage language)
    {
        return Task.CompletedTask;
    }

    public Task SetLanguageAsync(string languageCode)
    {
        return Task.CompletedTask;
    }

    public string GetLocalizedString(string uid)
    {
        return uid;
    }

    public string? GetLocalizedStringOrDefault(string uid, string? defaultValue = null,
        bool? useUidAsDefaultValue = null)
    {
        return uid;
    }
}