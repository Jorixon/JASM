using System.Text.Json.Serialization;

namespace GIMI_ModManager.Core.GamesService.Interfaces;

/// <summary>
/// In game playable character
/// </summary>
public interface ICharacter : IRarity, IDateSupport, IModdableObject, IEquatable<ICharacter>
{
    /// <summary>
    /// Unmodified version of the character.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [JsonIgnore]
    public ICharacter DefaultCharacter { get; }

    public IGameClass Class { get; }
    public IGameElement Element { get; }
    public ICollection<string> Keys { get; internal set; }

    public ICollection<IRegion> Regions { get; }
    public ICollection<ICharacterSkin> Skins { get; }
}