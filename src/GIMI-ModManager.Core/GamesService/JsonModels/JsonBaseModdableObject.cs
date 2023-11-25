namespace GIMI_ModManager.Core.GamesService.JsonModels;

internal class JsonBaseModdableObject : JsonBaseNameable
{
    public string? ModFilesName { get; set; }
    public bool? IsMultiMod { get; set; }
}