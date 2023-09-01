using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models;

public class ModManagerOptions
{
    [JsonIgnore] public const string Section = "ModManagerOptions";
    public string? GimiRootFolderPath { get; set; }
    public string? ModsFolderPath { get; set; }
    public string? UnloadedModsFolderPath { get; set; }
}