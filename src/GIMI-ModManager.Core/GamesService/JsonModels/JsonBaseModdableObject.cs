namespace GIMI_ModManager.Core.GamesService.JsonModels;

internal class JsonBaseModdableObject : JsonBaseNameable
{
    public string? ModFilesName { get; set; }
    public bool? IsMultiMod { get; set; }
    public string? Image { get; set; }
    public string? ModCategory { get; set; }
}