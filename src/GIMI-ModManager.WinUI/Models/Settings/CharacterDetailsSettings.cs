using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Settings;

public class CharacterDetailsSettings
{
    [JsonIgnore] public const string Key = "CharacterDetailsSettings";

    public bool GalleryView { get; set; } = false;

    public string? SortingMethod { get; set; }
    public bool? SortByDescending { get; set; }
}