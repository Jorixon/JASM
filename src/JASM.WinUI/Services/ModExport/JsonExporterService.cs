using System.Collections.Concurrent;
using System.Reflection;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Services.ModPresetService;
using GIMI_ModManager.Core.Services.ModPresetService.Models;

namespace GIMI_ModManager.WinUI.Services.ModExport;

public class JsonExporterService(ISkinManagerService skinManagerService, ModPresetService modPresetService)
{
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly ModPresetService _modPresetService = modPresetService;


    public async Task<JsonExportRoot> CreateExportJsonAsync()
    {
        var json = new JsonExportRoot
        {
            Version = "1.0",
            JASMVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        };

        var presets = _modPresetService.GetPresets();

        var presetJson = new List<JsonExportPresetInfo>();

        foreach (var modPreset in presets)
        {
            presetJson.Add(JsonExportPresetInfo.Create(modPreset));
        }

        await _skinManagerService.RefreshModsAsync().ConfigureAwait(false);

        var allModLists = _skinManagerService.CharacterModLists;

        var allMods = allModLists.SelectMany(x => x.Mods).ToList();

        var jsonModInfoList = new ConcurrentBag<JsonExportModInfo>();

        await Parallel.ForEachAsync(allMods, async (modEntry, token) =>
        {
            var modSettings = await modEntry.Mod.Settings.TryReadSettingsAsync(true, token).ConfigureAwait(false);

            var modInfo = JsonExportModInfo.Create(modEntry.ModList.Character, modEntry, modSettings);
            jsonModInfoList.Add(modInfo);
        }).ConfigureAwait(false);


        json.Presets = presetJson.OrderBy(p => p.Name).ToArray();
        json.Mods = jsonModInfoList.OrderBy(m => m.Id).ToArray();

        return json;
    }
}

public class JsonExportRoot
{
    public string? Version { get; set; }
    public string? JASMVersion { get; set; }
    public JsonExportModInfo[] Mods { get; set; } = [];

    public JsonExportPresetInfo[] Presets { get; set; } = [];
}

public class JsonExportPresetInfo
{
    public static JsonExportPresetInfo Create(ModPreset preset)
    {
        return new JsonExportPresetInfo
        {
            Name = preset.Name,
            Created = preset.Created.ToString("O"),
            IsReadOnly = preset.IsReadOnly,
            Index = preset.Index,
            Mods = preset.Mods.Select(JsonExportPresetModInfo.Create).ToArray()
        };
    }

    public int Index { get; set; }

    public bool IsReadOnly { get; set; }

    public string? Created { get; set; }

    public string? Name { get; set; }

    public JsonExportPresetModInfo[] Mods { get; set; } = [];
}

public class JsonExportPresetModInfo
{
    public static JsonExportPresetModInfo Create(ModPresetEntry presetEntry)
    {
        return new JsonExportPresetModInfo
        {
            ModId = presetEntry.ModId,
            FolderPath = presetEntry.FullPath,
            ModUrl = presetEntry.SourceUrl?.ToString(),
            AddedAt = presetEntry.AddedAt?.ToString("O"),
            IsMissing = presetEntry.IsMissing,
            CustomName = presetEntry.CustomName,
            Name = presetEntry.Name
        };
    }

    public string? Name { get; set; }

    public string? CustomName { get; set; }

    public bool IsMissing { get; set; }

    public string? AddedAt { get; set; }

    public string? ModUrl { get; set; }

    public string? FolderPath { get; set; }

    public Guid ModId { get; set; }
}

public class JsonExportModInfo
{
    public static JsonExportModInfo Create(IModdableObject moddableObject, CharacterSkinEntry mod, ModSettings? modSettings)
    {
        return new JsonExportModInfo
        {
            Id = mod.Mod.Id,
            CustomName = modSettings?.CustomName,
            FolderPath = mod.Mod.FullPath,
            DateAdded = modSettings?.DateAdded?.ToString("O"),
            Author = modSettings?.Author,
            Description = modSettings?.Description,
            CharacterSkinOverride = modSettings?.CharacterSkinOverride,
            ModUrl = modSettings?.ModUrl?.ToString(),
            ImagePath = modSettings?.ImagePath?.ToString(),
            ModdableObjectInternalName = moddableObject.InternalName,
            ModdableObjectDisplayName = moddableObject.DisplayName,
            ModdableObjectCategory = moddableObject.ModCategory.InternalName
        };
    }

    public string? ImagePath { get; set; }

    public string? ModUrl { get; set; }

    public string? CharacterSkinOverride { get; set; }

    public string? ModdableObjectCategory { get; set; }

    public string? ModdableObjectDisplayName { get; set; }

    public string? ModdableObjectInternalName { get; set; }

    public string? Description { get; set; }

    public string? Author { get; set; }

    public string? DateAdded { get; set; }

    public string? FolderPath { get; set; }

    public string? CustomName { get; set; }

    public Guid Id { get; set; }
}