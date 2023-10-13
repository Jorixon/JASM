namespace GIMI_ModManager.Core.GamesService.JsonModels;

internal class JsonCharacterSkin : JsonBaseNameable
{
    public string? Image { get; set; }
    public string? ReleaseDate { get; set; }
    public int? Rarity { get; set; }
}