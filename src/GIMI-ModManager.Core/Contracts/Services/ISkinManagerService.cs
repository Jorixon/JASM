using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Services;

namespace GIMI_ModManager.Core.Contracts.Services;

public interface ISkinManagerService : IDisposable
{
    public string UnloadedModsFolderPath { get; }
    public string ActiveModsFolderPath { get; }
    public IReadOnlyCollection<ICharacterModList> CharacterModLists { get; }
    public void ScanForMods();
    public ICharacterModList GetCharacterModList(GenshinCharacter character);

    public void Initialize(string activeModsFolderPath, string? unloadedModsFolderPath,
        string? threeMigotoRootfolder = null);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="characterFolderToReorganize">If null, reorganize all mods outside of characters mod folders</param>
    /// <returns>Mods moved</returns>
    public int ReorganizeMods(GenshinCharacter? characterFolderToReorganize = null);

    /// <summary>
    /// This looks for mods in characters mod folder that are not tracked by the mod manager and adds them to the mod manager.
    /// </summary>
    public void RefreshMods(GenshinCharacter? refreshForCharacter = null);

    public void TransferMods(ICharacterModList source, ICharacterModList destination, IEnumerable<Guid> modsEntryIds);

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

}

public enum SetModStatus
{
    KeepCurrent,
    EnableAllMods,
    DisableAllMods
}