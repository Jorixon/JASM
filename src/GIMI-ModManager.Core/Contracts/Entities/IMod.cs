using GIMI_ModManager.Core.Entities;

namespace GIMI_ModManager.Core.Contracts.Entities;

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
    /// </summary>
    /// <param name="absPath"></param>
    public void MoveTo(string absPath);

    /// <summary>
    /// Rename the modFolder.
    /// </summary>
    /// <param name="newName"></param>
    public void Rename(string newName);

    public void Delete(bool moveToRecycleBin = true);

    public bool Exists();

    public bool IsEmpty();
    public bool DeepEquals(IMod? x, IMod? y);
    
    public byte[] GetContentsHash();

}