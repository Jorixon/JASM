using GenshinGenerator;
using GIMI_ModManager.Core.Entities.Genshin;
using Newtonsoft.Json;

const string JsonPath = "..\\..\\..\\..\\characters.json";
const string JsonNewPath = "..\\..\\..\\..\\charactersNew.json";

var characters = JsonConvert.DeserializeObject<GenshinCharacter[]>(File.ReadAllText(JsonPath))!.ToList();

CheckDisplayNameIsUnique(characters);
CheckIdsAreUnique(characters);


var characterToAdd = new GenshinCharacter[]
{
};
var currentMaxId = characters.Max(ch => ch.Id);
var nextId = characters.Max(ch => ch.Id) + 1;

foreach (var character in characterToAdd)
{
    character.Id = nextId;
    if (string.IsNullOrEmpty(character.ImageUri))
        character.ImageUri = ImageUri(character.DisplayName);

    if (characters.Contains(character) || characters.Any(ch => ch.DisplayName == character.DisplayName) ||
        characters.Any(ch =>
            string.Equals(ch.DisplayName, character.DisplayName, StringComparison.CurrentCultureIgnoreCase)))
        throw new InvalidOperationException("Duplicate Characters");

    // Check for duplicate keys
    if (characters.Any(ch => ch.Keys.Any(k => character.Keys.Contains(k))))
        throw new InvalidOperationException("Duplicate Keys");

    nextId++;
    if (character.Id > currentMaxId)
        currentMaxId = character.Id;
    else
        throw new InvalidOperationException("Id is not unique");
}

characters.AddRange(characterToAdd);
CheckDisplayNameIsUnique(characters);
CheckIdsAreUnique(characters);

characters.ForEach(SkinPrefixFinder.SetDefaultSkins);
characters.ForEach(SkinPrefixFinder.SetAdditionalSkins);
CheckInGameSkinsAreValid(characters);


var newJson = JsonConvert.SerializeObject(characters.OrderBy(ch => ch.DisplayName), Formatting.Indented);

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