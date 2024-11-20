using GIMI_ModManager.Core.GamesService.Models;

namespace GIMI_ModManager.Core.GamesService.Interfaces;

/// <summary>
/// Base Interface that allows identification by internal name
/// </summary>
public interface INameable : IEquatable<INameable>
{
    /// <summary>
    /// Is displayed to the user.
    /// Can also be customized by user
    /// </summary>
    public string DisplayName { get; internal set; }

    /// <summary>
    /// Should not be changed. Is used to identify the object
    /// </summary>
    public InternalName InternalName { get; internal init; }

    /// <summary>
    /// InternalName comparison is case-insensitive, use this when comparing different INameable objects
    /// </summary>
    public bool InternalNameEquals(string? other) => InternalName.Equals(other);

    /// <inheritdoc cref="InternalNameEquals(string?)"/>
    public bool InternalNameEquals(INameable other) => InternalNameEquals(other.InternalName);
}