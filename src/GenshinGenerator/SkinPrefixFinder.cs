using GIMI_ModManager.Core.Entities.Genshin;
using GIMI_ModManager.Core.Services;

namespace GenshinGenerator;

public static class SkinPrefixFinder
{
    static DirectoryInfo folderToSearch = new(@"..\..\..\..\PlayerCharacterData");
    static GenshinService genshinService = new();

    private static string InsertManually(string note)
    {
        return $"---------------INSERT_{note.ToUpper()}_MANUALLY---------------";
    }

    public static void SetDefaultSkins(GenshinCharacter character)
    {
        if (!folderToSearch.Exists) throw new DirectoryNotFoundException("PlayerCharacterData folder not found");

        if (character.InGameSkins.Any(skin => skin.DefaultSkin)) return;

        var characterSkinFolder = folderToSearch.GetDirectories();

        Tuple<DirectoryInfo, int> BestMatch = new(null!, -1);
        foreach (var skinFolder in characterSkinFolder)
        {
            var result = GenshinService.SearchCharacters(skinFolder.Name, new[] { character }, 200);
            if (result.Any())
            {
                var match = result.MaxBy(r => r.Value).Value;
                if (match > BestMatch.Item2) BestMatch = new Tuple<DirectoryInfo, int>(skinFolder, match);
            }
        }

        if (BestMatch.Item1 is null)
        {
            Console.WriteLine("No default skin found for " + character.DisplayName);
            return;
        }

        character.InGameSkins = new ISubSkin[]
        {
            new Skin(true, "Default", BestMatch.Item1.Name, "")
        };
    }

    public static void SetAdditionalSkins(GenshinCharacter character)
    {
        var defaultSkin = character.InGameSkins.FirstOrDefault();
        if (defaultSkin is null)
        {
            Console.WriteLine("No default skin found for " + character.DisplayName);
            character.InGameSkins = new ISubSkin[]
                { new Skin(true, "Default", InsertManually("NAME"), InsertManually("SKIN")) };
            return;
        }

        if (character.InGameSkins.Count() > 1)
        {
            Console.WriteLine("Additional skins already set for " + character.DisplayName);
            return;
        }

        var characterSkinFolder = folderToSearch.GetDirectories().ToList();

        characterSkinFolder.Remove(characterSkinFolder.First(f =>
            f.Name.Equals(defaultSkin.Name, StringComparison.CurrentCultureIgnoreCase)));

        var additionalSkins = new List<ISubSkin>();

        characterSkinFolder.ForEach(f =>
        {
            if (f.Name.Contains(defaultSkin.Name, StringComparison.CurrentCultureIgnoreCase))
            {
                var displayName = f.Name.EndsWith("CN") ? f.Name : InsertManually("SKIN_DISPLAY_NAME");
                var skin = new Skin(false, displayName, f.Name,
                    f.Name.Replace(defaultSkin.Name, "")) { ImageUri = "" };

                additionalSkins.Add(skin);
            }
        });

        if (!additionalSkins.Any()) Console.WriteLine("No additional skins found for " + character.DisplayName);

        additionalSkins = additionalSkins.OrderBy(s => s.SkinSuffix).ToList();

        additionalSkins.ForEach(s =>
            Console.WriteLine("Found skin " + s.DisplayName + " for " + character.DisplayName));

        character.InGameSkins = character.InGameSkins.Concat(additionalSkins).ToArray();
    }
}