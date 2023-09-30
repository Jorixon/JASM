using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.RateLimiting;
using Polly.Registry;
using Serilog;

namespace GIMI_ModManager.Core.Services;

public class GameBanana : IModUpdateChecker
{
    private readonly ILogger _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResiliencePipeline _resiliencePipeline;
    public const string HttpClientName = "GameBanana";

    private const string DownloadUrl = "https://gamebanana.com/dl/";
    private const string DownloadsApiUrl = "https://gamebanana.com/apiv11/Mod/";

    public GameBanana(ILogger logger, IHttpClientFactory httpClientFactory,
        ResiliencePipelineProvider<string> resiliencePipelineProvider)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger.ForContext<GameBanana>();
        _resiliencePipeline = resiliencePipelineProvider.GetPipeline(HttpClientName);
    }

    public async Task<ModsRetrievedResult> CheckForUpdatesAsync(Uri url, DateTime lastCheck,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (url.Scheme != "https" || url.Host != "gamebanana.com")
            throw new ArgumentException("Invalid GameBanana url", nameof(url));

        // Get DownloadsApiUrl
        var modId = GetModIdFromUrl(url);

        if (modId == null)
        {
            _logger.Error("Failed to get modId from url: {url}", url);
            throw new ArgumentException("Failed to get modId from url, invalid GameBanana url?", nameof(url));
        }

        // Check if update is available

        var downloadsApiUrl = GetDownloadsApiUrl(modId);

        var client = _httpClientFactory.CreateClient();
        HttpResponseMessage response = null!;

        retry:
        try
        {
            await _resiliencePipeline.ExecuteAsync(
                    async (ct) => { response = await client.GetAsync(downloadsApiUrl, ct).ConfigureAwait(false); },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RateLimiterRejectedException e)
        {
            _logger.Debug("Rate limit exceeded, retrying after {retryAfter}", e.RetryAfter);
            var delay = e.RetryAfter ?? TimeSpan.FromSeconds(1);

            await Task.Delay(delay, cancellationToken);
            goto retry;
        }


        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to get mod info from GameBanana: {response}", response);
            throw new HttpRequestException(
                $"Failed to get mod info from GameBanana. Reason: {response?.ReasonPhrase ?? "Unknown"}");
        }

        _logger.Debug("Got response from GameBanana: {response}", response.StatusCode);
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);

        var apiMods =
            await JsonSerializer.DeserializeAsync<ApiRootResponse>(content, cancellationToken: cancellationToken);


        if (apiMods == null)
        {
            _logger.Error("Failed to deserialize GameBanana response: {content}", content);
            throw new HttpRequestException(
                $"Failed to deserialize GameBanana response. Reason: {response?.ReasonPhrase}");
        }

        return ApiToResultMapper.Map(apiMods, lastCheck, url);
    }

    private static string? GetModIdFromUrl(Uri url)
    {
        var segments = url.Segments;
        if (segments.Length < 2) return null;

        var modId = segments.Last();
        return modId;
    }

    private Uri GetDownloadsApiUrl(string modPageId)
    {
        return new Uri(DownloadsApiUrl + modPageId + "/DownloadPage");
    }
}

public interface IModUpdateChecker
{
    public Task<ModsRetrievedResult> CheckForUpdatesAsync(Uri url, DateTime lastCheck,
        CancellationToken cancellationToken);
}

public sealed class ApiRootResponse
{
    [JsonPropertyName("_aFiles")] public List<ApiFiles> Files { get; set; } = new(0);
}

public sealed class ApiFiles
{
    [JsonPropertyName("_idRow")] public int? Id { get; set; }

    [JsonPropertyName("_sFile")] public string? FileName { get; set; }

    [JsonPropertyName("_sDescription")] public string? Description { get; set; }

    [JsonPropertyName("_tsDateAdded")] public long? DateAddedUnixTimeStamp { get; set; }
    [JsonPropertyName("_sMd5Checksum")] public string? Md5Checksum { get; set; }
}

public record UpdateCheckResult
{
    public UpdateCheckResult(bool isNewer, string fileName, string description, DateTime dateAdded,
        string md5Checksum)
    {
        IsNewer = isNewer;
        FileName = fileName;
        Description = description;
        DateAdded = dateAdded;
        Md5Checksum = md5Checksum;
    }

    public bool IsNewer { get; }
    public string FileName { get; }
    public string Description { get; }
    public DateTime DateAdded { get; }
    public string Md5Checksum { get; }
}

public record ModsRetrievedResult
{
    public Uri SitePageUrl { get; init; } = null!;
    public bool AnyNewMods { get; init; }
    public ICollection<UpdateCheckResult> Mods { get; init; } = Array.Empty<UpdateCheckResult>();
}

internal static class ApiToResultMapper
{
    internal static ModsRetrievedResult Map(ApiRootResponse apiResponse, DateTime lastCheck, Uri SitePageUrl)
    {
        var updateCheckResults = apiResponse.Files.Select(apiFile =>
        {
            var fileName = apiFile.FileName ?? "";
            var description = apiFile.Description ?? "";

            var dateAdded = DateTime.MinValue;
            if (apiFile.DateAddedUnixTimeStamp != null)
                dateAdded = DateTimeOffset.FromUnixTimeSeconds(apiFile.DateAddedUnixTimeStamp.Value).LocalDateTime;


            var isNewer = dateAdded > lastCheck;

            var md5Checksum = apiFile.Md5Checksum ?? "";

            return new UpdateCheckResult(isNewer, fileName, description, dateAdded, md5Checksum);
        }).ToList();

        var anyNewMods = updateCheckResults.Any(result => result.IsNewer);

        return new ModsRetrievedResult
        {
            SitePageUrl = SitePageUrl,
            AnyNewMods = anyNewMods,
            Mods = updateCheckResults
        };
    }
}