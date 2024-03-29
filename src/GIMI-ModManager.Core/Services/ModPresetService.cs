﻿using System.Diagnostics.CodeAnalysis;
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

    private DirectoryInfo _settingsDirectory = null!;
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

        _settingsDirectory = new DirectoryInfo(appDataFolder);
        _settingsDirectory.Create();

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
                    var modList = new ModPreset(Path.GetFileNameWithoutExtension(jsonPreset.Name), preset);
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

        AssertIsValidPresetName(name);


        var nextIndex = GetNextPresetIndex();
        var preset = new ModPreset(name)
        {
            Index = nextIndex
        };
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

        AssertIsValidPresetName(newName);

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

            mod = allMods.FirstOrDefault(m => ModFolderHelpers.AbsModFolderCompare(m.Mod.FullPath, modEntry.FullPath));


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

                // Set Preset preferences for mod

                var modEntry = modEntryToMod.First(ske => ske.Value.Id == characterSkinEntry.Id).Key;

                ModSettings? modSettings;

                try
                {
                    modSettings = await characterSkinEntry.Mod.Settings
                        .TryReadSettingsAsync(useCache: false)
                        .ConfigureAwait(false);


                    if (modSettings is null)
                        throw new ModSettingsNotFoundException(
                            $"Could not read mod settings for {characterSkinEntry.Mod.FullPath}");
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Failed to read mod settings for {ModFullPath}",
                        characterSkinEntry.Mod.FullPath);
                    continue;
                }

                modSettings.SetPreferences(modEntry.Preferences is not null
                    ? new Dictionary<string, string>(modEntry.Preferences)
                    : null);

                await characterSkinEntry.Mod.Settings.SaveSettingsAsync(modSettings).ConfigureAwait(false);
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
    }


    public async Task DuplicatePresetAsync(string presetName)
    {
        using var _ = await LockAsync().ConfigureAwait(false);

        var preset = GetFirstModPreset(presetName);

        var nextIndex = GetNextPresetIndex();
        var newPreset = new ModPreset(preset.Name + " (Copy)") { Index = nextIndex };

        AssertIsValidPresetName(newPreset.Name);

        newPreset._mods.AddRange(preset.Mods);

        _presets.Add(newPreset);

        await WritePresetsAsync().ConfigureAwait(false);
    }

    public async Task SavePresetOrderAsync(IEnumerable<string> presetNamesOrder)
    {
        using var _ = await LockAsync().ConfigureAwait(false);

        var presetOrder = presetNamesOrder.ToList();

        if (presetOrder.Count != _presets.Count)
            throw new ArgumentException("Preset order count does not match preset count");

        var index = 0;
        foreach (var presetName in presetOrder)
        {
            var preset = GetFirstModPreset(presetName);
            preset.Index = index;
            index++;
        }

        await WritePresetsAsync().ConfigureAwait(false);
    }


    private async Task WritePresetsAsync()
    {
        _presetDirectory.EnumerateFiles().ForEach(f => f.Delete());

        foreach (var preset in _presets.ToArray())
        {
            var jsonPreset = new JsonModPreset
                { Mods = preset.Mods.ToList(), Index = preset.Index, Created = preset.Created };
            await File.WriteAllTextAsync(GetPresetPath(preset.Name),
                JsonSerializer.Serialize(jsonPreset, _jsonSerializerOptions)).ConfigureAwait(false);
        }
    }

    private int GetNextPresetIndex() => _presets.Count == 0 ? 0 : _presets.Max(p => p.Index) + 1;

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

    private bool IsValidPresetName(string name) =>
        !name.IsNullOrEmpty() && name.IndexOfAny(Path.GetInvalidFileNameChars()) == -1 &&
        !_presets.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private void AssertIsValidPresetName([NotNull] string? name)
    {
        if (!IsValidPresetName(name ?? ""))
            throw new InvalidOperationException(
                $"The preset name '{name}' cannot be empty and must be unique among preset names");
    }

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

public class ModPreset
{
    internal ModPreset(string name, JsonModPreset json)
    {
        Name = name;
        _mods.AddRange(json.Mods);
        Index = json.Index;
        Created = json.Created;
    }

    internal ModPreset(string name)
    {
        Name = name;
    }

    public string Name { get; internal set; }
    internal readonly List<ModPresetEntry> _mods = [];
    public IReadOnlyList<ModPresetEntry> Mods => _mods;
    public int Index { get; internal set; }
    public DateTime Created { get; internal set; } = DateTime.Now;
}

internal class JsonModPreset
{
    public DateTime Created { get; set; } = DateTime.Now;
    public int Index { get; set; }
    public List<ModPresetEntry> Mods { get; set; } = new();
}