using Newtonsoft.Json;

namespace GIMI_ModManager.Core.GamesService.JsonModels;

internal class JsonCharacterSkin : JsonBaseNameable
{
    public string? ModFilesName { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Image { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ReleaseDate { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? Rarity { get; set; }
}