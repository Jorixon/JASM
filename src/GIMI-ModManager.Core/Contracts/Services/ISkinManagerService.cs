using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities;

namespace GIMI_ModManager.Core.Contracts.Services;

public interface ISkinManagerService
{
    public string UnloadedModsFolderPath { get; }
    public string ActiveModsFolderPath { get; }
    public IReadOnlyCollection<ICharacterModList> CharacterModLists { get; }
    public void ScanForMods();
    public ICharacterModList GetCharacterModList(GenshinCharacter character);
    public void Initialize(string activeModsFolderPath, string? unloadedModsFolderPath);

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

    /// <summary>
    /// Exports mods to a user specified folder.
    /// </summary>
    /// <param name="characterModLists">Mods from characters to export</param>
    /// <param name="exportPath">Folder to extract to</param>
    /// <param name="keepLocalJasmSettings"></param>
    /// <param name="zip"></param>
    /// <param name="keepCharacterFolderStructure"></param>
    /// <returns></returns>
    public void ExportMods(ICollection<ICharacterModList> characterModLists, string exportPath,
        bool keepLocalJasmSettings = true, bool zip = true, bool keepCharacterFolderStructure = false);
}