using GIMI_ModManager.WinUI.ViewModels;
using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Settings;

public class CharacterOverviewSettings
{
    [JsonIgnore] public const string Key = "CharacterOverviewOptions";

    public int[] PinedCharacters { get; set; } = Array.Empty<int>();
    public int[] HiddenCharacters { get; set; } = Array.Empty<int>();
    public int[] IgnoreMultipleModsWarning { get; set; } = Array.Empty<int>();
    public bool ShowOnlyCharactersWithMods { get; set; } = false;
    public bool SortByDescending { get; set; } = false;
    public SortingMethodType SortingMethod { get; set; } = SortingMethodType.Alphabetical;
}