using System.Diagnostics.CodeAnalysis;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Helpers;
using Serilog;

namespace GIMI_ModManager.Core.Services;

public class UserPreferencesService(ILogger logger, ISkinManagerService skinManagerService)
{
    private readonly ILogger _logger = logger.ForContext<UserPreferencesService>();
    private readonly ISkinManagerService _skinManagerService = skinManagerService;

    private DirectoryInfo _threeMigotoFolder = null!;
    private DirectoryInfo _activeModsFolder = null!;
    private const string D3DX_USER_INI = "d3dx_user.ini";


    public Task InitializeAsync()
    {
        _threeMigotoFolder = new DirectoryInfo(_skinManagerService.ThreeMigotoRootfolder);
        _activeModsFolder = new DirectoryInfo(_skinManagerService.ActiveModsFolderPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Saves the mod preferences to the mod settings file
    /// This overrides the existing preferences in the mod settings file
    /// 3Dmigoto should do a refresh (F10) so that it store the new preferences in the d3dx_user.ini
    /// And we save the mod preferences to the mod settings files
    /// </summary>
    public async Task SaveModPreferencesAsync()
    {
        if (!_threeMigotoFolder.Exists)
            return;

        var d3dxUserIni = new FileInfo(Path.Combine(_threeMigotoFolder.FullName, D3DX_USER_INI));
        if (!d3dxUserIni.Exists)
        {
            _logger.Debug("d3dx_user.ini does not exist in 3DMigoto folder");
            return;
        }

        var lines = await File.ReadAllLinesAsync(d3dxUserIni.FullName).ConfigureAwait(false);

        var activeMods = _skinManagerService.GetAllMods(GetOptions.Enabled);

        foreach (var characterSkinEntry in activeMods)
        {
            var modSettings = await characterSkinEntry.Mod.Settings.TryReadSettingsAsync(true).ConfigureAwait(false);
            if (modSettings is null)
                continue;

            var existingModPref = FindExistingModPref(_activeModsFolder.FullName, lines, characterSkinEntry);


            var keyValues = existingModPref
                .Where(x => x.HasKeyValue || x.KeyValuePair is not null)
                .Select(x => x.KeyValuePair!.Value);

            var pref = new Dictionary<string, string>(keyValues);
            modSettings.SetPreferences(pref);

            await characterSkinEntry.Mod.Settings.SaveSettingsAsync(modSettings).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Overrides the mod preferences in the d3dx_user.ini file with the mod settings preferences
    /// </summary>
    /// <returns></returns>
    public async Task<bool> SetModPreferencesAsync()
    {
        if (!_threeMigotoFolder.Exists)
            return false;

        var d3dxUserIni = new FileInfo(Path.Combine(_threeMigotoFolder.FullName, D3DX_USER_INI));
        if (!d3dxUserIni.Exists)
        {
            _logger.Debug("d3dx_user.ini does not exist in 3DMigoto folder");
            return false;
        }

        var lines = (await File.ReadAllLinesAsync(d3dxUserIni.FullName).ConfigureAwait(false)).ToList();
        var constantSectionIndex =
            lines.IndexOf(lines.FirstOrDefault(x => IniConfigHelpers.IsSection(x, "Constants")) ?? "SomeString");

        if (constantSectionIndex == -1)
        {
            _logger.Debug("Constants section not found in d3dx_user.ini");
            return false;
        }


        var activeMods = _skinManagerService.GetAllMods(GetOptions.Enabled)
            .OrderBy(ske => ske.ModList.Character.InternalName.Id)
            .Where(ske => !ske.Mod.Settings.TryGetSettings(out var modSettings) || modSettings.Preferences.Any());


        foreach (var characterSkinEntry in activeMods)
        {
            var modSettings = await characterSkinEntry.Mod.Settings.TryReadSettingsAsync(true).ConfigureAwait(false);
            if (modSettings is null || !modSettings.Preferences.Any())
                continue;


            var modSettingsPref = modSettings.Preferences
                .Select(kv => CreateUserIniPreference(_activeModsFolder.FullName, characterSkinEntry, kv))
                .Where(pref => pref.HasKeyValue)
                .ToArray();

            var existingModPref = FindExistingModPref(_activeModsFolder.FullName, lines, characterSkinEntry);

            // Remove existing ones for this mode
            var reversedList = existingModPref.ToList();
            reversedList.Reverse();
            foreach (var pref in reversedList)
            {
                lines.RemoveAt(pref.Index);
            }

            // Add new ones from mod settings
            // Insert after the Constants section if no existing mod preferences found
            // 3Dmigoto seems to sort them alphabetically when adding entries, but it doesn't seem to matter if we add without alphabetical order

            var i = existingModPref.FirstOrDefault()?.Index ?? constantSectionIndex + 2;
            foreach (var iniPreference in modSettingsPref)
            {
                lines.Insert(i, iniPreference);
            }
        }

        await File.WriteAllLinesAsync(d3dxUserIni.FullName, lines).ConfigureAwait(false);

        return true;
    }

    private List<IniPreference> FindExistingModPref(string rootModFolderPath, ICollection<string> lines,
        CharacterSkinEntry skinEntry)
    {
        // => $\Mods\Character\dehya\modfolder\
        var modNameSpace = CreateUserIniPreference(rootModFolderPath, skinEntry);

        var modIndexes = new List<IniPreference>();


        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines.ElementAt(i);

            if (line.StartsWith(modNameSpace, StringComparison.OrdinalIgnoreCase))
            {
                var keyValue = line.Replace(modNameSpace, "").Split("=", StringSplitOptions.TrimEntries);

                if (keyValue.Length != 2)
                    continue;

                modIndexes.Add(CreateUserIniPreference(rootModFolderPath, skinEntry,
                    new KeyValuePair<string, string>(keyValue[0], keyValue[1])));

                modIndexes.Last().Index = i;
            }
        }

        return modIndexes;
    }

    private IniPreference CreateUserIniPreference(string rootModFolderPath, CharacterSkinEntry skinEntry,
        KeyValuePair<string, string>? keyValueTuple = null)
    {
        var separator = Path.DirectorySeparatorChar;
        rootModFolderPath = rootModFolderPath.TrimEnd(separator);


        // => $\Mods\
        var rootPath = "$" + separator + rootModFolderPath.Split(separator).Last() +
                       separator;

        return new IniPreference(rootPath,
            skinEntry.ModList.Character.ModCategory.InternalName,
            skinEntry.ModList.Character.InternalName,
            skinEntry.Mod.Name,
            keyValueTuple);
    }

    internal class IniPreference : IEquatable<IniPreference>
    {
        public int Index { get; set; } = -1;
        public string FullPath { get; }
        public string Category { get; }
        public string Character { get; }
        public string ModName { get; }

        public KeyValuePair<string, string>? KeyValuePair;

        public IniPreference(
            string modRoot,
            string category,
            string character,
            string modName,
            KeyValuePair<string, string>? keyValueTuple = null)
        {
            Category = category.ToLower();
            Character = character.ToLower();
            ModName = modName.ToLower();
            KeyValuePair = keyValueTuple is null
                ? null
                : new KeyValuePair<string, string>(keyValueTuple.Value.Key.ToLower(),
                    keyValueTuple.Value.Value.ToLower());

            var separator = Path.DirectorySeparatorChar;


            FullPath = modRoot + category + separator + character + separator + modName + separator;
            if (keyValueTuple is not null)
                FullPath += $"{keyValueTuple.Value.Key} = {keyValueTuple.Value.Value}";

            FullPath = FullPath.ToLower();
        }

        public override string ToString() => FullPath;

        public static implicit operator string(IniPreference iniPreference) => iniPreference.FullPath;

        [MemberNotNullWhen(true, nameof(KeyValuePair))]
        public bool HasKeyValue => KeyValuePair is not null;

        public bool KeyEquals(string key) => KeyValuePair?.Key.Equals(key, StringComparison.OrdinalIgnoreCase) ?? false;

        public bool ValueEquals(string value) =>
            KeyValuePair?.Value.Equals(value, StringComparison.OrdinalIgnoreCase) ?? false;

        public bool Equals(IniPreference? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return FullPath.Equals(other.FullPath, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((IniPreference)obj);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(FullPath, StringComparer.OrdinalIgnoreCase);
            return hashCode.ToHashCode();
        }

        public static bool operator ==(IniPreference? left, IniPreference? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(IniPreference? left, IniPreference? right)
        {
            return !Equals(left, right);
        }
    }
}