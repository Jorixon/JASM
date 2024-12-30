using GIMI_ModManager.Core.Contracts.Entities;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

public class ModModel(string displayName, string dateAdded)
{
    public static ModModel FromMod(ISkinMod mod)
    {
        var dateAdded = mod.Settings.TryGetSettings(out var settings) && settings.DateAdded.HasValue ? settings.DateAdded.Value.ToShortDateString() : "Unknown";
        return new ModModel(mod.GetDisplayName(), dateAdded);
    }

    public string DisplayName { get; } = displayName;
    public string DateAdded { get; } = dateAdded;
}