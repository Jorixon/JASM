using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Options;

public class ProcessOptions
{
    [JsonIgnore] public const string Key = "ProcessOptions";
    public string? GenshinExePath { get; set; }
    public string? MigotoExePath { get; set; }
}