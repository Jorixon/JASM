using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using Serilog;
using Serilog.Events;

namespace JASM.Tests;

public class CharactersJsonTests : IDisposable
{
    private readonly string AssetsUriPath =
        Path.GetFullPath("..\\..\\..\\..\\GIMI-ModManager.WinUI\\Assets\\Games\\Genshin");

    private readonly string _tmpDataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Guid.NewGuid().ToString());

    private async Task<IGameService> InitGenshinService()
    {
        if (!Directory.Exists(AssetsUriPath))
            throw new FileNotFoundException("Assets file not found.", AssetsUriPath);
        var genshinService = new GameService(new MockLogger(), new MockLocalizer());
        await genshinService.InitializeAsync(new InitializationOptions
        {
            AssetsDirectory = AssetsUriPath,
            LocalSettingsDirectory = _tmpDataDirectory
        });
        return genshinService;
    }

    [Fact]
    public async void CheckFor_DuplicateCharacterIds()
    {
        var genshinService = await InitGenshinService();
        var characters = genshinService.GetCharacters();

        var duplicateIds = characters.GroupBy(character => character.InternalName.Id).Where(g => g.Count() > 1);

        Assert.Empty(duplicateIds);
    }


    [Fact]
    public async void CheckFor_DuplicateNames()
    {
        var genshinService = await InitGenshinService();
        var characters = genshinService.GetCharacters();

        var duplicateNames = characters.GroupBy(character => character.DisplayName.ToLower())
            .Where(g => g.Count() > 1);

        Assert.Empty(duplicateNames);
    }


    [Fact]
    public async void CheckFor_DuplicateSubSkinNames()
    {
        var genshinService = await InitGenshinService();
        var characters = genshinService.GetCharacters();

        var subSkins = characters.SelectMany(character => character.Skins)
            .ToArray();

        foreach (var characterSkins in subSkins.GroupBy(subSkin => subSkin.InternalName))
        {
            Assert.True(
                characterSkins.Count() == 1,
                $"Duplicate subskin name: {characterSkins.First().InternalName} in characters: {string.Join(", ", characterSkins.Select(skin => skin.Character.InternalName))}");
        }

        Assert.Empty(subSkins.GroupBy(subSkin => subSkin.InternalName.Id).Where(g => g.Count() > 1));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tmpDataDirectory))
            Directory.Delete(_tmpDataDirectory, true);
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
}