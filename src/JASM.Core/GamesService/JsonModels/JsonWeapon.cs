namespace GIMI_ModManager.Core.GamesService.JsonModels;

internal class JsonWeapon : JsonBaseModdableObject
{
    public int Rarity { get; set; }
    public string Type { get; set; } = string.Empty;
}