using GIMI_ModManager.Core.GamesService;

namespace GIMI_ModManager.WinUI.Models.Settings;

public class CharacterOverviewSettings
{
    public static string GetKey(ICategory category) => $"CharacterOverviewOptions_{category.InternalName.Id}";
    public string[] PinedCharacters { get; set; } = Array.Empty<string>();
    public string[] HiddenCharacters { get; set; } = Array.Empty<string>();
    public bool ShowOnlyCharactersWithMods { get; set; } = false;
    public bool ShowOnlyCharactersWithEnabledMods { get; set; } = false;
    public bool SortByDescending { get; set; } = false;
    public string? SortingMethod { get; set; }
    public bool ShowOnlyModsWithNotifications { get; set; }
}