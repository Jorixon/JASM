using GIMI_ModManager.Core.Services;

namespace JASM.Tests;

public class CharactersJsonTests
{
    private readonly string AssetsUriPath = Path.GetFullPath("..\\..\\..\\..\\GIMI-ModManager.WinUI\\Assets");

    private string JsonPath => Path.Combine(AssetsUriPath, "characters.json");

    private async Task<IGenshinService> InitGenshinService()
    {
        if (!File.Exists(AssetsUriPath + "\\characters.json"))
            throw new FileNotFoundException("Assets file not found.", AssetsUriPath);
        var genshinService = new GenshinService();
        await genshinService.InitializeAsync(AssetsUriPath);
        return genshinService;
    }

    [Fact]
    public async void CheckFor_DuplicateCharacterIds()
    {
        var genshinService = await InitGenshinService();
        var characters = genshinService.GetCharacters();

        var duplicateIds = characters.GroupBy(character => character.Id).Where(g => g.Count() > 1);

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
    public async void CheckFor_UnpopulatedFields()
    {
        var json = await File.ReadAllTextAsync(JsonPath);

        Assert.DoesNotContain("-----INSERT_", json);
    }

    [Fact]
    public async void CheckFor_DuplicateSubSkinNames()
    {
        var genshinService = await InitGenshinService();
        var characters = genshinService.GetCharacters();

        var subSkins = characters.SelectMany(character => character.InGameSkins);

        Assert.Empty(subSkins.GroupBy(subSkin => subSkin.Name).Where(g => g.Count() > 1));
    }
}