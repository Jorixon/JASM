using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using GIMI_ModManager.Core.Services.GameBanana.Models;
using Serilog;

namespace GIMI_ModManager.Core.Services.GameBanana;

public sealed class ModArchiveRepository
{
    private readonly ILogger _logger;
    private readonly ArchiveService _archiveService;


    private readonly ConcurrentDictionary<string, ModArchiveHandle> _modArchives = new();

    private DirectoryInfo _settingsDirectory = null!;
    private DirectoryInfo _modArchiveDirectory = null!;
    private const string ModArchiveDirectoryName = "ModDownloads";

    private const string DownloadTempPrefix = ".TMP_DOWNLOAD";
    public const string Separator = "_!!_";

    private readonly int _MaxDirectorySizeGb = 10;

    public ModArchiveRepository(ILogger logger, ArchiveService archiveService)
    {
        _archiveService = archiveService;
        _logger = logger.ForContext<ModArchiveRepository>();
    }

    public async Task InitializeAsync(string appDataFolder)
    {
        _settingsDirectory = new DirectoryInfo(appDataFolder);
        _settingsDirectory.Create();

        _modArchiveDirectory = new DirectoryInfo(Path.Combine(appDataFolder, ModArchiveDirectoryName));
        _modArchiveDirectory.Create();

        foreach (var archive in _modArchiveDirectory.EnumerateFiles())
        {
            try
            {
                var archiveHandle = ModArchiveHandle.FromManagedFile(EnsureValidArchive(archive.FullName));

                if (_modArchives.ContainsKey(archiveHandle.FullName))
                {
                    archive.Delete();
                    continue;
                }

                TrackArchive(archiveHandle);
            }
            catch (InvalidArchiveNameFormatException)
            {
                archive.Delete();
            }
        }

        var _ = Task.Run(RemoveUntilUnderMaxSize);
    }


    public async Task<ModArchiveHandle> CopyAndTrackModArchiveAsync(string archivePath,
        GbModFileIdentifier modFileIdentifier,
        CancellationToken cancellationToken = default)
    {
        var archiveFile = EnsureValidArchive(archivePath);

        await foreach (var modArchiveHandle in GetModArchivesAsync(cancellationToken).ConfigureAwait(false))
        {
            if (modArchiveHandle.FullName.Equals(archiveFile.FullName, StringComparison.OrdinalIgnoreCase))
                return modArchiveHandle;
        }

        var archiveHash = await _archiveService.CalculateFileMd5HashAsync(archiveFile.FullName, cancellationToken)
            .ConfigureAwait(false);
        var newName = await GetManagedArchiveNameAsync(archiveFile.Name, modFileIdentifier, archiveHash)
            .ConfigureAwait(false);

        archiveFile = archiveFile.CopyTo(Path.Combine(_modArchiveDirectory.FullName, newName), true);

        var archiveHandle = ModArchiveHandle.FromManagedFile(archiveFile);
        TrackArchive(archiveHandle);
        return archiveHandle;
    }


    public async Task<ModArchiveHandle> CreateAndTrackModArchiveAsync(
        Func<FileStream, Task<string>> streamToWrite, GbModFileIdentifier modIdentifier,
        CancellationToken cancellationToken = default)
    {
        var newFile =
            new FileInfo(Path.Combine(_modArchiveDirectory.FullName, $"{DownloadTempPrefix}_{Guid.NewGuid()}"));

        if (newFile.Exists)
            newFile.Delete();


        try
        {
            var fileStream = newFile.Create();

            var archiveName = "";
            await using (fileStream.ConfigureAwait(false))
            {
                archiveName = await streamToWrite(fileStream).ConfigureAwait(false);
            }

            var archiveHash = await _archiveService.CalculateFileMd5HashAsync(newFile.FullName, cancellationToken)
                .ConfigureAwait(false);

            var newName = await GetManagedArchiveNameAsync(archiveName, modIdentifier, archiveHash)
                .ConfigureAwait(false);

            newFile.MoveTo(Path.Combine(_modArchiveDirectory.FullName, newName), true);
        }
        catch (Exception e)
        {
            newFile.Delete();

            _logger.Error(e, "Failed to download mod archive");
            throw;
        }


        var archiveHandle = ModArchiveHandle.FromManagedFile(newFile);
        TrackArchive(archiveHandle);
        return archiveHandle;
    }

    public async Task<ModArchiveHandle?> FirstOrDefaultAsync(Predicate<ModArchiveHandle> predicate,
        CancellationToken cancellationToken = default)
    {
        await foreach (var archiveHandle in GetModArchivesAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (predicate(archiveHandle))
            {
                return archiveHandle;
            }
        }

        return null;
    }

    public async IAsyncEnumerable<ModArchiveHandle> GetModArchivesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var archives = _modArchives.ToArray();

        foreach (var archiveHandle in archives)
        {
            cancellationToken.ThrowIfCancellationRequested();
            archiveHandle.Value.Refresh();

            if (!archiveHandle.Value.Exists)
            {
                _modArchives.TryRemove(archiveHandle.Key, out _);
                continue;
            }

            yield return archiveHandle.Value;
        }
    }


    private void TrackArchive(ModArchiveHandle archiveHandle)
    {
        ArgumentNullException.ThrowIfNull(archiveHandle);
        _modArchives.AddOrUpdate(archiveHandle.FullName, (_) => archiveHandle, (_, _) => archiveHandle);
    }

    private Task<string> GetManagedArchiveNameAsync(string archiveFileName, GbModFileIdentifier modFileIdentifier,
        string? archiveHash)
    {
        ArgumentNullException.ThrowIfNull(modFileIdentifier);
        var archiveName = Path.GetFileNameWithoutExtension(archiveFileName);
        var archiveExtension = Path.GetExtension(archiveFileName);

        var hash = archiveHash ?? "|REPLACE|"; //TODO: Implement hash calculation

        return Task.FromResult(
            $"{archiveName}{Separator}{modFileIdentifier.ModId}{Separator}{modFileIdentifier.ModFileId}{Separator}{hash}{archiveExtension}");
    }

    private Task<string> GetManagedArchiveNameAsync(ModArchiveHandle archiveHandle)
    {
        var modId = new GbModId(archiveHandle.ModId);
        var modFileId = new GbModFileId(archiveHandle.ModFileId);

        var archiveHash = archiveHandle.MD5Hash;

        return GetManagedArchiveNameAsync(archiveHandle.Name, new GbModFileIdentifier(modId, modFileId), archiveHash);
    }

    private static FileInfo EnsureValidArchive(string archivePath)
    {
        var archiveFileName = Path.GetFileNameWithoutExtension(archivePath);
        var archiveExtension = Path.GetExtension(archivePath);

        if (string.IsNullOrEmpty(archiveFileName) || string.IsNullOrEmpty(archiveExtension))
            throw new ArgumentException("Invalid archive file path", nameof(archivePath));

        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found", archivePath);

        var archiveFile = new FileInfo(archivePath);

        return archiveFile;
    }


    private void RemoveUntilUnderMaxSize()
    {
        while (_modArchives.ToArray().Sum(x => x.Value.SizeInGb) > _MaxDirectorySizeGb)
        {
            var smallest = _modArchives.ToArray().MinBy(x => x.Value.SizeInGb);
            _modArchives.TryRemove(smallest.Key, out _);

            if (smallest.Value.Exists)
                File.Delete(smallest.Value.FullName);
            smallest.Value.Refresh();
        }
    }
}

public class ModArchiveHandle
{
    private readonly FileInfo _archiveFile;

    public string ModId { get; private set; }
    public string ModFileId { get; private set; }
    public string ModFileName { get; private set; }
    public string MD5Hash { get; private set; }

    public string FullName => _archiveFile.FullName.ToLowerInvariant();

    public string Name => _archiveFile.Name.ToLowerInvariant();

    public bool Exists => _archiveFile.Exists;

    public int SizeInGb => (int)Math.Ceiling((decimal)_archiveFile.Length / 1024 / 1024 / 1024);

    public void Refresh() => _archiveFile.Refresh();

    private ModArchiveHandle(string modId, string modFileId, string modFileName, string md5Hash, FileInfo archiveFile)
    {
        ModId = modId;
        ModFileId = modFileId;
        ModFileName = modFileName;
        MD5Hash = md5Hash;
        _archiveFile = archiveFile;
    }

    internal static ModArchiveHandle FromManagedFile(FileInfo archiveFile)
    {
        var archiveFileName = Path.GetFileNameWithoutExtension(archiveFile.Name);

        var sections = archiveFileName.Split(ModArchiveRepository.Separator);

        if (sections.Length != 4)
            throw new InvalidArchiveNameFormatException();

        var modFileName = sections[0];
        var modId = sections[1];
        var modFileId = sections[2];
        var md5Hash = sections[3];

        if (string.IsNullOrEmpty(modFileName) || string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(modFileId) ||
            string.IsNullOrEmpty(md5Hash))
            throw new InvalidArchiveNameFormatException();


        return new ModArchiveHandle(modId, modFileId, modFileName, md5Hash, archiveFile);
    }
}

public class InvalidArchiveNameFormatException : Exception
{
    public InvalidArchiveNameFormatException(string message) : base(message)
    {
    }

    public InvalidArchiveNameFormatException() : base("Invalid archive name format")
    {
    }
}

// Archive has filename: ModFileName_!!_ModId_!!_MD5Hash.<extension>
public class ModIdentifier
{
    public string? ModId { get; private init; }
    public string? ModFileName { get; private init; }

    public string? MD5Hash { get; private init; }

    private ModIdentifier()
    {
    }

    public static ModIdentifier FromArchiveFileName(string archiveFileName)
    {
        var parts = archiveFileName.Split("__");
        if (parts.Length != 3)
            throw new ArgumentException("Invalid archive file name", nameof(archiveFileName));

        return new ModIdentifier
        {
            ModFileName = parts[0],
            ModId = parts[1],
            MD5Hash = parts[2]
        };
    }

    [MemberNotNull(nameof(ModId))] //TODO: Test if this works
    public static ModIdentifier FromModId(string modId)
    {
        return new ModIdentifier
        {
            ModId = modId
        };
    }

    public static ModIdentifier FromModIdAndFileName(string modId, string modFileName)
    {
        return new ModIdentifier
        {
            ModId = modId,
            ModFileName = modFileName
        };
    }

    public static ModIdentifier FromMD5Hash(string md5Hash)
    {
        return new ModIdentifier
        {
            MD5Hash = md5Hash
        };
    }
}