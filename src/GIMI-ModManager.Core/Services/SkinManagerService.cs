#nullable enable
using System.IO.Compression;
using System.Runtime.CompilerServices;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Helpers;
using Serilog;

namespace GIMI_ModManager.Core.Services;

public sealed class SkinManagerService : ISkinManagerService
{
    private readonly IGenshinService _genshinService;
    private readonly ILogger _logger;

    private DirectoryInfo _unloadedModsFolder = null!;
    private DirectoryInfo _activeModsFolder = null!;
    private DirectoryInfo? _threeMigotoFolder;

    private FileSystemWatcher _userIniWatcher = null!;
    private readonly List<ICharacterModList> _characterModLists = new();
    public IReadOnlyCollection<ICharacterModList> CharacterModLists => _characterModLists.AsReadOnly();

    public SkinManagerService(IGenshinService genshinService, ILogger logger)
    {
        _genshinService = genshinService;
        _logger = logger.ForContext<SkinManagerService>();
    }

    public string UnloadedModsFolderPath => _unloadedModsFolder.FullName;
    public string ActiveModsFolderPath => _activeModsFolder.FullName;

    public bool UnloadingModsEnabled { get; private set; }

    public void ScanForMods()
    {
        _activeModsFolder.Refresh();

        var characters = _genshinService.GetCharacters();
        foreach (var character in characters)
        {
            var characterModFolder = new DirectoryInfo(GetCharacterModFolderPath(character));
            if (!characterModFolder.Exists)
            {
                _logger.Warning("Character mod folder for '{Character}' does not exist", character.DisplayName);
                continue;
            }

            var characterModList = new CharacterModList(character, characterModFolder.FullName, logger: _logger);
            _characterModLists.Add(characterModList);

            foreach (var modFolder in characterModFolder.EnumerateDirectories())
            {
                var mod = new SkinMod(modFolder, modFolder.Name);
                characterModList.TrackMod(mod);
            }
        }
    }

    public void RefreshMods(GenshinCharacter? refreshForCharacter = null)
    {
        foreach (var characterModList in _characterModLists)
        {
            if (refreshForCharacter is not null && characterModList.Character.Id != refreshForCharacter.Id) continue;

            var modsDirectory = new DirectoryInfo(characterModList.AbsModsFolderPath);
            modsDirectory.Refresh();

            var orphanedMods = new List<CharacterSkinEntry>(characterModList.Mods);

            foreach (var modDirectory in modsDirectory.EnumerateDirectories())
            {
                CharacterSkinEntry? mod = null;

                foreach (var x in characterModList.Mods)
                {
                    if (x.Mod.FullPath == modDirectory.FullName)
                    {
                        mod = x;
                        mod.Mod.ClearCache();
                        orphanedMods.Remove(x);
                        break;
                    }

                    var disabledName = $"{CharacterModList.DISABLED_PREFIX}{modDirectory.Name}";
                    if (x.Mod.FullPath == Path.Combine(modDirectory.Parent!.FullName, disabledName))
                    {
                        mod = x;
                        mod.Mod.ClearCache();
                        orphanedMods.Remove(x);
                        break;
                    }
                }

                if (mod is not null) continue;

                var newMod = new SkinMod(modDirectory, modDirectory.Name);
                characterModList.TrackMod(newMod);
            }

            orphanedMods.ForEach(x => characterModList.UnTrackMod(x.Mod));
        }
    }

    public void TransferMods(ICharacterModList source, ICharacterModList destination, IEnumerable<Guid> modsEntryIds)
    {
        var mods = source.Mods.Where(x => modsEntryIds.Contains(x.Id)).Select(x => x.Mod).ToList();
        foreach (var mod in mods)
        {
            if (!source.Mods.Select(modEntry => modEntry.Mod).Contains(mod))
                throw new InvalidOperationException(
                    $"Mod {mod.Name} is not in source character mod list {source.Character.DisplayName}");


            if (mods.Select(x => x.Name).Any(destination.FolderAlreadyExists))
                throw new InvalidOperationException(
                    $"Mod {mod.Name} already exists in destination character mod list {destination.Character.DisplayName}");
        }

        _logger.Information("Transferring {ModsCount} mods from '{SourceCharacter}' to '{DestinationCharacter}'",
            mods.Count, source.Character.DisplayName, destination.Character.DisplayName);

        using var sourceDisabled = source.DisableWatcher();
        using var destinationDisabled = destination.DisableWatcher();

        foreach (var mod in mods)
        {
            source.UnTrackMod(mod);
            mod.MoveTo(destination.AbsModsFolderPath);
            destination.TrackMod(mod);
        }
    }


    public void ExportMods(ICollection<ICharacterModList> characterModLists, string exportPath,
        bool removeLocalJasmSettings = true, bool zip = true, bool keepCharacterFolderStructure = false,
        SetModStatus setModStatus = SetModStatus.KeepCurrent)
    {
        if (characterModLists.Count == 0)
            throw new ArgumentException("Value cannot be an empty collection.", nameof(characterModLists));
        ArgumentNullException.ThrowIfNull(exportPath);

        var exportFolderResultName = $"JASM_MOD_EXPORT_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";

        var exportFolder = new DirectoryInfo(Path.Combine(exportPath, exportFolderResultName));
        exportFolder.Create();

        if (exportFolder.EnumerateFileSystemInfos().Any())
            throw new InvalidOperationException("Export folder is not empty");


        var modsToExport = new List<CharacterSkinEntry>();


        foreach (var characterModList in characterModLists)
        {
            modsToExport.AddRange(characterModList.Mods);
        }


        if (!keepCharacterFolderStructure && !zip) // Copy mods unorganized
        {
            var exportedMods = new List<IMod>();
            foreach (var characterSkinEntry in modsToExport)
            {
                var mod = characterSkinEntry.Mod;
                if (CheckForDuplicates(exportFolder, mod)) // Handle duplicate mod names
                {
                    _logger.Warning("Mod '{ModName}' already exists in export folder, appending GUID to folder name",
                        characterSkinEntry.Mod.Name);
                    var oldName = mod.Name;
                    mod.Rename(mod.Name + "__" + Guid.NewGuid().ToString("N"));
                    exportedMods.Add(mod.CopyTo(exportFolder.FullName));
                    mod.Rename(oldName);
                    continue;
                }

                exportedMods.Add(characterSkinEntry.Mod.CopyTo(exportFolder.FullName));
            }

            RemoveJASMSettings(removeLocalJasmSettings, exportedMods);


            SetModsStatus(setModStatus, exportedMods);

            return;
        }

        if (keepCharacterFolderStructure && !zip) // Copy mods organized by character
        {
            var characterToFolder = new Dictionary<GenshinCharacter, DirectoryInfo>();
            var emptyFoldersCount = 0;

            foreach (var characterModList in characterModLists)
            {
                if (!characterModList.Mods.Any())
                {
                    emptyFoldersCount++;
                    continue; // Skip empty character folders
                }

                var characterFolder = new DirectoryInfo(Path.Combine(exportFolder.FullName,
                    characterModList.Character.DisplayName));

                characterToFolder.Add(characterModList.Character, characterFolder);
                characterFolder.Create();
            }

            if (characterToFolder.Count != _genshinService.GetCharacters().Count() - emptyFoldersCount)
                throw new InvalidOperationException(
                    "Failed to create character folders in export folder, character mismatch");

            var exportedMods = new List<IMod>();
            foreach (var characterSkinEntry in modsToExport)
            {
                var mod = characterSkinEntry.Mod;
                var destinationFolder = characterToFolder[characterSkinEntry.ModList.Character];

                if (CheckForDuplicates(destinationFolder, mod)) // Handle duplicate mod names
                {
                    _logger.Warning("Mod '{ModName}' already exists in export folder, appending GUID to folder name",
                        characterSkinEntry.Mod.Name);

                    var oldName = mod.Name;
                    mod.Rename(mod.Name + "__" + Guid.NewGuid().ToString("N"));
                    exportedMods.Add(mod.CopyTo(destinationFolder.FullName));
                    mod.Rename(oldName);
                    continue;
                }

                exportedMods.Add(characterSkinEntry.Mod.CopyTo(destinationFolder.FullName));
            }

            RemoveJASMSettings(removeLocalJasmSettings, exportedMods);

            SetModsStatus(setModStatus, exportedMods);


            return;
        }

        if (zip)
            throw new NotImplementedException();
    }

    private static bool CheckForDuplicates(DirectoryInfo destinationFolder, IMod mod)
    {
        destinationFolder.Refresh();
        foreach (var directory in destinationFolder.EnumerateDirectories())
        {
            if (directory.Name.Equals(mod.Name, StringComparison.CurrentCultureIgnoreCase))
                return true;

            if (directory.Name.EndsWith(mod.Name, StringComparison.CurrentCultureIgnoreCase))
                return true;

            if (mod.Name.Replace(CharacterModList.DISABLED_PREFIX, "") == directory.Name ||
                mod.Name.Replace("DISABLED", "") == directory.Name)
                return true;
        }

        return false;
    }

    private static void SetModsStatus(SetModStatus setModStatus, IEnumerable<IMod> mods)
    {
        switch (setModStatus)
        {
            case SetModStatus.EnableAllMods:
            {
                foreach (var mod in mods)
                {
                    var enabledName = mod.Name;
                    enabledName = enabledName.Replace(CharacterModList.DISABLED_PREFIX, "");
                    enabledName = enabledName.Replace("DISABLED", "");
                    if (enabledName != mod.Name)
                        mod.Rename(enabledName);
                }

                break;
            }
            case SetModStatus.DisableAllMods:
            {
                foreach (var mod in mods)
                {
                    if (!mod.Name.StartsWith("DISABLED") || !mod.Name.StartsWith(CharacterModList.DISABLED_PREFIX))
                        mod.Rename(CharacterModList.DISABLED_PREFIX + mod.Name);
                }

                break;
            }
        }
    }

    private void RemoveJASMSettings(bool removeLocalJasmSettings, IEnumerable<IMod> exportedMods)
    {
        if (removeLocalJasmSettings)
        {
            foreach (var file in exportedMods.Select(mod => new DirectoryInfo(mod.FullPath))
                         .SelectMany(folder => folder.EnumerateFileSystemInfos(".JASM_*")))
            {
                _logger.Debug("Deleting local jasm file '{JasmFile}' in modFolder", file.FullName);
                file.Delete();
            }
        }
    }

    public ICharacterModList GetCharacterModList(GenshinCharacter character)
    {
        var characterModList = _characterModLists.First(x => x.Character.Id == character.Id);

        return characterModList;
    }


    public void Initialize(string activeModsFolderPath, string? unloadedModsFolderPath = null,
        string? threeMigotoRootfolder = null)
    {
        _logger.Debug(
            "Initializing ModManagerService, activeModsFolderPath: {ActiveModsFolderPath}, unloadedModsFolderPath: {UnloadedModsFolderPath}",
            activeModsFolderPath, unloadedModsFolderPath);
        if (unloadedModsFolderPath is not null)
        {
            _unloadedModsFolder = new DirectoryInfo(unloadedModsFolderPath);
            _unloadedModsFolder.Create();
            UnloadingModsEnabled = true;
        }

        if (threeMigotoRootfolder is not null)
        {
            _threeMigotoFolder = new DirectoryInfo(threeMigotoRootfolder);
            _threeMigotoFolder.Refresh();
            if (!_threeMigotoFolder.Exists)
                throw new InvalidOperationException("3DMigoto folder does not exist");

            //_userIniWatcher = new FileSystemWatcher(_threeMigotoFolder.FullName, D3DX_USER_INI);
            //_userIniWatcher.Changed += OnUserIniChanged;
            //_userIniWatcher.NotifyFilter = NotifyFilters.LastWrite;
            //_userIniWatcher.IncludeSubdirectories = false;
            //_userIniWatcher.EnableRaisingEvents = true;
        }

        _activeModsFolder = new DirectoryInfo(activeModsFolderPath);
        _activeModsFolder.Create();
        InitializeFolderStructure();
        ScanForMods();
    }

    private void InitializeFolderStructure()
    {
        var characters = _genshinService.GetCharacters();
        foreach (var character in characters)
        {
            var characterModFolder = new DirectoryInfo(GetCharacterModFolderPath(character));
            characterModFolder.Create();
        }
    }

    public int ReorganizeMods(GenshinCharacter? characterFolderToReorganize = null)
    {
        if (_activeModsFolder is null) throw new InvalidOperationException("ModManagerService is not initialized");

        if (characterFolderToReorganize is null)
            _logger.Information("Reorganizing mods");
        else
            _logger.Information("Reorganizing mods for '{Character}'", characterFolderToReorganize.DisplayName);

        _activeModsFolder.Refresh();
        var characters = _genshinService.GetCharacters().ToArray();
        var othersCharacter = _genshinService.GetCharacter(_genshinService.OtherCharacterId);
        if (othersCharacter is null)
        {
            _logger.Error("Failed to get 'Others' character");
            return -1;
        }

        var movedMods = 0;

        var folderToReorganize = characterFolderToReorganize is null
            ? _activeModsFolder
            : new DirectoryInfo(GetCharacterModFolderPath(characterFolderToReorganize));

        foreach (var folder in folderToReorganize.EnumerateDirectories())
        {
            // Is a character folder continue
            var character = characters.FirstOrDefault(x =>
                x.DisplayName.Equals(folder.Name, StringComparison.InvariantCultureIgnoreCase));
            if (character is not null)
                continue;

            // Is a mod folder, determine which character it belongs to
            var closestMatchCharacter = _genshinService.GetCharacter(folder.Name);
            switch (closestMatchCharacter)
            {
                case null when characterFolderToReorganize is null:
                    _logger.Information(
                        "Mod folder '{ModFolder}' does not seem to belong to a character. Moving to 'Others' folder",
                        folder.Name);
                    closestMatchCharacter = othersCharacter;
                    break;
                case null:
                    continue; // Mod folder does not belong to this character, but we cant determine which character it belongs to. Move on
            }


            var modList = GetCharacterModList(closestMatchCharacter);
            var mod = new SkinMod(folder, folder.Name);
            try
            {
                using var disableWatcher = modList.DisableWatcher();
                mod.MoveTo(GetCharacterModList(closestMatchCharacter).AbsModsFolderPath);
                _logger.Information("Moved mod '{ModName}' to '{CharacterFolder}' mod folder", mod.Name,
                    closestMatchCharacter.DisplayName);
                movedMods++;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to move mod '{ModName}' to '{CharacterFolder}'", mod.FullPath,
                    closestMatchCharacter.DisplayName);
                continue;
            }

            modList.TrackMod(mod);
        }

        return movedMods;
    }

    private string GetCharacterModFolderPath(GenshinCharacter character)
    {
        return Path.Combine(_activeModsFolder.FullName, character.DisplayName);
    }


    private void OnUserIniChanged(object sender, FileSystemEventArgs e)
    {
        _logger.Debug("d3dx_user.ini was changed");
        UserIniChanged?.Invoke(this, new UserIniChanged());
    }

    public event EventHandler<UserIniChanged>?  UserIniChanged;

    private const string D3DX_USER_INI = "d3dx_user.ini";
    public async Task<string> GetCurrentSwapVariationAsync(Guid characterSkinEntryId)
    {
        if (_threeMigotoFolder is null || !_threeMigotoFolder.Exists)
            return "3DMigoto folder not set";

        var characterSkinEntry = _characterModLists.SelectMany(x => x.Mods)
            .FirstOrDefault(x => x.Id == characterSkinEntryId);
        if (characterSkinEntry is null)
            throw new InvalidOperationException(
                $"CharacterSkinEntry with id {characterSkinEntryId} was not found in any character mod list");

        var mod = characterSkinEntry.Mod;


        var d3dxUserIni = new FileInfo(Path.Combine(_threeMigotoFolder.FullName, D3DX_USER_INI));
        if (!d3dxUserIni.Exists)
        {
            _logger.Debug("d3dx_user.ini does not exist in 3DMigoto folder");
            return "Unknown";
        }

        var lines = await File.ReadAllLinesAsync(d3dxUserIni.FullName);

        var sectionStarted = false;
        var returnVar = "Unknown";
        foreach (var line in lines)
        {
            if (IniConfigHelpers.IsComment(line)) continue;
            if (IniConfigHelpers.IsSection(line, "Constants"))
            {
                sectionStarted = true;
                continue;
            }

            if (!sectionStarted) continue;
            if (IniConfigHelpers.IsSection(line)) break;

            var iniKey = IniConfigHelpers.GetIniKey(line);
            if (iniKey is null || !iniKey.EndsWith("swapvar", StringComparison.CurrentCultureIgnoreCase)) continue;

            if (!iniKey.Contains(mod.Name.ToLower())) continue;

            returnVar = IniConfigHelpers.GetIniValue(line) ?? "Unknown";
            break;
        }

        return returnVar;
    }

    public void Dispose()
    {
        _userIniWatcher.Dispose();
    }
}

public class UserIniChanged : EventArgs
{

}