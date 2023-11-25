namespace GIMI_ModManager.Core.GamesService.Interfaces;

/// <summary>
/// Base Interface for all categories.
/// Each instance gets their own folder in the mods folder.
/// </summary>
public interface IModdableObject : INameable, IEquatable<IModdableObject>
{
    /// <summary>
    /// Static should not be changed.
    /// If Empty => no automatic mod detection
    /// </summary>
    public string ModFilesName { get; }

    /// <summary>
    /// If true => Multiple mods can be active at the same time
    /// </summary>
    public bool IsMultiMod { get; }

    /// <summary>
    /// What category this mod belongs to
    /// </summary>
    public ICategory ModCategory { get; }
}