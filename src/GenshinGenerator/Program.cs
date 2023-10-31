using GIMI_ModManager.Core.Entities.Genshin;
using GIMI_ModManager.Core.GamesService.JsonModels;
using Newtonsoft.Json;

const string JsonPath = "..\\..\\..\\..\\characters.json";
const string JsonNewPath = "..\\..\\..\\..\\charactersNew.json";

var characters = JsonConvert.DeserializeObject<GenshinCharacter[]>(File.ReadAllText(JsonPath))!.ToList();

CheckDisplayNameIsUnique(characters);
CheckIdsAreUnique(characters);


var newFormat = new List<JsonCharacter>();
foreach (var genshinCharacter in characters)
{
    var characterJson = new JsonCharacter()
    {
        Id = genshinCharacter.Id,
        DisplayName = genshinCharacter.DisplayName,
        InternalName = genshinCharacter.DisplayName,
        Rarity = genshinCharacter.Rarity,
        ModFilesName = genshinCharacter.InGameSkins.First(skin => skin.DefaultSkin).Name,
        ReleaseDate = genshinCharacter.ReleaseDate.ToString("yyyy-MM-ddTHH:mm:ss"),
        Keys = genshinCharacter.Keys.ToArray(),
        Image = genshinCharacter.ImageUri,
        Element = genshinCharacter.Element.ToString(),
        Class = genshinCharacter.Weapon.ToString(),
        Region = genshinCharacter.Region.Select(r => r.ToString()).ToArray()
    };

    var skins = new List<JsonCharacterSkin>();

    foreach (var skin in genshinCharacter.InGameSkins)
    {
        if (skin.DefaultSkin)
            continue;

        var skinJson = new JsonCharacterSkin()
        {
            ModFilesName = skin.Name,
            DisplayName = skin.DisplayName,
            InternalName = skin.DisplayName,
            Image = skin.ImageUri
        };

        skins.Add(skinJson);
    }

    characterJson.InGameSkins = skins.ToArray();


    newFormat.Add(characterJson);
}


var newJson = JsonConvert.SerializeObject(newFormat.OrderBy(ch => ch.DisplayName), Formatting.Indented);

File.WriteAllText(JsonNewPath, newJson);

Console.WriteLine();

string ImageUri(string name, string fileType = "webp")
{
    return $"Character_{name}_Thumb.{fileType}";
}


void CheckIdsAreUnique(ICollection<GenshinCharacter> characters)
{
    var groupedIds = characters.GroupBy(ch => ch.Id).ToArray();
    // check for duplicate ids
    foreach (var g in groupedIds)
        if (g.Count() > 1)
            throw new InvalidOperationException($"Duplicate Id: {g.Key}");
}

void CheckDisplayNameIsUnique(ICollection<GenshinCharacter> characters)
{
    var groupedIds = characters.GroupBy(ch => ch.DisplayName).ToArray();
    // check for duplicate ids
    foreach (var g in groupedIds)
        if (g.Count() > 1)
            throw new InvalidOperationException($"Duplicate DisplayName: {g.Key}");
}

void CheckInGameSkinsAreValid(ICollection<GenshinCharacter> characters)
{
    foreach (var genshinCharacter in characters)
    {
        if (!genshinCharacter.InGameSkins.Any(s => s.DefaultSkin))
            throw new InvalidOperationException($"No Default skin found for character {genshinCharacter.DisplayName}");

        if (genshinCharacter.InGameSkins.Count(skin => skin.DefaultSkin) > 1)
            throw new InvalidOperationException(
                $"Multiple Default skins found for character {genshinCharacter.DisplayName}");
    }
}