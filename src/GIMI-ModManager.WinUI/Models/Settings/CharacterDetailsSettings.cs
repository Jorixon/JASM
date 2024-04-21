using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Settings;

public class CharacterDetailsSettings
{
    [JsonIgnore] public const string Key = "CharacterDetailsSettings";

    public bool GalleryView { get; set; } = false;
}