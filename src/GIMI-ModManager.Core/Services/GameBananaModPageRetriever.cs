using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.RateLimiting;
using Polly.Registry;
using Serilog;

namespace GIMI_ModManager.Core.Services;

public class GameBananaModPageRetriever : IModUpdateChecker
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline _resiliencePipeline;
    public const string HttpClientName = "GameBanana";

    private const string DownloadUrl = "https://gamebanana.com/dl/";
    private const string ApiUrl = "https://gamebanana.com/apiv11/Mod/";

    public GameBananaModPageRetriever(ILogger logger, HttpClient httpClient,
        ResiliencePipelineProvider<string> resiliencePipelineProvider)
    {
        _httpClient = httpClient;
        _logger = logger.ForContext<GameBananaModPageRetriever>();
        _resiliencePipeline = resiliencePipelineProvider.GetPipeline(HttpClientName);
    }


    public async Task<ModsRetrievedResult> CheckForUpdatesAsync(Uri url, DateTime lastCheck,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(lastCheck);

        ValidateGameBananaUrl(url);

        // Get DownloadsApiUrl
        var modId = GetModIdFromUrl(url);

        if (modId == null)
        {
            _logger.Error("Failed to get modId from url: {url}", url);
            throw new ArgumentException("Failed to get modId from url, invalid GameBanana url?", nameof(url));
        }

        // Check if update is available
        var downloadsApiUrl = GetDownloadsApiUrl(modId);

        var response = await SendRequest(cancellationToken, downloadsApiUrl);

        _logger.Debug("Got response from GameBanana: {response}", response.StatusCode);
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var apiMods =
            await JsonSerializer.DeserializeAsync<ApiRootResponse>(contentStream,
                cancellationToken: cancellationToken);


        if (apiMods == null)
        {
            _logger.Error("Failed to deserialize GameBanana response: {content}", contentStream);
            throw new HttpRequestException(
                $"Failed to deserialize GameBanana response. Reason: {response?.ReasonPhrase}");
        }

        return ApiToResultMapper.Map(apiMods, lastCheck, url);
    }


    public async Task<ModPageDataResult> GetModPageDataAsync(Uri url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        ValidateGameBananaUrl(url);

        // Get DownloadsApiUrl
        var modId = GetModIdFromUrl(url);

        if (modId == null)
        {
            _logger.Error("Failed to get modId from url: {url}", url);
            throw new ArgumentException("Failed to get modId from url, invalid GameBanana url?", nameof(url));
        }


        var modPageApiUrl = GetModPageApiUrl(modId);

        var response = await SendRequest(cancellationToken, modPageApiUrl);

        _logger.Debug("Got response from GameBanana: {response}", response.StatusCode);
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var apiModPage =
            await JsonSerializer.DeserializeAsync<ApiRootModPage>(contentStream,
                cancellationToken: cancellationToken);


        if (apiModPage == null)
        {
            _logger.Error("Failed to deserialize GameBanana response: {content}", contentStream);
            throw new HttpRequestException(
                $"Failed to deserialize GameBanana response. Reason: {response?.ReasonPhrase}");
        }


        List<Uri>? previewImageUrls = null;
        if (apiModPage.PreviewMedia is not null)
        {
            foreach (var previewMediaImage in apiModPage.PreviewMedia.Images)
            {
                var imageUrl = Uri.TryCreate(previewMediaImage.BaseUrl + "/" + previewMediaImage.ImageId,
                    UriKind.Absolute, out var uri)
                    ? uri
                    : null;


                if (imageUrl is null ||
                    imageUrl.Scheme != Uri.UriSchemeHttps ||
                    !imageUrl.Host.Equals("images.gamebanana.com", StringComparison.OrdinalIgnoreCase)) continue;

                previewImageUrls ??= new List<Uri>();

                previewImageUrls.Add(imageUrl);
            }
        }


        return new ModPageDataResult(url)
        {
            ModName = apiModPage.ModName?.Trim(),
            AuthorName = apiModPage.Author?.ModName?.Trim(),
            PreviewImages = previewImageUrls?.ToArray()
        };
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
        return new Uri(ApiUrl + modPageId + "/DownloadPage");
    }

    private Uri GetModPageApiUrl(string modPageId)
    {
        return new Uri(ApiUrl + modPageId + "/ProfilePage");
    }

    private static void ValidateGameBananaUrl(Uri url)
    {
        if (!url.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
            !url.Host.Equals("gamebanana.com", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid GameBanana url: {url}", nameof(url));
    }

    private async Task<HttpResponseMessage?> SendRequest(CancellationToken cancellationToken, Uri downloadsApiUrl)
    {
        HttpResponseMessage response;
        retry:
        try
        {
            await Task.Delay(200, cancellationToken);
            response = await _resiliencePipeline.ExecuteAsync(
                    (ct) => new ValueTask<HttpResponseMessage>(_httpClient.GetAsync(downloadsApiUrl, ct)),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RateLimiterRejectedException e)
        {
            _logger.Verbose("Rate limit exceeded, retrying after {retryAfter}", e.RetryAfter);
            var delay = e.RetryAfter ?? TimeSpan.FromSeconds(2);

            await Task.Delay(delay, cancellationToken);
            goto retry;
        }


        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to get mod info from GameBanana: {response} | Url: {Url}", response,
                downloadsApiUrl);
            throw new HttpRequestException(
                $"Failed to get mod info from GameBanana. Reason: {response?.ReasonPhrase ?? "Unknown"} | Url: {downloadsApiUrl}");
        }

        return response;
    }
}

public interface IModUpdateChecker
{
    public Task<ModsRetrievedResult> CheckForUpdatesAsync(Uri url, DateTime lastCheck,
        CancellationToken cancellationToken = default);

    public Task<ModPageDataResult> GetModPageDataAsync(Uri url, CancellationToken cancellationToken = default);
}

public class ModPageDataResult
{
    public ModPageDataResult(Uri sitePageUrl)
    {
        SitePageUrl = sitePageUrl;
    }

    public DateTime CheckTime { get; init; } = DateTime.Now;
    public Uri SitePageUrl { get; }

    public string? ModName { get; init; }
    public string? AuthorName { get; init; }
    public Uri[]? PreviewImages { get; init; }
}

public sealed class ApiRootModPage
{
    [JsonPropertyName("_sName")] public string? ModName { get; set; }

    [JsonPropertyName("_aSubmitter")] public ApiAuthor? Author { get; set; }
    [JsonPropertyName("_aPreviewMedia")] public ApiImagesRoot? PreviewMedia { get; set; }
}

public sealed class ApiAuthor
{
    [JsonPropertyName("_sName")] public string? ModName { get; set; }
}

public sealed class ApiImagesRoot
{
    [JsonPropertyName("_aImages")] public ApiImageUrl[] Images { get; set; } = Array.Empty<ApiImageUrl>();
}

public sealed class ApiImageUrl
{
    [JsonPropertyName("_sFile")] public string? ImageId { get; set; }
    [JsonPropertyName("_sBaseUrl")] public string? BaseUrl { get; set; }
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

    public bool IsNewer { get; set; }
    public string FileName { get; }
    public string Description { get; }
    public DateTime DateAdded { get; }
    public TimeSpan Age => DateTime.Now - DateAdded;
    public string Md5Checksum { get; }
}

public record ModsRetrievedResult
{
    public DateTime CheckTime { get; init; }
    public DateTime LastCheck { get; init; }
    public Uri SitePageUrl { get; init; } = null!;
    public bool AnyNewMods { get; init; }
    public ICollection<UpdateCheckResult> Mods { get; init; } = Array.Empty<UpdateCheckResult>();
}

internal static class ApiToResultMapper
{
    internal static ModsRetrievedResult Map(ApiRootResponse apiResponse, DateTime lastCheck, Uri sitePageUrl)
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
        }).ToArray();

        var anyNewMods = updateCheckResults.Any(result => result.IsNewer);

        return new ModsRetrievedResult
        {
            CheckTime = DateTime.Now,
            LastCheck = lastCheck,
            SitePageUrl = sitePageUrl,
            AnyNewMods = anyNewMods,
            Mods = updateCheckResults
        };
    }
}