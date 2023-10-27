using Newtonsoft.Json;

namespace GIMI_ModManager.Core.GamesService.Interfaces;

public interface ICharacterSkin : IRarity, IImageSupport, INameable
{
    /// <summary>
    /// Character this skin belongs to
    /// </summary>
    [JsonIgnore]
    public ICharacter Character { get; }

    /// <summary>
    /// Is default skin for character
    /// </summary>
    public bool IsDefault { get; }

    public DateTime? ReleaseDate { get; }

    /// <summary>
    /// Static should not be changed.
    /// If Empty => no automatic mod detection
    /// </summary>
    public string ModFilesName { get; }
}