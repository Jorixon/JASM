﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.SkinMod;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
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

    public string ThreeMigotoRootfolder => _threeMigotoFolder?.FullName ?? string.Empty;

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


    private void AddNewModList(ICharacterModList newModList)
    {
        lock (_modListLock)
        {
            if (_characterModLists.Any(x => x.Character.InternalNameEquals(newModList.Character.InternalName)))
                throw new InvalidOperationException(
                    $"Mod list for character '{newModList.Character.DisplayName}' already exists");

            _characterModLists.Add(newModList);
        }
    }

    public async Task ScanForModsAsync()
    {
        _activeModsFolder.Refresh();

        var characters = _gameService.GetAllModdableObjects();
        foreach (var character in characters)
        {
            var characterModFolder = new DirectoryInfo(GetCharacterModFolderPath(character));

            var characterModList = new CharacterModList(character, characterModFolder.FullName, logger: _logger);
            AddNewModList(characterModList);

            if (!characterModFolder.Exists)
            {
                _logger.Verbose("Character mod folder for '{Character}' does not exist", character.DisplayName);
                continue;
            }

            foreach (var modFolder in characterModFolder.EnumerateDirectories())
            {
                try
                {
                    var mod = await CreateModAsync(modFolder).ConfigureAwait(false);

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

    private async Task<ISkinMod> CreateModAsync(DirectoryInfo modFolder)
    {
        try
        {
            return await SkinMod.CreateModAsync(modFolder.FullName).ConfigureAwait(false);
        }
        catch (JsonException e)
        {
            modFolder.Refresh();

            var invalidJasmConfigFile = modFolder.EnumerateFiles(Constants.ModConfigFileName).FirstOrDefault();
            if (invalidJasmConfigFile is null)
                throw new FileNotFoundException("Could not find JASM config file", Constants.ModConfigFileName, e);


            _logger.Error(e, "Failed to initialize mod due to invalid config file'{ModFolder}'",
                modFolder.FullName);
            _logger.Information("Renaming invalid config file '{ConfigFile}' to {ConfigFile}.invalid",
                invalidJasmConfigFile.FullName, invalidJasmConfigFile.FullName);
            invalidJasmConfigFile.MoveTo(Path.Combine(modFolder.FullName,
                invalidJasmConfigFile.Name + ".invalid"));

            return await SkinMod.CreateModAsync(modFolder.FullName).ConfigureAwait(false);
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

            if (!modsDirectory.Exists)
            {
                _logger.Debug("RefreshModsAsync() Character mod folder for '{Character}' does not exist",
                    characterModList.Character.DisplayName);

                if (characterModList.IsCharacterFolderCreated())
                    throw new InvalidOperationException(
                        $"Character mod folder for '{characterModList.Character.DisplayName}' does not exist, but the character folder is created");

                continue;
            }

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
                        await TryRefreshIniPathAsync(mod.Mod, errors).ConfigureAwait(false);
                        break;
                    }

                    if (x.Mod.FullPath.AbsPathCompare(modDirectory.FullName))
                    {
                        mod = x;
                        mod.Mod.ClearCache();
                        orphanedMods.Remove(x);
                        await TryRefreshIniPathAsync(mod.Mod, errors).ConfigureAwait(false);
                        break;
                    }

                    var disabledName = ModFolderHelpers.GetFolderNameWithDisabledPrefix(modDirectory.Name);
                    if (x.Mod.FullPath.AbsPathCompare(Path.Combine(characterModList.AbsModsFolderPath, disabledName)))
                    {
                        mod = x;
                        mod.Mod.ClearCache();
                        orphanedMods.Remove(x);
                        await TryRefreshIniPathAsync(mod.Mod, errors).ConfigureAwait(false);
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
            continue;

            async Task TryRefreshIniPathAsync(ISkinMod mod, IList<string> errorList)
            {
                try
                {
                    await mod.GetModIniPathAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
#if DEBUG
                    throw;
#endif
                    _logger.Error(e, "Failed getting mod .ini path when refreshing mods");
                    errorList.Add(
                        $"Failed to get ini path for mod: '{mod.GetDisplayName()}' | Mod file path: {mod.FullPath}");
                }
            }
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

    public CharacterSkinEntry? GetModEntryById(Guid id)
    {
        var modEntries = GetAllMods(GetOptions.All);
        return modEntries.FirstOrDefault(x => x.Id == id);
    }

    public Task EnableModListAsync(ICharacter moddableObject)
    {
        var modList = new CharacterModList(moddableObject, GetCharacterModFolderPath(moddableObject), logger: _logger);

        AddNewModList(modList);

        return RefreshModsAsync(moddableObject.InternalName);
    }

    public Task DisableModListAsync(IModdableObject moddableObject, bool deleteFolder = false)
    {
        var modList = GetCharacterModList(moddableObject);
        var modFolder = new DirectoryInfo(modList.AbsModsFolderPath);
        lock (_modListLock)
        {
            _characterModLists.Remove(modList);
            modList.Dispose();
        }

        if (deleteFolder && modFolder.Exists)
        {
            _logger.Information("Deleting mod folder '{ModFolder}'", modFolder.FullName);
            modFolder.Delete(true);
        }

        return Task.CompletedTask;
    }

    public ISkinMod AddMod(ISkinMod mod, ICharacterModList modList, bool move = false)
    {
        if (GetModById(mod.Id) is not null)
            throw new InvalidOperationException($"Mod with id {mod.Id} is already tracked in a modList");

        var existingMods = modList.Mods.Select(ske => ske.Mod).ToArray();

        foreach (var existingMod in existingMods)
        {
            if (ModFolderHelpers.FolderNameEquals(mod.Name, existingMod.Name))
                throw new InvalidOperationException(
                    $"Mod with name {mod.Name} already exists in modList {modList.Character.DisplayName}");
        }

        modList.InstantiateCharacterFolder();

        using var disableWatcher = modList.DisableWatcher();
        if (move)
            mod.MoveTo(modList.AbsModsFolderPath);
        else
            mod = mod.CopyTo(modList.AbsModsFolderPath);

        modList.TrackMod(mod);
        return mod;
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
                    characterModList.Character.ModCategory.InternalName, characterModList.Character.InternalName));

                characterToFolder.Add(characterModList.Character, characterFolder);
                characterFolder.Create();
            }

            if (characterToFolder.Count != _gameService.GetAllModdableObjects().Count - emptyFoldersCount)
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
                         .SelectMany(folder => folder.EnumerateFileSystemInfos(".JASM_*", SearchOption.AllDirectories)))
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


    public ICharacterModList? GetCharacterModListOrDefault(string internalName)
    {
        return _characterModLists.FirstOrDefault(x => x.Character.InternalNameEquals(internalName));
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
        await ScanForModsAsync().ConfigureAwait(false);
        IsInitialized = true;

#if DEBUG
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        Task.Run(DebugDuplicateIdChecker).ConfigureAwait(false);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#endif
    }

    private void InitializeFolderStructure()
    {
        var categories = _gameService.GetCategories();

        foreach (var category in categories)
        {
            var categoryFolder = GetCategoryFolderPath(category);
            categoryFolder.Create();
        }
    }

    public DirectoryInfo GetCategoryFolderPath(ICategory category)
    {
        return new DirectoryInfo(Path.Combine(_activeModsFolder.FullName, category.InternalName));
    }


    public async Task<int> ReorganizeModsAsync(InternalName? characterFolderToReorganize = null,
        bool disableMods = false)
    {
        if (_activeModsFolder is null) throw new InvalidOperationException("ModManagerService is not initialized");

        var characterFolder = characterFolderToReorganize is null
            ? null
            : _gameService.GetModdableObjectByIdentifier(characterFolderToReorganize);

        if (characterFolderToReorganize is null)
            _logger.Information("Reorganizing mods");
        else
            _logger.Information("Reorganizing mods for '{Character}'", characterFolder?.InternalName);

        _activeModsFolder.Refresh();
        var characters = _gameService.GetAllModdableObjects();

        var disabledCharacters = _gameService.GetAllModdableObjects(GetOnly.Disabled);

        var othersCharacter = _gameService.GetModdableObjectByIdentifier(new InternalName("Others"));
        if (othersCharacter is null)
        {
            _logger.Error("Failed to get 'Others' character");
            return -1;
        }

        var movedMods = 0;

        var folderToReorganize = characterFolder is null
            ? _activeModsFolder
            : new DirectoryInfo(GetCharacterModFolderPath(characterFolder));

        if (!folderToReorganize.Exists)
            return movedMods;

        foreach (var folder in folderToReorganize.EnumerateDirectories())
        {
            // Is a character folder in the root mods folder then move it into the category folder
            var character = characters.Concat(disabledCharacters).FirstOrDefault(x =>
                x.InternalName.Equals(folder.Name));


            if (character is not null)
            {
                var existingModList = GetCharacterModListOrDefault(character.InternalName);
                if (existingModList is null)
                {
                    _logger.Information(
                        "Character folder '{CharacterFolder}' does not have a mod list, disabled? Ignoring and continuing",
                        folder.FullName);
                    continue;
                }

                if (folder.EnumerateFileSystemInfos().Any())
                    _logger.Warning($"""
                                     During mod reorganization, a moddableObject (Navia, Ayaka... etc) folder was found in root mods folder.
                                     But should be in the their respective category folder. example: Mods/Character/Ayaka/<mod folders>
                                     JASM will move the mod folders in the moddableObject folder to the moddableObject folder in the category folder.
                                     Then delete the moddableObject folder in the root mods folder if it is empty.
                                     If there any loose files they will be ignored in the root of the moddableObject, but will not be deleted.
                                     => Mods in the folder {folder.FullName} will be moved to {existingModList.AbsModsFolderPath} folder.
                                     """);

                if (HasFilesWithModExtensionsInRoot(folder, character))
                {
                    _logger.Warning($"""
                                     Found files that are usually at the root of a mod in a character folder. Therefore, JASM will ignore this folder:
                                     {folder.Name} - {folder.FullName}
                                     {character.InternalName} is a reserved name for a character folder. It is suggested to avoid naming a mod folder the same as a character. JASM uses internal names stored in the Assets folder to determine chracter folder names.
                                     JASM will ignore this folder and continue.
                                     """);
                    continue;
                }


                foreach (var modFolder in folder.EnumerateDirectories())
                {
                    if (existingModList.FolderAlreadyExists(modFolder.Name))
                    {
                        _logger.Warning(
                            "Mod '{ModName}' already exists in '{CharacterFolder}', skipping mod",
                            modFolder.Name, existingModList.Character.InternalName);
                        continue;
                    }

                    try
                    {
                        AddMod(await SkinMod.CreateModAsync(modFolder), existingModList, true);
                        _logger.Information("Moved mod '{ModName}' to '{CharacterFolder}' mod folder",
                            modFolder.Name, existingModList.Character.InternalName);
                        movedMods++;
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Failed to move mod '{ModName}' to '{CharacterFolder}'", modFolder.FullName,
                            existingModList.Character.InternalName);
                    }
                }


                if (!folder.EnumerateFileSystemInfos().Any())
                {
                    _logger.Information("Deleting empty character folder '{CharacterFolder}'", folder.FullName);
                    folder.Delete();
                }

                continue;
            }


            // Is a category folder => ignore
            if (_gameService.GetCategories().Any(x => x.InternalName.Equals(folder.Name)))
            {
                _logger.Debug("Found category folder '{CategoryFolder}' in root mods folder, ignoring",
                    folder.FullName);
                continue;
            }


            var closestMatchCharacter =
                _modCrawlerService.GetMatchingModdableObjects(folder.FullName).FirstOrDefault();


            if (closestMatchCharacter is null)
            {
                closestMatchCharacter = _gameService.QueryModdableObjects(
                    ModFolderHelpers.GetFolderNameWithoutDisabledPrefix(folder.Name),
                    minScore: 200).OrderByDescending(x => x.Value).FirstOrDefault().Key;
            }

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

            modList.InstantiateCharacterFolder();

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

    private bool HasFilesWithModExtensionsInRoot(DirectoryInfo directoryInfo, IModdableObject? moddableObject = null)
    {
        foreach (var file in directoryInfo.EnumerateFiles())
        {
            if (Constants.ScriptIniNames.Any(x => x.Equals(file.Name, StringComparison.OrdinalIgnoreCase)))
                return true;

            if (file.Name.StartsWith("cover", StringComparison.OrdinalIgnoreCase))
                return true;

            if (file.Name.StartsWith("preview", StringComparison.OrdinalIgnoreCase))
                return true;

            if (moddableObject is not null &&
                file.Name.Equals($"{moddableObject.InternalName}.ini", StringComparison.OrdinalIgnoreCase))
                return true;

            if (ModCrawlerService.ModExtensions.Any(ext =>
                    ext.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private string GetCharacterModFolderPath(IModdableObject character)
    {
        var category = _gameService.GetCategories()
            .FirstOrDefault(c => c.InternalName.Equals(character.ModCategory.InternalName));

        if (category is null)
            throw new InvalidOperationException(
                $"Failed to get category for character '{character.DisplayName}'");

        return Path.Combine(_activeModsFolder.FullName, category.InternalName, character.InternalName);
    }

    public ICollection<DirectoryInfo> CleanCharacterFolders()
    {
        var deletedFolders = new List<DirectoryInfo>();
        foreach (var characterModList in _characterModLists)
        {
            var characterFolder = new DirectoryInfo(characterModList.AbsModsFolderPath);
            if (!characterFolder.Exists) continue;

            var characterModFolders = characterFolder.EnumerateDirectories().ToArray();

            foreach (var modFolder in characterModFolders)
            {
                var containsOnlyJasmFiles = modFolder.EnumerateFileSystemInfos().All(ModFolderHelpers.IsJASMFileEntry);

                if (!containsOnlyJasmFiles) continue;

                _logger.Information("Deleting mod folder, due to it only containing jasm files '{ModFolder}'",
                    modFolder.FullName);

                modFolder.Delete(true);
                deletedFolders.Add(modFolder);
            }

            if (characterFolder.EnumerateFileSystemInfos().Any()) continue;

            _logger.Information("Deleting empty character folder '{CharacterFolder}'", characterFolder.FullName);
            characterFolder.Delete();
            deletedFolders.Add(characterFolder);
        }

        var categories = _gameService.GetCategories();
        foreach (var directory in _activeModsFolder.EnumerateDirectories())
        {
            if (categories.Any(x => x.InternalName.Equals(directory.Name))) continue;

            if (directory.EnumerateFileSystemInfos().Any()) continue;

            _logger.Information("Deleting unknown empty folder '{Folder}'", directory.FullName);
            directory.Delete();
            deletedFolders.Add(directory);
        }

        return deletedFolders;
    }

    public IList<CharacterSkinEntry> GetAllMods(GetOptions getOptions = GetOptions.All)
    {
        // We get them all to avoid locking for too long
        var allMods = CharacterModLists.SelectMany(x => x.Mods).ToList();

        return getOptions switch
        {
            GetOptions.All => allMods,
            GetOptions.Enabled => allMods.Where(x => x.IsEnabled).ToList(),
            GetOptions.Disabled => allMods.Where(x => !x.IsEnabled).ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(getOptions), getOptions, null)
        };
    }

    private void OnUserIniChanged(object sender, FileSystemEventArgs e)
    {
        _logger.Debug("d3dx_user.ini was changed");
        UserIniChanged?.Invoke(this, new UserIniChanged());
    }

    public event EventHandler<UserIniChanged>? UserIniChanged;

    private static string D3DX_USER_INI = Constants.UserIniFileName;

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
            await Task.Delay(1000).ConfigureAwait(false);
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