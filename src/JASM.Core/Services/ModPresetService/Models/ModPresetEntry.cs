using System.Globalization;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services.ModPresetService.JsonModels;

namespace GIMI_ModManager.Core.Services.ModPresetService.Models;

public class ModPresetEntry
{
    public Guid ModId { get; internal set; }

    public required string FullPath { get; init; }

    /// <summary>
    /// If the mod is missing from the disk and/or could not be resolved by ModId or FullPath.
    /// </summary>
    public bool IsMissing { get; set; }

    public string? CustomName { get; init; }

    public Uri? SourceUrl { get; init; }

    public string Name => ModFolderHelpers.GetFolderNameWithoutDisabledPrefix(new DirectoryInfo(FullPath).Name);


    public IReadOnlyDictionary<string, string>? Preferences { get; private set; }

    public DateTime? AddedAt { get; internal set; }


    private ModPresetEntry()
    {
    }


    internal ModPresetEntry Duplicate()
    {
        return new ModPresetEntry
        {
            ModId = ModId,
            FullPath = FullPath,
            CustomName = CustomName,
            SourceUrl = SourceUrl,
            Preferences = Preferences,
            AddedAt = AddedAt
        };
    }

    internal static ModPresetEntry FromSkinMod(ISkinMod skinMod, ModSettings settings)
    {
        return new ModPresetEntry
        {
            ModId = skinMod.Id,
            CustomName = settings.CustomName,
            FullPath = skinMod.FullPath,
            Preferences = settings.Preferences.Count == 0 ? null : settings.Preferences,
            SourceUrl = settings.ModUrl,
            AddedAt = DateTime.Now
        };
    }

    internal void UpdatePreferences(IEnumerable<KeyValuePair<string, string>>? preferences)
    {
        Preferences = preferences?.ToDictionary();
    }

    internal static ModPresetEntry FromJson(JsonModPresetEntry json)
    {
        return new ModPresetEntry
        {
            ModId = json.ModId,
            CustomName = json.CustomName,
            FullPath = json.FullPath,
            Preferences = json.Preferences,
            SourceUrl = Uri.TryCreate(json.SourceUrl, UriKind.Absolute, out var uri) ? uri : null,
            AddedAt = DateTime.TryParse(json.AddedAt, out var date) ? date : DateTime.Now,
            IsMissing = json.IsMissing
        };
    }

    internal JsonModPresetEntry ToJson()
    {
        return new JsonModPresetEntry
        {
            ModId = ModId,
            CustomName = CustomName,
            FullPath = FullPath,
            Preferences = Preferences?.ToDictionary() ?? new Dictionary<string, string>(),
            SourceUrl = SourceUrl?.ToString(),
            AddedAt =
                AddedAt?.ToString(CultureInfo.CurrentCulture) ?? DateTime.Now.ToString(CultureInfo.CurrentCulture),
            IsMissing = IsMissing
        };
    }
}