using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Settings;

public class CharacterOverviewSettings
{
    [JsonIgnore] public const string Key = "CharacterOverviewOptions";

    public string[] PinedCharacters { get; set; } = Array.Empty<string>();
    public string[] HiddenCharacters { get; set; } = Array.Empty<string>();
    public int[] IgnoreMultipleModsWarning { get; set; } = Array.Empty<int>();
    public bool ShowOnlyCharactersWithMods { get; set; } = false;
    public bool SortByDescending { get; set; } = false;
    public string? SortingMethod { get; set; }
}