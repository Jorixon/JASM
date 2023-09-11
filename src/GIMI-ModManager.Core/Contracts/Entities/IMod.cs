using GIMI_ModManager.Core.Entities;

namespace GIMI_ModManager.Core.Contracts.Entities;

/// <summary>
/// The Idea behind this interface was that mods might not be folders but could also be archives or some other format
/// </summary>
public interface IMod : IEqualityComparer<IMod>
{
    /// <summary>
    /// Full path with folder/file name.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// folder/file name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Full path without folder/file name.
    /// </summary>
    public string OnlyPath { get; }

    /// <summary>
    /// Custom name of the mod.
    /// </summary>
    public string CustomName { get; }

    /// <summary>
    /// Set the custom name of the mod.
    /// </summary>
    /// <param name="customName"></param>
    public void SetCustomName(string customName);

    /// <summary>
    /// Move the mod to the specified folder. This does not change the folder name.
    /// If the drive is different, the mod will be copied and then deleted.
    /// </summary>
    /// <param name="absPath">
    /// This is not the same as "mv" command on linux where you can specify a new folder name.
    /// This needs to be an absolute path to the folder you want to move the mod to.
    /// </param>
    public void MoveTo(string absPath);
    /// <summary>
    /// Copies the mod to the specified folder. This does not change the folder name.
    /// </summary>
    /// <param name="absPath"></param>
    /// <returns>The new copied IMod instance</returns>
    public IMod CopyTo(string absPath);

    /// <summary>
    /// Rename the modFolder. This does not change the folder path.
    /// </summary>
    /// <param name="newName"></param>
    public void Rename(string newName);

    public void Delete(bool moveToRecycleBin = true);

    public bool Exists();

    public bool IsEmpty();
    /// <summary>
    /// This uses the contents of the mods to compare them
    /// </summary>
    /// <returns></returns>
    public bool DeepEquals(IMod? x, IMod? y);
    
    public byte[] GetContentsHash();

}