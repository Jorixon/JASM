namespace GIMI_ModManager.Core.GamesService.JsonModels;

internal class JsonOverride : JsonBaseNameable
{
    public ICollection<string>? Keys { get; set; }
    public bool? OverrideKeys { get; set; }
    public string? Image { get; set; }

    public ICollection<JsonCharacterSkin>? InGameSkins { get; set; }
}