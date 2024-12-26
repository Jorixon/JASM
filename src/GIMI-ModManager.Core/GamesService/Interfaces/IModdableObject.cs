namespace GIMI_ModManager.Core.GamesService.Interfaces;

/// <summary>
/// Base Interface for all categories.
/// Each instance gets their own folder in the mods folder.
/// </summary>
public interface IModdableObject : INameable, IEquatable<IModdableObject>, IImageSupport
{
    /// <summary>
    /// Static should not be changed.
    /// If Empty => no automatic mod detection
    /// </summary>
    public string ModFilesName { get; internal set; }

    /// <summary>
    /// If true => Multiple mods can be active at the same time
    /// </summary>
    public bool IsMultiMod { get; internal set; }

    /// <summary>
    /// What category this mod belongs to
    /// </summary>
    public ICategory ModCategory { get; }

    /// <summary>
    /// If true then the mod object is created by the user
    /// </summary>
    public bool IsCustomModObject { get; }
}