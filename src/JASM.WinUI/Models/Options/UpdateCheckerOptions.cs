using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Options;

public class UpdateCheckerOptions
{
    [JsonIgnore] public const string Key = "UpdateChecker";
    public Version? IgnoreNewVersion { get; set; }
}