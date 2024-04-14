using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Settings;

internal class CharacterDetailsSettings
{
    [JsonIgnore] public const string Key = "CharacterDetailsSettings";
    public ModSortingMethods? DefaultSortColumn { get; set; } = ModSortingMethods.IsEnabled;
    public bool DefaultSortDescending { get; set; } = false;

    public bool SingleEnableMode { get; set; } = false;
}

public enum ModSortingMethods
{
    IsEnabled,
    Name,
    ModFolder,
    Author,
    Date
}