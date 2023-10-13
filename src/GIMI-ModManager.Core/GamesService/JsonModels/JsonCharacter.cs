namespace GIMI_ModManager.Core.GamesService.JsonModels;

internal class JsonCharacter : JsonBaseNameable
{
    public int Id { get; set; } = -1;
    public string[]? Keys { get; set; }
    public string? ReleaseDate { get; set; }
    public string? Image { get; set; }
    public int? Rarity { get; set; }
    public string? RarityName { get; set; }
    public string? Element { get; set; } = string.Empty;

    public string? Class { get; set; } = string.Empty;

    public string[]? Region { get; set; } = Array.Empty<string>();
    public JsonCharacterSkin[] InGameSkins { get; set; } = Array.Empty<JsonCharacterSkin>();
}