using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Services;
using OneOf;
using OneOf.Types;

namespace GIMI_ModManager.Core.Contracts.Services;

public interface ISkinManagerService : IDisposable
{
    public string UnloadedModsFolderPath { get; }
    public string ActiveModsFolderPath { get; }
    public IReadOnlyCollection<ICharacterModList> CharacterModLists { get; }
    bool IsInitialized { get; }
    public Task ScanForModsAsync();
    public ICharacterModList GetCharacterModList(string internalName);
    public ICharacterModList GetCharacterModList(IModdableObject character);

    public Task InitializeAsync(string activeModsFolderPath, string? unloadedModsFolderPath,
        string? threeMigotoRootfolder = null);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="characterFolderToReorganize">If null, reorganize all mods outside of characters mod folders</param>
    /// <param name="disableMods">If true will also disable the mods</param>
    /// <returns>Mods moved</returns>
    public Task<int> ReorganizeModsAsync(string? characterFolderToReorganize = null,
        bool disableMods = false);

    /// <summary>
    /// This looks for mods in characters mod folder that are not tracked by the mod manager and adds them to the mod manager.
    /// </summary>
    public Task<RefreshResult> RefreshModsAsync(string? refreshForCharacter = null);

    public Task<OneOf<Success, Error<string>[]>> TransferMods(ICharacterModList source, ICharacterModList destination,
        IEnumerable<Guid> modsEntryIds);

    public Task<string> GetCurrentSwapVariationAsync(Guid characterSkinEntryId);

    /// <summary>
    /// Exports mods to a user specified folder.
    /// </summary>
    /// <param name="characterModLists">Mods from characters to export</param>
    /// <param name="exportPath">Folder to extract to</param>
    /// <param name="removeLocalJasmSettings"></param>
    /// <param name="zip"></param>
    /// <param name="keepCharacterFolderStructure"></param>
    /// <param name="setModStatus"></param>
    /// <returns></returns>
    public void ExportMods(ICollection<ICharacterModList> characterModLists, string exportPath,
        bool removeLocalJasmSettings = true, bool zip = true, bool keepCharacterFolderStructure = false,
        SetModStatus setModStatus = SetModStatus.KeepCurrent);

    public event EventHandler<ExportProgress>? ModExportProgress;

    public ISkinMod? GetModById(Guid id);

    public Task EnableModListAsync(ICharacter moddableObject);

    public Task DisableModListAsync(IModdableObject moddableObject, bool deleteFolder = false);
}

public enum SetModStatus
{
    KeepCurrent,
    EnableAllMods,
    DisableAllMods
}

public readonly struct RefreshResult
{
    public RefreshResult(IReadOnlyCollection<string> modsUntracked, IReadOnlyCollection<ISkinMod> modsTracked,
        IReadOnlyCollection<DuplicateMods> modsDuplicate, IReadOnlyCollection<string> errors)
    {
        ModsUntracked = modsUntracked;
        ModsTracked = modsTracked;
        ModsDuplicate = modsDuplicate;
        Errors = errors;
    }

    public IReadOnlyCollection<string> ModsUntracked { get; }
    public IReadOnlyCollection<ISkinMod> ModsTracked { get; }

    public IReadOnlyCollection<string> Errors { get; }

    public IReadOnlyCollection<DuplicateMods> ModsDuplicate { get; }

    public readonly struct DuplicateMods
    {
        public DuplicateMods(string existingFolderName, string renamedFolderName)
        {
            ExistingFolderName = existingFolderName;
            RenamedFolderName = renamedFolderName;
        }

        public string ExistingFolderName { get; }
        public string RenamedFolderName { get; }
    }
}