using System.Diagnostics;
using System.Net;
using System.Text.Json;
using GIMI_ModManager.Core.Services.GameBanana.ApiModels;
using GIMI_ModManager.Core.Services.GameBanana.Models;
using Polly;
using Polly.RateLimiting;
using Polly.Registry;
using Serilog;

namespace GIMI_ModManager.Core.Services.GameBanana;

public sealed class ApiGameBananaClient(
    ILogger logger,
    HttpClient httpClient,
    ResiliencePipelineProvider<string> resiliencePipelineProvider)
    : IApiGameBananaClient
{
    private readonly ILogger _logger = logger.ForContext<ApiGameBananaClient>();
    private readonly HttpClient _httpClient = httpClient;
    private readonly ResiliencePipeline _resiliencePipeline = resiliencePipelineProvider.GetPipeline(HttpClientName);
    public const string HttpClientName = "GameBanana";

    private const string DownloadUrl = "https://gamebanana.com/dl/";
    private const string ApiUrl = "https://gamebanana.com/apiv11/Mod/";
    private const string HealthCheckUrl = "https://gamebanana.com/apiv11";

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(HealthCheckUrl, cancellationToken).ConfigureAwait(false);

        foreach (var (key, value) in response.Headers)
        {
            if (key.Contains("Deprecation", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Deprecated", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning("GameBanana API is deprecated: {Key}={Value}", key, value);
                Debugger.Break();
                break;
            }
        }

        return response.StatusCode == HttpStatusCode.OK;
    }

    public async Task<ApiModProfile?> GetModProfileAsync(GbModId modId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modId);

        var modPageApiUrl = GetModInfoUrl(modId);

        using var response = await SendRequest(modPageApiUrl, cancellationToken).ConfigureAwait(false);

        _logger.Debug("Got response from GameBanana: {response}", response.StatusCode);
        await using var contentStream =
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var apiResponse =
            await JsonSerializer.DeserializeAsync<ApiModProfile>(contentStream,
                cancellationToken: cancellationToken).ConfigureAwait(false);


        if (apiResponse == null)
        {
            _logger.Error("Failed to deserialize GameBanana response: {content}", contentStream);
            throw new HttpRequestException(
                $"Failed to deserialize GameBanana response. Reason: {response?.ReasonPhrase}");
        }

        return apiResponse;
    }

    public async Task<ApiModFilesInfo?> GetModFilesInfoAsync(GbModId modId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modId);

        var requestUrl = GetModFilesInfoUrl(modId);

        using var response = await SendRequest(requestUrl, cancellationToken).ConfigureAwait(false);

        _logger.Debug("Got response from GameBanana: {response}", response.StatusCode);
        await using var contentStream =
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var apiResponse =
            await JsonSerializer.DeserializeAsync<ApiModFilesInfo>(contentStream,
                cancellationToken: cancellationToken).ConfigureAwait(false);


        if (apiResponse == null)
        {
            _logger.Error("Failed to deserialize GameBanana response: {content}", contentStream);
            throw new HttpRequestException(
                $"Failed to deserialize GameBanana response. Reason: {response?.ReasonPhrase}");
        }

        return apiResponse;
    }

    public async Task<ApiModFileInfo?> GetModFileInfoAsync(GbModId modId, GbModFileId modFileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modFileId);

        var modFilesInfo = await GetModFilesInfoAsync(modId, cancellationToken).ConfigureAwait(false);

        return modFilesInfo?.Files.FirstOrDefault(x => x.FileId.ToString() == modFileId);
    }

    public async Task<bool> ModFileExists(GbModFileId modFileId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modFileId);

        var requestUrl = GetAltUrlForModInfo(modFileId);

        using var response = await SendRequest(requestUrl, cancellationToken).ConfigureAwait(false);

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return !content.Contains("error:", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri GetAltUrlForModInfo(GbModFileId modFileId)
    {
        return new Uri(
            $"https://api.gamebanana.com/Core/Item/Data?itemid={modFileId}&itemtype=File&fields=file");
    }

    public async Task DownloadModAsync(GbModFileId modFileId, FileStream destinationFile, IProgress<int>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modFileId, nameof(modFileId));
        ArgumentNullException.ThrowIfNull(destinationFile);
        var downloadUrl = DownloadUrl + modFileId;


        using var response = await _httpClient
            .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("Mod not found.");

        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException(
                $"Failed to download mod from GameBanana. Reason: {response?.ReasonPhrase}");

        var contentLength = response.Content.Headers.ContentLength;

        await using var downloadStream =
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        if (contentLength is not null && progress is not null)
            _ = Task.Run(() => DownloadMonitor(contentLength.Value, destinationFile.Name, progress, cancellationToken),
                cancellationToken);

        await downloadStream.CopyToAsync(destinationFile, cancellationToken).ConfigureAwait(false);
    }

    private static async Task DownloadMonitor(long totalSizeBytes, string downloadFilePath, IProgress<int> progress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var file = new FileInfo(downloadFilePath);
            while (!cancellationToken.IsCancellationRequested && file.Length < totalSizeBytes)
            {
                file.Refresh();
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                var fileSize = file.Length;
                progress.Report((int)Math.Round((decimal)fileSize / (decimal)totalSizeBytes * 100));
            }
        }
        catch (Exception e)
        {
#if DEBUG
            throw;
#endif
        }
    }

    private Uri GetModFilesInfoUrl(GbModId gbModId)
    {
        return new Uri(ApiUrl + gbModId + "/DownloadPage");
    }

    private Uri GetModInfoUrl(GbModId gbModId)
    {
        return new Uri(ApiUrl + gbModId + "/ProfilePage");
    }

    private async Task<HttpResponseMessage> SendRequest(Uri downloadsApiUrl, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        retry:
        try
        {
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);

            // Use anonymous state object to avoid closure allocation
            var state = new { url = downloadsApiUrl, httpClient = _httpClient };


            response = await _resiliencePipeline.ExecuteAsync(
                    async (context, token) => await context.httpClient.GetAsync(context.url, token)
                        .ConfigureAwait(false),
                    state, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RateLimiterRejectedException e)
        {
            _logger.Debug("Rate limit exceeded, retrying after {retryAfter}", e.RetryAfter);
            var delay = e.RetryAfter ?? TimeSpan.FromSeconds(2);

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            goto retry;
        }


        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Failed to get mod info from GameBanana: {response} | Url: {Url}", response,
                downloadsApiUrl);
            throw new HttpRequestException(
                $"Failed to get mod info from GameBanana. Reason: {response?.ReasonPhrase ?? "Unknown"} | Url: {downloadsApiUrl}");
        }

        _logger.Debug("Response received {0} | {1}", DateTime.Now, downloadsApiUrl);

        return response;
    }
}