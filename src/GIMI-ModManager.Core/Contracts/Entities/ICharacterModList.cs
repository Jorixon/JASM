using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Genshin;

namespace GIMI_ModManager.Core.Contracts.Entities;

public interface ICharacterModList
{
    public string AbsModsFolderPath { get; }
    public string DisabledPrefix { get; }

    /// <summary>
    /// All mods for this character that have added to the mod list.
    /// </summary>
    public IReadOnlyCollection<CharacterSkinEntry> Mods { get; }

    /// <summary>
    /// The character this mod list is for.
    /// </summary>
    public GenshinCharacter Character { get; }

    /// <summary>
    /// Add a mod to the mod list. Starts tracking the mod.
    /// </summary>
    /// <param name="mod"></param>
    internal void TrackMod(ISkinMod mod);

    /// <summary>
    /// Remove a mod from the mod list. Stops tracking the mod.
    /// </summary>
    /// <param name="mod"></param>
    internal void UnTrackMod(IMod mod);

    /// <summary>
    /// Enable a mod. This enables the mod.
    /// </summary>
    public void EnableMod(Guid modId);

    /// <summary>
    /// Disable a mod. This disables the mod.
    /// </summary>
    public void DisableMod(Guid modId);

    public bool IsModEnabled(IMod mod);

    public void SetCustomModName(Guid modId, string newName);

    public bool IsMultipleModsActive(bool perSkin = false);


    /// <summary>
    /// When folders are added, removed or renamed from the mod folder, this event is fired.
    /// </summary>
    public event EventHandler<ModFolderChangedArgs>? ModsChanged;

    /// <summary>
    /// Returns a disposable DisableWatcher that disables the watcher for this mod list until it is disposed.
    /// </summary>
    /// <returns></returns>
    public DisableWatcher DisableWatcher();

    /// <summary>
    /// Checks to see if a folder already exists in the mods folder. With our without the disabled prefix.
    /// </summary>
    /// <param name="folderName"></param>
    /// <returns>True if a folder already exists</returns>
    public bool FolderAlreadyExists(string folderName);

    /// <summary>
    /// Permanently deletes a mod from the mod list. This deletes entire mod from the mod folder.
    /// </summary>
    public void DeleteMod(Guid modId, bool moveToRecycleBin = true);

    public void DeleteModBySkinEntryId(Guid skinEntryId, bool moveToRecycleBin = true);
}