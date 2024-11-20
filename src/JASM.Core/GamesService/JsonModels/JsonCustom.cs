namespace GIMI_ModManager.Core.GamesService.JsonModels;

internal class JsonCustom
{
    public string? CategoryInternalName { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryNamePlural { get; set; }

    public JsonBaseModdableObject[]? Items { get; set; } = Array.Empty<JsonBaseModdableObject>();
}