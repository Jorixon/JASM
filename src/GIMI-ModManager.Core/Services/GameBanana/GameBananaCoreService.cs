using System.Collections.Concurrent;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services.GameBanana.ApiModels;
using GIMI_ModManager.Core.Services.GameBanana.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace GIMI_ModManager.Core.Services.GameBanana;

/// <summary>
/// A Higher level service that provides functionality to interact with GameBanana. That uses caching to reduce the number of API calls.
/// </summary>
public sealed class GameBananaCoreService(
    IServiceProvider serviceProvider,
    ILogger logger,
    ModArchiveRepository modArchiveRepository)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger _logger = logger.ForContext<GameBananaCoreService>();
    private readonly ModArchiveRepository _modArchiveRepository = modArchiveRepository;

    private readonly ApiGameBananaCache _cache = new(cacheDuration: TimeSpan.FromMinutes(10));
    private readonly ConcurrentDictionary<Uri, DownloadHandle> _downloadHandles = new();


    private IApiGameBananaClient CreateApiGameBananaClient() =>
        _serviceProvider.GetRequiredService<IApiGameBananaClient>();

    /// <summary>
    /// Checks if the GameBanana API is reachable
    /// </summary>
    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        var apiGameBananaClient = CreateApiGameBananaClient();

        return await apiGameBananaClient.HealthCheckAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the profile of a mod from GameBanana. Uses caching to reduce the number of API calls.
    /// The return type <see cref="ModPageInfo"/> also contains mod files info <see cref="ModFileInfo"/>
    /// </summary>
    public async Task<ModPageInfo?> GetModProfileAsync(GbModId modId, CancellationToken ct = default)
    {
        var cachedModProfile = _cache.Get<ModPageInfo>(modId);

        if (cachedModProfile != null)
            return cachedModProfile;

        var apiGameBananaClient = CreateApiGameBananaClient();

        var apiModProfile = await apiGameBananaClient.GetModProfileAsync(modId, ct).ConfigureAwait(false);

        if (apiModProfile == null)
            return null;


        var modInfo = new ModPageInfo(apiModProfile);

        _cache.Set(modId, modInfo);

        return modInfo;
    }

    /// <summary>
    ///  Tries to get a locally cached mod archive by its MD5 hash
    /// </summary>
    public async Task<ModArchiveHandle?> GetLocalModArchiveByMd5HashAsync(string md5Hash,
        CancellationToken ct = default) =>
        await _modArchiveRepository.FirstOrDefaultAsync(modFile => modFile.MD5Hash == md5Hash, ct)
            .ConfigureAwait(false);

    /// <summary>
    /// Gets the files info of a mod from GameBanana. Uses caching to reduce the number of API calls. Is more lightweight than <see cref="GetModProfileAsync"/>>
    /// </summary>
    /// <param name="modId"></param>
    /// <param name="ignoreCache"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<ModFileInfo>?> GetModFilesInfoAsync(GbModId modId, bool ignoreCache = false,
        CancellationToken ct = default)
    {
        var apiGameBananaClient = CreateApiGameBananaClient();

        var modFilesInfo = await GetModFilesInfoAsync(apiGameBananaClient, modId, ignoreCache: ignoreCache, ct: ct)
            .ConfigureAwait(false);

        if (modFilesInfo == null)
            return null;

        return new List<ModFileInfo>(modFilesInfo.Files.Select(x => new ModFileInfo(x, modId)));
    }

    /// <summary>
    /// Makes requests in parallel
    /// </summary>
    public async Task<Dictionary<GbModFileId, bool>> ModFilesExists(
        IEnumerable<GbModFileId> modFileIdentifier, CancellationToken ct = default)
    {
        var apiGameBananaClient = CreateApiGameBananaClient();


        var results = new ConcurrentDictionary<GbModFileId, bool>();

        await Parallel.ForEachAsync(modFileIdentifier, ct, async (i, cancellationToken) =>
        {
            var modFileExists = _cache.Get<ExistsResult>(i.ModFileId);

            if (modFileExists is null)
            {
                var result = await apiGameBananaClient.ModFileExists(i, cancellationToken)
                    .ConfigureAwait(false);

                modFileExists = new ExistsResult { Exists = result };

                _cache.Set(i.ModFileId, modFileExists);
            }

            results[i] = modFileExists.Exists;
        }).ConfigureAwait(false);

        return new Dictionary<GbModFileId, bool>(results);
    }

    private class ExistsResult
    {
        public required bool Exists { get; init; }
    }

    private readonly AsyncLock _downloadLock = new();

    /// <summary>
    /// Downloads a mod from GameBanana. Uses caching to reduce the number of API calls and checks archive cache before downloading. Also checks for duplicate downloads.
    /// </summary>
    /// <param name="modFileIdentifier"></param>
    /// <param name="progress">An IProgress that can be used to monitor progress. Goes from 0 to 100</param>
    /// <param name="ct"></param>
    /// <returns>The Absolute path to the downloaded archive</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<string> DownloadModAsync(GbModFileIdentifier modFileIdentifier, IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var cachedDataUsed = true;
        var modFilesInfo = _cache.Get<ApiModFilesInfo>(modFileIdentifier.ModId);
        var apiGameBananaClient = CreateApiGameBananaClient();

        if (modFilesInfo is null)
        {
            modFilesInfo = await GetModFilesInfoAsync(apiGameBananaClient, modFileIdentifier.ModId, ct: ct)
                .ConfigureAwait(false);

            if (modFilesInfo == null)
                throw new InvalidOperationException($"Mod with id {modFileIdentifier.ModId} not found");

            cachedDataUsed = false;
        }

        var modFileInfo = modFilesInfo.Files.FirstOrDefault(x => x.FileId.ToString() == modFileIdentifier.ModFileId);
        if (modFileInfo is null)
        {
            if (cachedDataUsed)
                // Mod file not found in cache, try to get it directly from the API
                modFileInfo = (await GetModFilesInfoAsync(apiGameBananaClient, modFileIdentifier.ModId, true, ct)
                        .ConfigureAwait(false))
                    ?.Files.FirstOrDefault(x => x.FileId.ToString() == modFileIdentifier.ModFileId);

            if (modFileInfo is null)
                throw new InvalidOperationException($"Mod file with id {modFileIdentifier.ModFileId} not found");
        }


        var modArchiveHandle =
            await GetLocalModArchiveByMd5HashAsync(modFileInfo.Md5Checksum, ct).ConfigureAwait(false);

        if (modArchiveHandle != null)
            // Mod archive already exists locally
            return modArchiveHandle.FullName;

        var downloadUri = Uri.TryCreate(modFileInfo.DownloadUrl, UriKind.Absolute, out var parsedDownloadUrl)
            ? parsedDownloadUrl
            : null;
        Task<ModArchiveHandle> downloadTask;

        // Prevent duplicate downloads from the same url
        using (var _ = await _downloadLock.LockAsync(cancellationToken: ct).ConfigureAwait(false))
        {
            if (downloadUri is not null && _downloadHandles.TryGetValue(downloadUri, out var downloadHandle))
            {
                modArchiveHandle = await downloadHandle.DownloadTask.ConfigureAwait(false);
                return modArchiveHandle.FullName;
            }

            downloadTask = _modArchiveRepository.CreateAndTrackModArchiveAsync(
                async (fileStream) =>
                {
                    await apiGameBananaClient.DownloadModAsync(modFileIdentifier.ModFileId, fileStream, progress, ct)
                        .ConfigureAwait(false);
                    return modFileInfo.FileName;
                }, modFileIdentifier, cancellationToken: ct);

            if (downloadUri is not null)
            {
                var handle = new DownloadHandle()
                {
                    DownloadUri = downloadUri,
                    DownloadTask = downloadTask
                };

                _downloadHandles.AddOrUpdate(downloadUri, handle, (_, _) => handle);
            }
        }

        try
        {
            modArchiveHandle = await downloadTask.ConfigureAwait(false);
        }
        finally
        {
            if (downloadUri is not null)
                // In case the task was cancelled or faulted
                _downloadHandles.TryRemove(downloadUri, out _);
        }


        return modArchiveHandle.FullName;
    }


    private async Task<ApiModFilesInfo?> GetModFilesInfoAsync(
        IApiGameBananaClient apiClient, GbModId modId,
        bool ignoreCache = false, CancellationToken ct = default)
    {
        if (!ignoreCache)
        {
            var cachedModFilesInfo = _cache.Get<ApiModFilesInfo>(modId);
            if (cachedModFilesInfo != null)
                return cachedModFilesInfo;
        }


        var modFilesInfo = await apiClient.GetModFilesInfoAsync(modId, ct)
            .ConfigureAwait(false);

        if (modFilesInfo == null)
            return null;

        _cache.Set(modId, modFilesInfo);
        return modFilesInfo;
    }
}

public sealed class DownloadHandle()
{
    public required Uri DownloadUri { get; init; }
    public required Task<ModArchiveHandle> DownloadTask { get; init; }
}