using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Settings;

public class CharacterDetailsSettings
{
    [JsonIgnore] public const string Key = "CharacterDetailsSettings";

    public bool GalleryView { get; set; } = false;

    public bool LegacyCharacterDetails { get; set; } = false;

    public bool SingleSelect { get; set; }
    public string? SortingMethod { get; set; }
    public bool? SortByDescending { get; set; }
    public bool ModFolderNameColumnVisible { get; set; }
}