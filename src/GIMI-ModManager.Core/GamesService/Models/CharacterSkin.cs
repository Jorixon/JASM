using GIMI_ModManager.Core.GamesService.JsonModels;

namespace GIMI_ModManager.Core.GamesService.Models;

public class CharacterSkin : ICharacterSkin
{
    public string ModFilesName { get; internal set; } = null!;
    public int Rarity { get; internal set; } = -1;
    public Uri? ImageUri { get; set; } = null;
    public string DisplayName { get; set; } = null!;
    public string InternalName { get; set; } = null!;
    public ICharacter Character { get; internal set; } = null!;
    public DateTime? ReleaseDate { get; internal set; } = null;


    internal static CharacterSkin FromJson(ICharacter Character, JsonCharacterSkin jsonSkin)
    {
        var internalName = jsonSkin.InternalName ??
                           throw new Character.InvalidJsonConfigException("InternalName can never be missing or null");

        var characterSkin = new CharacterSkin
        {
            InternalName = internalName,
            ModFilesName = jsonSkin.InternalName,
            DisplayName = jsonSkin.DisplayName ?? internalName,
            Rarity = jsonSkin.Rarity is >= 0 and <= 5 ? jsonSkin.Rarity.Value : -1,
            ReleaseDate = DateTime.TryParse(jsonSkin.ReleaseDate, out var date) ? date : DateTime.MaxValue,
            Character = Character
        };

        return characterSkin;
    }
}