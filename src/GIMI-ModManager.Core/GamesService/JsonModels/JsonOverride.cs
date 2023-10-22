using Newtonsoft.Json;

namespace GIMI_ModManager.Core.GamesService.JsonModels;

internal class JsonOverride : JsonBaseNameable
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ICollection<string>? Keys { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? OverrideKeys { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Image { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ICollection<JsonCharacterSkin>? InGameSkins { get; set; }
}