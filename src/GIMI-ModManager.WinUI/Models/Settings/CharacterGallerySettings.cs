using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Settings;

public class CharacterGallerySettings
{
    [JsonIgnore] public const string Key = "CharacterGallerySettings";

    public int ItemHeight { get; set; } = 300;

    public int ItemDesiredWidth { get; set; } = 500;

    public bool IsSingleSelection { get; set; } = true;

    public bool IsNavPaneOpen { get; set; } = false;

    public bool CanDeleteDialogPrompt { get; set; } = true;
}