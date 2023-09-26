using System.Text.Json;
using System.Text.Json.Serialization;
using GIMI_ModManager.Core.Contracts.Entities;

namespace GIMI_ModManager.Core.Services;

public class ModListManager
{
    private string _modListPath = null!;

    private readonly List<ModList> _modLists = new();

    public IReadOnlyList<ModList> ModLists => _modLists;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public record Errors(string[] ErrorMessages);

    internal async Task<Errors?> InitializeModLists(string localSettingsPath)
    {
        _modListPath = Path.Combine(localSettingsPath, "ModLists");

        var modLists = new List<ModList>();
        var modListDir = new DirectoryInfo(_modListPath);
        if (!modListDir.Exists)
            return null;

        var errors = new List<string>();
        foreach (var file in modListDir.EnumerateFiles("*.json"))
        {
            ModList? modList = null;
            try
            {
                modList = JsonSerializer.Deserialize<ModList>(await File.ReadAllTextAsync(file.FullName),
                    _jsonSerializerOptions);
            }
            catch (Exception e)
            {
                errors.Add($"Failed to deserialize mod list {file.Name}: {e.Message}");
            }

            if (modList is null)
                continue;
            modLists.Add(modList);
        }

        return errors.Count > 0 ? new Errors(errors.ToArray()) : null;
    }

    internal async Task SaveModList(ModList modList)
    {
        var modListDir = new DirectoryInfo(_modListPath);
        if (!modListDir.Exists)
            modListDir.Create();

        var modListFileName =
            string.IsNullOrWhiteSpace(modList.DisplayName) ? modList.Id.ToString() : modList.DisplayName;


        var file = new FileInfo(Path.Combine(modListDir.FullName, $"{modListFileName}.json"));
        await File.WriteAllTextAsync(file.FullName, JsonSerializer.Serialize(modList, _jsonSerializerOptions));
    }

    public async Task<ModList> CreateModList(string displayName)
    {
        var modList = new ModList { Id = _modLists.Count, DisplayName = displayName };
        _modLists.Add(modList);
        await SaveModList(modList);
        return modList;
    }
}

public class ModList
{
    public int Id { get; init; }
    public string DisplayName { get; set; } = string.Empty;

    private readonly List<IModListItem> _mods = new();

    public IReadOnlyList<IModListItem> Mods => _mods;

    internal void AddMod(IModListItem mod)
    {
        if (_mods.Contains(mod))
            return;
        _mods.Add(mod);
    }

    internal void RemoveMod(IModListItem mod)
    {
        if (!_mods.Contains(mod))
            return;
        _mods.Remove(mod);
    }

    internal bool ReplaceMod(IModListItem oldMod, IModListItem newMod)
    {
        if (!_mods.Contains(oldMod))
            return false;
        _mods[_mods.IndexOf(oldMod)] = newMod;
        return true;
    }
}

public sealed class ModListItem : IModListItem, IEqualityComparer<ModListItem>
{
    private readonly ISkinMod _mod;

    internal ModListItem(ISkinMod mod)
    {
        _mod = mod;
    }

    public string Path => _mod.FullPath;
    [JsonIgnore] public string Name => _mod.Name;
    public string DisplayName => _mod.CustomName;


    public bool Equals(IModListItem? x, IModListItem? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return string.Equals(x.Path, y.Path, StringComparison.CurrentCultureIgnoreCase);
    }

    public int GetHashCode(IModListItem obj)
    {
        return StringComparer.CurrentCultureIgnoreCase.GetHashCode(obj.Path);
    }

    public bool Equals(ModListItem? x, ModListItem? y)
    {
        return Equals(x as IModListItem, y as IModListItem);
    }

    public int GetHashCode(ModListItem obj)
    {
        return GetHashCode(obj as IModListItem);
    }
}

public interface IModListItem : IEqualityComparer<IModListItem>
{
    /// <summary>
    /// Absolute path to the mod folder
    /// </summary>
    string Path { get; }

    /// <summary>
    /// Name of the mod, the folder name
    /// </summary>
    [JsonIgnore]
    string Name { get; }

    /// <summary>
    /// Custom user set display name for the mod
    /// </summary>
    [JsonIgnore]
    string DisplayName { get; }
}