using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Options;

public class CharacterOverviewOptions
{
    [JsonIgnore] public const string Key = "CharacterOverviewOptions";

    public int[] PinedCharacters { get; set; } = Array.Empty<int>();
    public int[] HiddenCharacters { get; set; } = Array.Empty<int>();
    public int[] IgnoreMultipleModsWarning { get; set; } = Array.Empty<int>();
    public bool ShowOnlyCharactersWithMods { get; set; } = false;
}