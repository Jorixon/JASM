using GIMI_ModManager.Core.GamesService.JsonModels;
using GIMI_ModManager.Core.GamesService;
using Newtonsoft.Json;

namespace GIMI_ModManager.Core.Contracts;

public class CreateCharacterRequest
{
    public required string InternalName { get; init; }
    public required string DisplayName { get; init; }
    public required string[] Keys { get; set; }
    public required DateTime ReleaseDate { get; set; }

    /// <summary>
    /// Will be copied when new character is created
    /// </summary>
    public string? Image { get; init; }

    public required int Rarity { get; init; }
    public string? Element { get; init; }

    public string? Class { get; init; }

    public string[]? Region { get; init; }

    /// <summary>
    /// Defaults to internalName
    /// </summary>
    public string? ModFilesName { get; init; }
    public bool IsMultiMod { get; init; }
}