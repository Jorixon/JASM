using System.Collections.Concurrent;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using Serilog;
using SharpCompress.Common;

namespace GIMI_ModManager.WinUI.Services;

public class DownloadCacheService
{
    private readonly ILogger _logger;
    private readonly ArchiveService _archiveService;

    private readonly List<DownloadedModCacheEntry> _cache = new();

    private readonly object _cacheLock = new();

    public DownloadCacheService(ILogger logger, ArchiveService archiveService)
    {
        _logger = logger.ForContext<DownloadCacheService>();
        _archiveService = archiveService;
    }

    public void CacheArchive(string filePath, string? fileId)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        if (fileId is not null && fileId.Trim() == string.Empty)
            throw new ArgumentException("FileId must be null or not empty", nameof(fileId));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Archive not found", filePath);

        var checksum = _archiveService.GetContentsHash(filePath);
        var checksumString = BitConverter.ToString(checksum).Replace("-", string.Empty).ToLowerInvariant();

        var cacheEntry = new DownloadedModCacheEntry(fileId, filePath, Path.GetFileName(filePath), checksumString);

        lock (_cacheLock)
        {
            if (fileId is null)
            {
                var existingEntry = _cache.FirstOrDefault(x => x.Checksum == checksumString);
                if (existingEntry is not null)
                    _cache.Remove(existingEntry);
            }
            else
            {
                var existingEntry = _cache.FirstOrDefault(x => x.FileId == fileId);
                if (existingEntry is not null)
                    _cache.Remove(existingEntry);
            }

            _cache.Add(cacheEntry);
        }
    }

    public DownloadedModCacheEntry? GetCachedArchive(string fileId)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileId);

        DownloadedModCacheEntry? cacheHit;

        lock (_cacheLock)
        {
            cacheHit = _cache.FirstOrDefault(x => x.FileId == fileId);

            if (cacheHit is not null && !cacheHit.Exists)
            {
                _cache.Remove(cacheHit);
                cacheHit = null;
            }
        }


        return cacheHit;
    }

    public DownloadedModCacheEntry? GetCachedArchiveByChecksum(string checksum)
    {
        ArgumentException.ThrowIfNullOrEmpty(checksum);

        DownloadedModCacheEntry? cacheHit;

        lock (_cacheLock)
        {
            cacheHit = _cache.FirstOrDefault(x => x.Checksum == checksum);

            if (cacheHit is not null && !cacheHit.Exists)
            {
                _cache.Remove(cacheHit);
                cacheHit = null;
            }
        }


        return cacheHit;
    }
}

public class DownloadedModCacheEntry
{
    public DownloadedModCacheEntry(string? fileId, string archivePath, string modFileName, string checksum)
    {
        FileId = fileId;
        ArchivePath = archivePath;
        ModFileName = modFileName;
        Checksum = checksum;
    }

    public bool Exists => File.Exists(ArchivePath);
    public string? FileId { get; }
    public string ArchivePath { get; }
    public string ModFileName { get; }
    public string Checksum { get; }

    public DirectoryInfo? ExtractedFolder { get; private set; }

    public DirectoryInfo SetExtractedFolder(string extractedDirectory)
    {
        if (!Exists)
            throw new InvalidOperationException("Archive does not exist");

        if (!Directory.Exists(extractedDirectory))
            throw new DirectoryNotFoundException("Extracted directory does not exist");

        if (ExtractedFolder is not null)
            throw new InvalidOperationException("Extracted folder already set");

        ExtractedFolder = new DirectoryInfo(extractedDirectory);

        return ExtractedFolder;
    }
}