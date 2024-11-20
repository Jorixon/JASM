using System.Text.Json.Serialization;

namespace GIMI_ModManager.Core.Services.GameBanana.ApiModels;

public class ApiModProfile
{
    [JsonPropertyName("_idRow")] public int ModId { get; init; } = -1;
    [JsonPropertyName("_sName")] public string? ModName { get; init; }

    [JsonPropertyName("_aSubmitter")] public ApiAuthor? Author { get; init; }
    [JsonPropertyName("_aPreviewMedia")] public ApiImagesRoot? PreviewMedia { get; init; }

    [JsonPropertyName("_sProfileUrl")] public string? ModPageUrl { get; init; }

    [JsonPropertyName("_aFiles")] public ICollection<ApiModFileInfo>? Files { get; init; }
}

public sealed class ApiAuthor
{
    [JsonPropertyName("_sName")] public string? AuthorName { get; init; }
    [JsonPropertyName("_sAvatarUrl")] public string? AvatarImageUrl { get; init; }
    [JsonPropertyName("_sProfileUrl")] public string? ProfileUrl { get; init; }
}

public sealed class ApiImagesRoot
{
    [JsonPropertyName("_aImages")] public ApiImageUrl[] Images { get; init; } = [];
}

public sealed class ApiImageUrl
{
    [JsonPropertyName("_sFile")] public string? ImageId { get; init; }
    [JsonPropertyName("_sBaseUrl")] public string? BaseUrl { get; init; }
}