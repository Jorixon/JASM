using GIMI_ModManager.Core.GamesService.Models;

namespace GIMI_ModManager.Core.Contracts;

public class CreateCharacterRequest
{
    public required InternalName InternalName { get; set; }
    public required string DisplayName { get; set; }
    public string[]? Keys { get; set; }
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Will be copied when new character is created
    /// </summary>
    public Uri? Image { get; set; }

    public required int Rarity { get; set; }
    public string? Element { get; set; }

    public string? Class { get; set; }

    public string[]? Region { get; set; }

    /// <summary>
    /// Defaults to internalName
    /// </summary>
    public string? ModFilesName { get; set; }

    public bool IsMultiMod { get; set; }
}