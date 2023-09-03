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
}