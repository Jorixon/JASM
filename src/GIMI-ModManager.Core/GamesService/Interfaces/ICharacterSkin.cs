namespace GIMI_ModManager.Core.GamesService.Interfaces;

public interface ICharacterSkin : IModdableObject, IRarity, IImageSupport, INameable
{
    /// <summary>
    /// Character this skin belongs to
    /// </summary>
    public ICharacter Character { get; }

    /// <summary>
    /// Is default skin for character
    /// </summary>
    public bool IsDefault { get; }

    public DateTime? ReleaseDate { get; }
}