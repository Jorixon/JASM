using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.SkinMod;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using OneOf;
using OneOf.Types;
using Serilog;
using static GIMI_ModManager.Core.Contracts.Services.RefreshResult;

namespace GIMI_ModManager.Core.Services;

public sealed class SkinManagerService : ISkinManagerService
{
    private readonly IGameService _gameService;
    private readonly ILogger _logger;
    private readonly ModCrawlerService _modCrawlerService;

    private DirectoryInfo _unloadedModsFolder = null!;
    private DirectoryInfo _activeModsFolder = null!;
    private DirectoryInfo? _threeMigotoFolder;

    private FileSystemWatcher _userIniWatcher = null!;

    private readonly List<ICharacterModList> _characterModLists = new();

    public IReadOnlyCollection<ICharacterModList> CharacterModLists
    {
        get
        {
            lock (_modListLock)
            {
                return _characterModLists.AsReadOnly();
            }
        }
    }


    private readonly object _modListLock = new();
    public bool IsInitialized { get; private set; }

    public SkinManagerService(IGameService gameService, ILogger logger, ModCrawlerService modCrawlerService)
    {
        _gameService = gameService;
        _modCrawlerService = modCrawlerService;
        _logger = logger.ForContext<SkinManagerService>();
    }

    public string UnloadedModsFolderPath => _unloadedModsFolder.FullName;
    public string ActiveModsFolderPath => _activeModsFolder.FullName;

    public bool UnloadingModsEnabled { get; private set; }

    public async Task ScanForModsAsync()
    {
        _activeModsFolder.Refresh();

        var characters = _gameService.GetCharacters();
        foreach (var character in characters)
        {
            var characterModFolder = new DirectoryInfo(GetCharacterModFolderPath(character));
            if (!characterModFolder.Exists)
            {
                _logger.Debug("Character mod folder for '{Character}' does not exist", character.DisplayName);
                continue;
            }

            var characterModList = new CharacterModList(character, characterModFolder.FullName, logger: _logger);
            _characterModLists.Add(characterModList);

            foreach (var modFolder in characterModFolder.EnumerateDirectories())
            {
                try
                {
                    var mod = await SkinMod.CreateModAsync(modFolder.FullName).ConfigureAwait(false);

                    if (GetModById(mod.Id) is not null)
                        mod = await SkinMod.CreateModAsync(modFolder.FullName, true).ConfigureAwait(false);


                    characterModList.TrackMod(mod);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Failed to initialize mod '{ModFolder}'", modFolder.FullName);
                }
            }
        }
    }

    public async Task<RefreshResult> RefreshModsAsync(string? refreshForCharacter = null)
    {
        var modsUntracked = new List<string>();
        var newModsFound = new List<ISkinMod>();
        var duplicateModsFound = new List<DuplicateMods>();
        var errors = new List<string>();

        foreach (var characterModList in _characterModLists)
        {
            if (refreshForCharacter is not null &&
                !characterModList.Character.InternalNameEquals(refreshForCharacter)) continue;

            var modsDirectory = new DirectoryInfo(characterModList.AbsModsFolderPath);
            modsDirectory.Refresh();

            var orphanedMods = new List<CharacterSkinEntry>(characterModList.Mods);

            foreach (var modDirectory in modsDirectory.EnumerateDirectories())
            {
                CharacterSkinEntry? mod = null;

                foreach (var x in characterModList.Mods)
                {
                    if (x.Mod.FullPath.AbsPathCompare(modDirectory.FullName)
                        &&
                        Directory.Exists(Path.Combine(characterModList.AbsModsFolderPath,
                            ModFolderHelpers.GetFolderNameWithDisabledPrefix(modDirectory.Name)))
                        &&
                        Directory.Exists(Path.Combine(characterModList.AbsModsFolderPath,
                            ModFolderHelpers.GetFolderNameWithoutDisabledPrefix(modDirectory.Name)))
                       )
                    {
                        var newName = modDirectory.Name;

                        while (Directory.Exists(Path.Combine(characterModList.AbsModsFolderPath, newName)))
                            newName = DuplicateModAffixHelper.AppendNumberAffix(newName);

                        _logger.Warning(
                            "Mod '{ModName}' has both enabled and disabled folders, renaming folder",
                            modDirectory.Name);

                        duplicateModsFound.Add(new DuplicateMods(x.Mod.Name, newName));
                        x.Mod.Rename(newName);
                        mod = x;
                        mod.Mod.ClearCache();
                        orphanedMods.Remove(x);
                        break;
                    }

                    if (x.Mod.FullPath.AbsPathCompare(modDirectory.FullName))
                    {
                        mod = x;
                        mod.Mod.ClearCache();
                        orphanedMods.Remove(x);
                        break;
                    }

                    var disabledName = ModFolderHelpers.GetFolderNameWithDisabledPrefix(modDirectory.Name);
                    if (x.Mod.FullPath.AbsPathCompare(Path.Combine(characterModList.AbsModsFolderPath, disabledName)))
                    {
                        mod = x;
                        mod.Mod.ClearCache();
                        orphanedMods.Remove(x);
                        break;
                    }
                }

                if (mod is not null) continue;

                try
                {
                    var newMod = await SkinMod.CreateModAsync(modDirectory.FullName).ConfigureAwait(false);

                    if (GetModById(newMod.Id) is not null)
                    {
                        _logger.Debug("Mod '{ModName}' has ID that already exists in mod list, generating new ID",
                            newMod.Name);
                        newMod = await SkinMod.CreateModAsync(modDirectory.FullName, true).ConfigureAwait(false);
                    }


                    characterModList.TrackMod(newMod);
                    newModsFound.Add(newMod);
                    _logger.Debug("Found new mod '{ModName}' in '{CharacterFolder}'", newMod.Name,
                        characterModList.Character.DisplayName);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Failed to create mod from folder '{ModFolder}'", modDirectory.FullName);
                    errors.Add(
                        $"Failed to track new mod folder: '{modDirectory.FullName}' | For character {characterModList.Character.DisplayName}");
                }
            }

            orphanedMods.ForEach(x =>
            {
                characterModList.UnTrackMod(x.Mod);
                modsUntracked.Add(x.Mod.FullPath);
                _logger.Debug("Mod '{ModName}' in '{CharacterFolder}' is no longer tracked", x.Mod.Name,
                    characterModList.Character.DisplayName);
            });
        }

        return new RefreshResult(modsUntracked, newModsFound, duplicateModsFound, errors: errors);
    }


    public async Task<OneOf<Success, Error<string>[]>> TransferMods(ICharacterModList source,
        ICharacterModList destination,
        IEnumerable<Guid> modsEntryIds)
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
            mods.Count, source.Character.InternalName, destination.Character.InternalName);

        using var sourceDisabled = source.DisableWatcher();
        using var destinationDisabled = destination.DisableWatcher();

        var errors = new List<Error<string>>();
        foreach (var mod in mods)
        {
            source.UnTrackMod(mod);
            mod.MoveTo(destination.AbsModsFolderPath);
            destination.TrackMod(mod);

            try
            {
                var skinSettings = await mod.Settings.ReadSettingsAsync();
                skinSettings.CharacterSkinOverride = null;
                await mod.Settings.SaveSettingsAsync(skinSettings);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to clear skin override for mod '{ModName}'", mod.Name);
                errors.Add(new Error<string>(
                    $"Failed to clear skin override for mod '{mod.Name}'. Reason: {e.Message}"));
            }
        }

        return errors.Any() ? errors.ToArray() : new Success();
    }

    public event EventHandler<ExportProgress>? ModExportProgress;

    public ISkinMod? GetModById(Guid id) =>
        CharacterModLists.SelectMany(x => x.Mods).FirstOrDefault(x => x.Id == id)?.Mod;

    public Task EnableModListAsync(ICharacter moddableObject)
    {
        CreateModListFolder(moddableObject);
        var modList = new CharacterModList(moddableObject, GetCharacterModFolderPath(moddableObject), logger: _logger);

        _characterModLists.Add(modList);

        return RefreshModsAsync(moddableObject.InternalName);
    }

    public Task DisableModListAsync(IModdableObject moddableObject, bool deleteFolder = false)
    {
        var modList = GetCharacterModList(moddableObject);
        var modFolder = new DirectoryInfo(modList.AbsModsFolderPath);

        _characterModLists.Remove(modList);
        modList.Dispose();

        if (deleteFolder && modFolder.Exists)
        {
            _logger.Information("Deleting mod folder '{ModFolder}'", modFolder.FullName);
            modFolder.Delete(true);
        }

        return Task.CompletedTask;
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


        foreach (var characterModList in characterModLists) modsToExport.AddRange(characterModList.Mods);

        double modsProgress = 0;
        double divider = modsToExport.Count + (removeLocalJasmSettings ? 1 : 0) +
                         (setModStatus != SetModStatus.KeepCurrent ? 1 : 0);
        var modsProgressIncrement = 100 / divider;

        if (!keepCharacterFolderStructure && !zip) // Copy mods unorganized
        {
            var exportedMods = new List<IMod>();
            foreach (var characterSkinEntry in modsToExport)
            {
                var mod = characterSkinEntry.Mod;
                ModExportProgress?.Invoke(this,
                    new ExportProgress(modsProgress += modsProgressIncrement, mod.Name, "Copying Folders"));

                if (CheckForDuplicates(exportFolder, mod)) // Handle duplicate mod names
                {
                    _logger.Information(
                        "Mod '{ModName}' already exists in export folder, appending GUID to folder name",
                        characterSkinEntry.Mod.Name);
                    var oldName = mod.Name;
                    using var disableWatcher = characterSkinEntry.ModList.DisableWatcher();
                    mod.Rename(mod.Name + "__" + Guid.NewGuid().ToString("N"));
                    exportedMods.Add(mod.CopyTo(exportFolder.FullName));
                    mod.Rename(oldName);
                    _logger.Information("Copied mod '{ModName}' to export folder", mod.Name);
                    continue;
                }

                exportedMods.Add(characterSkinEntry.Mod.CopyTo(exportFolder.FullName));
                _logger.Information("Copied mod '{ModName}' to export folder", mod.Name);
            }

            ModExportProgress?.Invoke(this,
                new ExportProgress(modsProgress += modsProgressIncrement, null, "Removing JASM settings..."));
            RemoveJASMSettings(removeLocalJasmSettings, exportedMods);

            ModExportProgress?.Invoke(this,
                new ExportProgress(modsProgress += modsProgressIncrement, null, "Setting Mod Status..."));
            SetModsStatus(setModStatus, exportedMods);

            ModExportProgress?.Invoke(this,
                new ExportProgress(100, null, "Finished"));
            return;
        }

        if (keepCharacterFolderStructure && !zip) // Copy mods organized by character
        {
            var characterToFolder = new Dictionary<IModdableObject, DirectoryInfo>();
            var emptyFoldersCount = 0;

            foreach (var characterModList in characterModLists)
            {
                if (!characterModList.Mods.Any())
                {
                    emptyFoldersCount++;
                    continue; // Skip empty character folders
                }

                var characterFolder = new DirectoryInfo(Path.Combine(exportFolder.FullName,
                    characterModList.Character.InternalName));

                characterToFolder.Add(characterModList.Character, characterFolder);
                characterFolder.Create();
            }

            if (characterToFolder.Count != _gameService.GetCharacters().Count() - emptyFoldersCount)
                throw new InvalidOperationException(
                    "Failed to create character folders in export folder, character mismatch");

            var exportedMods = new List<IMod>();
            foreach (var characterSkinEntry in modsToExport)
            {
                var mod = characterSkinEntry.Mod;
                var destinationFolder = characterToFolder[characterSkinEntry.ModList.Character];
                ModExportProgress?.Invoke(this,
                    new ExportProgress(modsProgress += modsProgressIncrement, mod.Name, "Copying Folders"));

                if (CheckForDuplicates(destinationFolder, mod)) // Handle duplicate mod names
                {
                    _logger.Information(
                        "Mod '{ModName}' already exists in export folder, appending GUID to folder name",
                        characterSkinEntry.Mod.Name);

                    var oldName = mod.Name;
                    using var disableWatcher = characterSkinEntry.ModList.DisableWatcher();
                    mod.Rename(mod.Name + "__" + Guid.NewGuid().ToString("N"));
                    exportedMods.Add(mod.CopyTo(destinationFolder.FullName));
                    mod.Rename(oldName);
                    _logger.Information("Copied mod '{ModName}' to export character folder '{CharacterFolder}'",
                        mod.Name,
                        characterSkinEntry.ModList.Character.InternalName);

                    continue;
                }

                exportedMods.Add(characterSkinEntry.Mod.CopyTo(destinationFolder.FullName));
                _logger.Information("Copied mod '{ModName}' to export character folder '{CharacterFolder}'", mod.Name,
                    characterSkinEntry.ModList.Character.InternalName);
            }

            ModExportProgress?.Invoke(this,
                new ExportProgress(modsProgress += modsProgressIncrement, null, "Removing JASM settings..."));
            RemoveJASMSettings(removeLocalJasmSettings, exportedMods);

            ModExportProgress?.Invoke(this,
                new ExportProgress(modsProgress += modsProgressIncrement, null, "Setting Mod Status..."));
            SetModsStatus(setModStatus, exportedMods);

            ModExportProgress?.Invoke(this,
                new ExportProgress(100, null, "Finished"));


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
                    if (!mod.Name.StartsWith("DISABLED") || !mod.Name.StartsWith(CharacterModList.DISABLED_PREFIX))
                        mod.Rename(CharacterModList.DISABLED_PREFIX + mod.Name);

                break;
            }
        }
    }

    private void RemoveJASMSettings(bool removeLocalJasmSettings, IEnumerable<IMod> exportedMods)
    {
        if (removeLocalJasmSettings)
            foreach (var file in exportedMods.Select(mod => new DirectoryInfo(mod.FullPath))
                         .SelectMany(folder => folder.EnumerateFileSystemInfos(".JASM_*")))
            {
                _logger.Debug("Deleting local jasm file '{JasmFile}' in modFolder", file.FullName);
                file.Delete();
            }
    }

    public ICharacterModList GetCharacterModList(string internalName)
    {
        var characterModList = _characterModLists.First(x => x.Character.InternalNameEquals(internalName));

        return characterModList;
    }

    public ICharacterModList GetCharacterModList(IModdableObject character)
    {
        return GetCharacterModList(character.InternalName);
    }


    public async Task InitializeAsync(string activeModsFolderPath, string? unloadedModsFolderPath = null,
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
        await ScanForModsAsync();
        IsInitialized = true;

#if DEBUG
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        Task.Run(DebugDuplicateIdChecker).ConfigureAwait(false);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#endif
    }

    private void InitializeFolderStructure()
    {
        var characters = _gameService.GetCharacters();
        foreach (var character in characters)
            CreateModListFolder(character);
    }

    private void CreateModListFolder(INameable character)
    {
        var characterModFolder = new DirectoryInfo(GetCharacterModFolderPath(character));
        characterModFolder.Create();
    }

    public async Task<int> ReorganizeModsAsync(string? characterFolderToReorganize = null,
        bool disableMods = false)
    {
        if (_activeModsFolder is null) throw new InvalidOperationException("ModManagerService is not initialized");

        var characterFolder = characterFolderToReorganize is null
            ? null
            : _gameService.GetCharacterByIdentifier(characterFolderToReorganize);

        if (characterFolderToReorganize is null)
            _logger.Information("Reorganizing mods");
        else
            _logger.Information("Reorganizing mods for '{Character}'", characterFolder?.InternalName);

        _activeModsFolder.Refresh();
        var characters = _gameService.GetCharacters().ToArray();

        var disabledCharacters = _gameService.GetDisabledCharacters();

        var othersCharacter = _gameService.GetCharacterByIdentifier("Others");
        if (othersCharacter is null)
        {
            _logger.Error("Failed to get 'Others' character");
            return -1;
        }

        var movedMods = 0;

        var folderToReorganize = characterFolder is null
            ? _activeModsFolder
            : new DirectoryInfo(GetCharacterModFolderPath(characterFolder));

        foreach (var folder in folderToReorganize.EnumerateDirectories())
        {
            // Is a character folder continue
            var character = characters.Concat(disabledCharacters).FirstOrDefault(x =>
                x.InternalName.Equals(folder.Name));
            if (character is not null)
                continue;


            var closestMatchCharacter =
                _modCrawlerService.GetFirstSubSkinRecursive(folder.FullName)?.Character ??
                _gameService.QueryCharacter(ModFolderHelpers.GetFolderNameWithoutDisabledPrefix(folder.Name),
                    minScore: 150);


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


            var modList = GetCharacterModList(closestMatchCharacter.InternalName);
            ISkinMod? mod = null;
            try
            {
                mod = await SkinMod.CreateModAsync(folder);
                if (GetModById(mod.Id) is not null)
                {
                    _logger.Debug("Mod '{ModName}' has ID that already exists in mod list, generating new ID",
                        mod.Name);
                    mod = await SkinMod.CreateModAsync(folder, true);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to initialize mod folder '{ModFolder}'", folder.FullName);
                continue;
            }

            try
            {
                using var disableWatcher = modList.DisableWatcher();

                var renameAttempts = 0;
                while (modList.FolderAlreadyExists(mod.Name))
                {
                    var oldName = mod.Name;
                    mod.Rename(DuplicateModAffixHelper.AppendNumberAffix(mod.Name));
                    _logger.Information(
                        "Mod '{ModName}' already exists in '{CharacterFolder}', renaming to {NewModName}",
                        oldName, closestMatchCharacter.InternalName, mod.Name);
                    renameAttempts++;
                    if (renameAttempts <= 10) continue;
                    _logger.Error(
                        "Failed to rename mod '{ModName}' to '{NewModName}' after 10 attempts, skipping mod",
                        mod.Name, mod.Name);
                    break;
                }

                if (renameAttempts > 10) continue;


                mod.MoveTo(GetCharacterModList(closestMatchCharacter.InternalName).AbsModsFolderPath);
                _logger.Information("Moved mod '{ModName}' to '{CharacterFolder}' mod folder", mod.Name,
                    closestMatchCharacter.InternalName);
                movedMods++;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to move mod '{ModName}' to '{CharacterFolder}'", mod.FullPath,
                    closestMatchCharacter.InternalName);
                continue;
            }

            modList.TrackMod(mod);
            if (disableMods && modList.IsModEnabled(mod))
            {
                try
                {
                    modList.DisableMod(mod.Id);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Failed to disable mod '{ModName}'", mod.Name);
                }
            }
        }

        return movedMods;
    }

    private string GetCharacterModFolderPath(INameable character)
    {
        return Path.Combine(_activeModsFolder.FullName, character.InternalName);
    }


    private void OnUserIniChanged(object sender, FileSystemEventArgs e)
    {
        _logger.Debug("d3dx_user.ini was changed");
        UserIniChanged?.Invoke(this, new UserIniChanged());
    }

    public event EventHandler<UserIniChanged>? UserIniChanged;

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
        _userIniWatcher?.Dispose();
    }

#if DEBUG
    [DoesNotReturn]
    private async Task DebugDuplicateIdChecker()
    {
        while (true)
        {
            await Task.Delay(1000);
            var mods = _characterModLists.SelectMany(x => x.Mods).Select(x => x.Mod);
            var duplicateIds = mods.GroupBy(x => x.Id).Where(x => x.Count() > 1).ToArray();

            if (duplicateIds.Any())
            {
                foreach (var duplicateId in duplicateIds)
                {
                    _logger.Error("Duplicate ID found: {Id}", duplicateId.Key);
                    foreach (var mod in duplicateId)
                        _logger.Error("Mod: {ModName}", mod.Name);
                }

                Debugger.Break();
            }
        }
    }
#endif
}

public class UserIniChanged : EventArgs
{
}

public sealed class ExportProgress : EventArgs
{
    public ExportProgress(double progress, string? modName, string operation)
    {
        Progress = (int)Math.Round(progress);
        ModName = modName;
        Operation = operation;
    }

    public int Progress { get; }
    public string? ModName { get; }
    public string Operation { get; }
}