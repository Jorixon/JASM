using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.Services;
using Serilog;
using Serilog.Events;

namespace JASM.Benchmark;

public static class Mocks
{
    public static ILogger Logger { get; } = new MockLogger();
    public static ILanguageLocalizer Localizer { get; } = new MockLocalizer();

    public static IGameService GetGameService()
    {
        var gameService = new GameService(Logger, Localizer);

        gameService.InitializeAsync(new InitializationOptions
        {
            AssetsDirectory = Helpers.GetGamesFolder("Genshin").FullName,
            LocalSettingsDirectory = Helpers.GetTmpFolder().FullName
        }).GetAwaiter().GetResult();

        return gameService;
    }

    public static ISkinManagerService GetSkinManagerService(IGameService? gameService)
    {
        gameService ??= GetGameService();
        var crawlerService = new ModCrawlerService(Logger, gameService);

        return new SkinManagerService(gameService, Logger, crawlerService);
    }
}

public class MockLogger : ILogger
{
    public void Write(LogEvent logEvent)
    {
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