namespace GIMI_ModManager.Core.Services.ModPresetService.JsonModels;

internal class JsonModPreset
{
    public DateTime Created { get; set; } = DateTime.Now;
    public int Index { get; set; }
    public List<JsonModPresetEntry> Mods { get; set; } = new();
}