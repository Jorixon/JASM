namespace GIMI_ModManager.Core.GamesService.Interfaces;

/// <summary>
/// In game playable character
/// </summary>
public interface ICharacter : IRarity, IImageSupport, IModdableObject, IEquatable<ICharacterSkin>
{
    public IGameClass Class { get; }
    public IGameElement Element { get; }
    public ICollection<string> Keys { get; }

    public DateTime ReleaseDate { get; }
    public ICollection<IRegion> Regions { get; }
    public ICollection<ICharacterSkin> Skins { get; }
}