using System.Text.Json.Serialization;

namespace GIMI_ModManager.Core.Services.GameBanana.ApiModels;

public class ApiModFileInfo
{
    [JsonPropertyName("_idRow")] public int FileId { get; init; } = -1;
    [JsonPropertyName("_sFile")] public string FileName { get; init; } = null!;
    [JsonPropertyName("_sDownloadUrl")] public string DownloadUrl { get; init; } = null!;

    [JsonPropertyName("_tsDateAdded")] public int DateAdded { get; init; } = -1;

    [JsonPropertyName("_sDescription")] public string Description { get; init; } = null!;

    [JsonPropertyName("_nFilesize")] public int FileSize { get; init; } = -1;

    [JsonPropertyName("_sAnalysisResultCode")]
    public string AnalysisResultCode { get; init; } = null!;

    [JsonPropertyName("_sMd5Checksum")] public string Md5Checksum { get; init; } = null!;

    [JsonPropertyName("_nDownloadCount")] public int DownloadCount { get; init; } = -1;
}