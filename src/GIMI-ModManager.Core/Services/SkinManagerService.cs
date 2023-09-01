#nullable enable
using System.Runtime.CompilerServices;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using Serilog;

namespace GIMI_ModManager.Core.Services;

public class SkinManagerService : ISkinManagerService
{
    private readonly IGenshinService _genshinService;
    private readonly ILogger _logger;

    private DirectoryInfo _unloadedModsFolder = null!;
    private DirectoryInfo _activeModsFolder = null!;

    private readonly List<ICharacterModList> _characterModLists = new();
    public IReadOnlyCollection<ICharacterModList> CharacterModLists => _characterModLists.AsReadOnly();

    public SkinManagerService(IGenshinService genshinService, ILogger logger)
    {
        _genshinService = genshinService;
        _logger = logger;
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
                _logger.Warning("Character mod folder for {Character} does not exist", character.DisplayName);
                continue;
            }

            var characterModList = new CharacterModList(character, characterModFolder.FullName, logger: _logger);
            _characterModLists.Add(characterModList);

            foreach (var modFolder in characterModFolder.EnumerateDirectories())
            {
                var mod = new Mod(modFolder, modFolder.Name);
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
                        orphanedMods.Remove(x);
                        break;
                    }

                    var disabledName = $"{CharacterModList.DISABLED_PREFIX}{modDirectory.Name}";
                    if (x.Mod.FullPath == Path.Combine(modDirectory.Parent!.FullName, disabledName))
                    {
                        mod = x;
                        orphanedMods.Remove(x);
                        break;
                    }
                }

                if (mod is not null) continue;

                var newMod = new Mod(modDirectory, modDirectory.Name);
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

        _logger.Information("Transferring {ModsCount} mods from {SourceCharacter} to {DestinationCharacter}",
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

    public ICharacterModList GetCharacterModList(GenshinCharacter character)
    {
        var characterModList = _characterModLists.First(x => x.Character.Id == character.Id);

        return characterModList;
    }


    public void Initialize(string activeModsFolderPath, string? unloadedModsFolderPath = null)
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

    public int ReorganizeMods()
    {
        if (_activeModsFolder is null) throw new InvalidOperationException("ModManagerService is not initialized");

        _logger.Information("Reorganizing mods");

        _activeModsFolder.Refresh();
        var characters = _genshinService.GetCharacters().ToArray();
        var othersCharacter = _genshinService.GetCharacter(_genshinService.OtherCharacterId);
        if (othersCharacter is null)
        {
            _logger.Error("Failed to get 'Others' character");
            return -1;
        }

        var movedMods = 0;

        foreach (var folder in _activeModsFolder.EnumerateDirectories())
        {
            // Is a character folder continue
            var character = characters.FirstOrDefault(x => x.DisplayName == folder.Name);
            if (character is not null)
                continue;

            // Is a mod folder, determine which character it belongs to
            var closestMatchCharacter = _genshinService.GetCharacter(folder.Name);
            if (closestMatchCharacter is null)
            {
                _logger.Information(
                    "Mod folder {ModFolder} does not seem to belong to a character. Moving to 'Others' folder",
                    folder.Name);
                closestMatchCharacter = othersCharacter;
            }

            var modList = GetCharacterModList(closestMatchCharacter);
            var mod = new Mod(folder, folder.Name);
            try
            {
                using var disableWatcher = modList.DisableWatcher();
                mod.MoveTo(GetCharacterModList(closestMatchCharacter).AbsModsFolderPath);
                _logger.Information("Moved mod {ModName} to {CharacterFolder} mod folder", mod.FullPath,
                    closestMatchCharacter.DisplayName);
                movedMods++;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to move mod {ModName} to {CharacterFolder}", mod.FullPath,
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
}