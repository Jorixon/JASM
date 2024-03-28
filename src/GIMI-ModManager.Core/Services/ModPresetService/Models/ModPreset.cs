using GIMI_ModManager.Core.Services.ModPresetService.JsonModels;

namespace GIMI_ModManager.Core.Services.ModPresetService.Models;

public class ModPreset
{
    private ModPreset(string name, IEnumerable<ModPresetEntry>? mods = null)
    {
        Name = name;
        if (mods != null)
            _mods.AddRange(mods);
    }

    public string Name { get; internal set; }
    private readonly List<ModPresetEntry> _mods = [];
    public IReadOnlyList<ModPresetEntry> Mods => _mods;
    public int Index { get; internal set; }
    public DateTime Created { get; init; } = DateTime.Now;


    internal void AddMods(IEnumerable<ModPresetEntry> mods)
    {
        _mods.AddRange(mods);
    }


    internal static ModPreset Create(string name, int index)
    {
        return new ModPreset(name)
        {
            Index = index
        };
    }


    internal static ModPreset FromJson(string name, JsonModPreset json)
    {
        return new ModPreset(name, json.Mods.Select(ModPresetEntry.FromJson))
        {
            Index = json.Index,
            Created = json.Created
        };
    }


    internal JsonModPreset ToJson()
    {
        return new JsonModPreset
        {
            Index = Index,
            Mods = _mods.Select(x => x.ToJson()).ToList()
            Created = Created,
        };
    }
}