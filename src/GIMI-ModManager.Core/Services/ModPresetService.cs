using System.Text.Json;
using System.Text.Json.Serialization;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Entities.Mods.Exceptions;
using GIMI_ModManager.Core.Helpers;
using Serilog;

namespace GIMI_ModManager.Core.Services;

public sealed class ModPresetService(
    ILogger logger,
    ISkinManagerService skinManagerService,
    UserPreferencesService userPreferencesService) : IDisposable
{
    private readonly ILogger _logger = logger.ForContext<ModPresetService>();
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly UserPreferencesService _userPreferencesService = userPreferencesService;

    private DirectoryInfo _modPresetDirectory = null!;
    private DirectoryInfo _presetDirectory = null!;

    private readonly AsyncLock _asyncLock = new();
    private readonly List<ModPreset> _presets = new();

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private bool _isInitialized;

    public async Task InitializeAsync(string appDataFolder)
    {
        if (_isInitialized)
            return;

        _modPresetDirectory = new DirectoryInfo(appDataFolder);
        _modPresetDirectory.Create();

        _presetDirectory = new DirectoryInfo(Path.Combine(appDataFolder, "Presets"));
        _presetDirectory.Create();


        foreach (var jsonPreset in _presetDirectory.EnumerateFiles("*.json"))
        {
            try
            {
                var preset =
                    JsonSerializer.Deserialize<JsonModPreset>(await File.ReadAllTextAsync(jsonPreset.FullName)
                        .ConfigureAwait(false));

                if (preset is not null)
                {
                    var modList = new ModPreset(Path.GetFileNameWithoutExtension(jsonPreset.Name));
                    modList._mods.AddRange(preset.Mods);
                    _presets.Add(modList);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to load preset from {PresetPath}", jsonPreset.FullName);
            }
        }

        _logger.Debug("Loaded {PresetCount} presets", _presets.Count);
        _isInitialized = true;
    }

    public IEnumerable<ModPreset> GetPresets() => _presets;

    public async Task CreatePresetAsync(string name, bool createEmptyPreset = true)
    {
        using var _ = await LockAsync().ConfigureAwait(false);
        name = name.Trim();
        if (name.IsNullOrEmpty())
            throw new ArgumentException("Name cannot be null or whitespace", nameof(name));

        if (_presets.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Preset with the same name already exists", nameof(name));

        var preset = new ModPreset(name);
        _presets.Add(preset);

        var modEntries = createEmptyPreset
            ? new List<ModPresetEntry>()
            : await GetActiveModsAsModEntriesAsync().ConfigureAwait(false);


        preset._mods.AddRange(modEntries);

        await WritePresetsAsync().ConfigureAwait(false);
    }

    public async Task SaveActiveModsToPresetAsync(string presetName)
    {
        using var _ = await LockAsync().ConfigureAwait(false);

        var preset = GetFirstModPreset(presetName);

        var modEntries = await GetActiveModsAsModEntriesAsync().ConfigureAwait(false);

        preset._mods.Clear();
        preset._mods.AddRange(modEntries);

        await WritePresetsAsync().ConfigureAwait(false);
    }

    public async Task DeletePresetAsync(string presetName)
    {
        using var _ = await LockAsync().ConfigureAwait(false);

        var preset = GetFirstModPreset(presetName);

        _presets.Remove(preset);

        var presetPath = GetPresetPath(presetName);
        if (File.Exists(presetPath))
            File.Delete(presetPath);
    }

    public async Task RenamePresetAsync(string oldName, string newName)
    {
        using var _ = await LockAsync().ConfigureAwait(false);

        oldName = oldName.Trim();
        newName = newName.Trim();

        if (oldName.IsNullOrEmpty())
            throw new ArgumentException("Old name cannot be null or whitespace", nameof(oldName));

        if (newName.IsNullOrEmpty())
            throw new ArgumentException("New name cannot be null or whitespace", nameof(newName));

        if (_presets.Any(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Preset with the same name already exists", nameof(newName));

        var preset = GetFirstModPreset(oldName);
        preset.Name = newName;

        await WritePresetsAsync().ConfigureAwait(false);
    }

    public async Task ApplyPresetAsync(string presetName)
    {
        using var _ = await LockAsync().ConfigureAwait(false);

        var preset = GetFirstModPreset(presetName);

        var allModEntries = preset.Mods;
        var missingMods = new List<ModPresetEntry>();

        var allModLists = _skinManagerService.CharacterModLists.ToArray();
        var allMods = allModLists.SelectMany(c => c.Mods).ToArray();

        var modEntryToMod = new Dictionary<ModPresetEntry, CharacterSkinEntry>();

        foreach (var modEntry in allModEntries)
        {
            var mod = allMods.FirstOrDefault(m => m.Id == modEntry.ModId);

            if (mod is not null)
            {
                modEntryToMod.Add(modEntry, mod);
                continue;
            }

            mod = allMods.FirstOrDefault(m => m.Mod.FullPath.AbsPathCompare(modEntry.FullPath));

            if (mod is not null)
            {
                modEntryToMod.Add(modEntry, mod);
                continue;
            }


            var modByName = allMods
                .Where(ske => ModFolderHelpers.FolderNameEquals(ske.Mod.Name, modEntry.Name)).ToArray();

            if (modByName.Length == 1)
            {
                modEntryToMod.Add(modEntry, modByName[0]);
                continue;
            }

            if (modByName.Length == 0)
            {
                missingMods.Add(modEntry);
                modEntry.IsMissing = true;
                _logger.Warning("Could not find mod with Id {ModEntryId}", modEntry.Name);
                continue;
            }

            _logger.Warning(
                "Could not find mod by Id or fullPath and found at least 2 mods with the same name {ModEntryName}\n\t1. {FirstMod}\n\t2. {SecondMod}",
                modEntry.Name, modByName[0].Mod.FullPath, modByName[1].Mod.FullPath);

            missingMods.Add(modEntry);
            modEntry.IsMissing = true;
        }


        foreach (var characterSkinEntry in allMods)
        {
            var modList = characterSkinEntry.ModList;
            var enableMod = modEntryToMod.Values.Any(ske => ske.Id == characterSkinEntry.Id);

            if (enableMod)
            {
                if (!characterSkinEntry.IsEnabled)
                {
                    modList.EnableMod(characterSkinEntry.Id);
                }
            }
            else
            {
                if (characterSkinEntry.IsEnabled)
                {
                    modList.DisableMod(characterSkinEntry.Id);
                }
            }
        }

        await WritePresetsAsync().ConfigureAwait(false);

        await _userPreferencesService.SetModPreferencesAsync().ConfigureAwait(false);
    }


    private async Task WritePresetsAsync()
    {
        foreach (var preset in _presets.ToArray())
        {
            var jsonPreset = new JsonModPreset { Mods = preset.Mods.ToList() };
            await File.WriteAllTextAsync(GetPresetPath(preset.Name),
                JsonSerializer.Serialize(jsonPreset, _jsonSerializerOptions)).ConfigureAwait(false);
        }
    }

    private async Task<IList<ModPresetEntry>> GetActiveModsAsModEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        var enabledMods = _skinManagerService.GetAllMods(GetOptions.Enabled);

        var modEntries = new List<ModPresetEntry>();

        await Parallel.ForEachAsync(enabledMods, cancellationToken, async (characterSkinEntry, ct) =>
        {
            var modSettings = await characterSkinEntry.Mod.Settings
                .TryReadSettingsAsync(useCache: false, cancellationToken: ct)
                .ConfigureAwait(false);

            if (modSettings is null)
                throw new ModSettingsNotFoundException(
                    $"Could not read mod settings for {characterSkinEntry.Mod.FullPath}");

            var modEntry = ModPresetEntry.FromSkinMod(characterSkinEntry.Mod, modSettings);
            modEntries.Add(modEntry);
        }).ConfigureAwait(false);

        return modEntries;
    }

    private string GetPresetPath(string name) => Path.Combine(_presetDirectory.FullName, $"{name}.json");

    private ModPreset GetFirstModPreset(string presetName) =>
        _presets.FirstOrDefault(p => p.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase)) ??
        throw new ArgumentException($"Preset with name {presetName} not found", nameof(presetName));

    // To avoid race conditions, we lock all the preset methods
    // There is no big need for parallelism here, so we just lock the whole class
    private Task<LockReleaser> LockAsync() => _asyncLock.LockAsync();

    public void Dispose() => _asyncLock.Dispose();
}

public record ModPresetEntry
{
    public required Guid ModId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CustomName { get; init; }

    [JsonIgnore]
    public string Name => ModFolderHelpers.GetFolderNameWithoutDisabledPrefix(new DirectoryInfo(FullPath).Name);

    public required string FullPath { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Preferences { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]

    public bool IsMissing { get; set; }

    public static ModPresetEntry FromSkinMod(ISkinMod skinMod, ModSettings settings)
    {
        return new ModPresetEntry
        {
            ModId = skinMod.Id,
            CustomName = settings.CustomName,
            FullPath = skinMod.FullPath,
            Preferences = settings.Preferences.Count == 0 ? null : settings.Preferences
        };
    }
}

public class ModPreset(string name)
{
    public string Name { get; internal set; } = name;
    internal readonly List<ModPresetEntry> _mods = [];
    public IReadOnlyList<ModPresetEntry> Mods => _mods;
}

internal class JsonModPreset
{
    public List<ModPresetEntry> Mods { get; set; } = new();
}