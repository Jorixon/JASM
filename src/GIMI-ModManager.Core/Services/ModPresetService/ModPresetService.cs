using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Entities.Mods.Exceptions;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services.ModPresetService.JsonModels;
using GIMI_ModManager.Core.Services.ModPresetService.Models;
using Serilog;

namespace GIMI_ModManager.Core.Services.ModPresetService;

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
    private const string PresetDirectoryName = "Presets";

    private readonly AsyncLock _asyncLock = new();
    private readonly List<ModPreset> _presets = new();

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private bool _isInitialized;

    public IEnumerable<ModPreset> GetPresets() => _presets;


    public async Task InitializeAsync(string appDataFolder)
    {
        if (_isInitialized)
            return;

        _settingsDirectory = new DirectoryInfo(appDataFolder);
        _settingsDirectory.Create();

        _presetDirectory = new DirectoryInfo(Path.Combine(appDataFolder, PresetDirectoryName));
        _presetDirectory.Create();


        foreach (var jsonPreset in _presetDirectory.EnumerateFiles("*.json"))
        {
            try
            {
                var preset =
                    JsonSerializer.Deserialize<JsonModPreset>(await File.ReadAllTextAsync(jsonPreset.FullName)
                        .ConfigureAwait(false));

                if (preset is not null)
                    _presets.Add(ModPreset.FromJson(Path.GetFileNameWithoutExtension(jsonPreset.Name), preset));
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to load preset from {PresetPath}", jsonPreset.FullName);
            }
        }

        _logger.Debug("Loaded {PresetCount} presets", _presets.Count);
        _isInitialized = true;
    }


    public async Task CreatePresetAsync(string name, bool createEmptyPreset = true)
    {
        using var _ = await LockAsync().ConfigureAwait(false);
        name = name.Trim();

        AssertIsValidPresetName(name);


        var nextIndex = GetNextPresetIndex();
        var preset = ModPreset.Create(name, nextIndex);
        _presets.Add(preset);

        var modEntries = createEmptyPreset
            ? new List<ModPresetEntry>()
            : await GetActiveModsAsModEntriesAsync().ConfigureAwait(false);


        preset.AddMods(modEntries);

        await WritePresetsAsync().ConfigureAwait(false);
    }

    //public async Task SaveActiveModsToPresetAsync(string presetName)
    //{
    //    using var _ = await LockAsync().ConfigureAwait(false);

    //    var preset = GetFirstModPreset(presetName);

    //    var modEntries = await GetActiveModsAsModEntriesAsync().ConfigureAwait(false);

    //    preset._mods.Clear();
    //    preset._mods.AddRange(modEntries);

    //    await WritePresetsAsync().ConfigureAwait(false);
    //}

    public async Task DeleteModEntryAsync(string presetName, Guid modId, CancellationToken cancellationToken = default)
    {
        using var _ = await LockAsync(cancellationToken).ConfigureAwait(false);

        var preset = GetFirstModPreset(presetName);
        AssertIsEditable(preset.Name);

        var modEntry = preset.Mods.FirstOrDefault(m => m.ModId == modId);
        if (modEntry != null)
        {
            preset.RemoveMods([modEntry]);
        }

        await WritePresetsAsync().ConfigureAwait(false);
    }

    public async Task AddModEntryAsync(string presetName, Guid modId, CancellationToken cancellationToken = default)
    {
        using var _ = await LockAsync(cancellationToken).ConfigureAwait(false);

        var preset = GetFirstModPreset(presetName);
        AssertIsEditable(preset.Name);

        throw new NotImplementedException();
    }

    public async Task ToggleReadOnlyAsync(string presetName)
    {
        using var _ = await LockAsync().ConfigureAwait(false);

        var preset = GetFirstModPreset(presetName);
        preset.IsReadOnly = !preset.IsReadOnly;

        await WritePresetsAsync().ConfigureAwait(false);
    }

    public async Task DeletePresetAsync(string presetName)
    {
        using var _ = await LockAsync().ConfigureAwait(false);

        var preset = GetFirstModPreset(presetName);
        AssertIsEditable(preset.Name);

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

        AssertIsEditable(preset.Name);

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
            var mod = ResolveModFromPresetEntry(allMods, modEntry);

            if (mod is not null)
            {
                modEntryToMod.Add(modEntry, mod);
                modEntry.IsMissing = false;
                continue;
            }

            _logger.Warning("Could not find mod with Id {ModEntryId} at path {ModEntryPath}", modEntry.Name,
                modEntry.FullPath);

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
        var newPreset = ModPreset.Create(preset.Name + " (Copy)", nextIndex);

        AssertIsValidPresetName(newPreset.Name);

        newPreset.AddMods(preset.Mods.Select(m => m.Duplicate()));

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
        foreach (var preset in _presets)
        {
            var presetPath = GetPresetPath(preset.Name);
            var json = JsonSerializer.Serialize(preset.ToJson(), _jsonSerializerOptions);
            await File.WriteAllTextAsync(presetPath, json).ConfigureAwait(false);
        }


        foreach (var presetFile in _presetDirectory.EnumerateFiles())
        {
            if (_presets.All(p =>
                    !p.Name.Equals(Path.GetFileNameWithoutExtension(presetFile.Name),
                        StringComparison.OrdinalIgnoreCase)))
            {
                presetFile.Delete();
            }
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

    /// <summary>
    /// Throws ArgumentException if the preset does not exist
    /// </summary>
    private ModPreset GetFirstModPreset(string presetName) =>
        _presets.FirstOrDefault(p => p.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase)) ??
        throw new ArgumentException($"Preset with name {presetName} not found", nameof(presetName));

    private bool IsValidPresetName([NotNullWhen(true)] string? name) =>
        !name.IsNullOrEmpty() && name.IndexOfAny(Path.GetInvalidFileNameChars()) == -1 &&
        !_presets.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private void AssertIsValidPresetName([NotNull] string? name)
    {
        if (!IsValidPresetName(name))
            throw new InvalidOperationException(
                $"The preset name '{name}' cannot be empty and must be unique among preset names");
    }

    private void AssertIsEditable(string presetName)
    {
        var preset = GetFirstModPreset(presetName);

        if (preset.IsReadOnly)
            throw new InvalidOperationException($"Preset '{presetName}' is set to read-only");
    }

    // To avoid race conditions, we lock all the preset methods
    // There is no big need for parallelism here, so we just lock the whole service
    private Task<LockReleaser> LockAsync(CancellationToken cancellationToken = default) =>
        _asyncLock.LockAsync(TimeSpan.FromSeconds(2), cancellationToken);

    public void Dispose() => _asyncLock.Dispose();


    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        using var _ = await LockAsync(cancellationToken).ConfigureAwait(false);

        var allMods = _skinManagerService.GetAllMods(GetOptions.All);

        var anyChanges = false;

        foreach (var preset in _presets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var mod in preset.Mods)
            {
                var characterSkinEntry = ResolveModFromPresetEntry(allMods, mod);

                if (characterSkinEntry is null)
                {
                    _logger.Information("Could not resolve mod preset entry at path {ModFullPath}", mod.FullPath);

                    mod.IsMissing = true;
                    anyChanges = true;
                    continue;
                }

                if (characterSkinEntry.Id != mod.ModId)
                {
                    _logger.Information(
                        "Resolved mod preset entry {ModFullPath} to {CharacterSkinEntryFullPath}, updating preset modId",
                        mod.FullPath,
                        characterSkinEntry.Mod.FullPath);

                    mod.ModId = characterSkinEntry.Id;
                    anyChanges = true;
                    continue;
                }

                mod.IsMissing = false;
            }
        }

        if (anyChanges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WritePresetsAsync().ConfigureAwait(false);
        }
    }

    private CharacterSkinEntry? ResolveModFromPresetEntry(ICollection<CharacterSkinEntry> mods,
        ModPresetEntry modPresetEntry)
    {
        var mod = mods.FirstOrDefault(m => m.Id == modPresetEntry.ModId);

        if (mod is not null)
            return mod;

        mod = mods.FirstOrDefault(m => m.Mod.FullPath.AbsPathCompare(modPresetEntry.FullPath));

        if (mod is not null)
            return mod;

        mod = mods.FirstOrDefault(m => ModFolderHelpers.AbsModFolderCompare(m.Mod.FullPath, modPresetEntry.FullPath));

        return mod;


        // TODO: Try to parse category, character and mod name from the path to be completely sure
        //var modByName = mods
        //    .Where(ske => ModFolderHelpers.FolderNameEquals(ske.Mod.Name, modPresetEntry.Name)).ToArray();

        //if (modByName.Length == 1)
        //    return modByName[0];

        //return null;
    }
}