using Newtonsoft.Json;

namespace GIMI_ModManager.Core.GamesService.JsonModels;

internal class JsonCharacter : JsonBaseNameable
{
    public string[]? Keys { get; set; }
    public string? ReleaseDate { get; set; }
    public string? Image { get; set; }
    public int? Rarity { get; set; }
    public string? Element { get; set; } = string.Empty;

    public string? Class { get; set; } = string.Empty;

    public string[]? Region { get; set; } = Array.Empty<string>();

    public string? ModFilesName { get; set; }
    public bool? IsMultiMod { get; set; }


    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public JsonCharacterSkin[]? InGameSkins { get; set; } = Array.Empty<JsonCharacterSkin>();
}