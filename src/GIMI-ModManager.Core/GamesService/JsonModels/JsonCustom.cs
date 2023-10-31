namespace GIMI_ModManager.Core.GamesService.JsonModels;

internal class JsonCustom : JsonBaseNameable
{
    public JsonModDefinition[]? Mods { get; set; }

    public class JsonModDefinition : JsonBaseNameable
    {
        public string? Image { get; set; }
        public string? ModFilesName { get; set; }
        public int? Rarity { get; set; }
    }
}