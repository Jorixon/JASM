using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace GIMI_ModManager.Core.Services;

public sealed class DragAndDropScanner : IDisposable
{
    private readonly string _tmpFolder = Path.Combine(Path.GetTempPath(), "JASM_TMP");
    private readonly string _workFolder = Path.Combine(Path.GetTempPath(), "JASM_TMP", Guid.NewGuid().ToString("N"));
    private string? tmpModFolder;

    public DragAndDropScanner()
    {
    }

    public DragAndDropScanResult Scan(string path)
    {
        PrepareWorkFolder();

        if (IsArchive(path))
        {
            var copiedPath = Path.Combine(_workFolder, Path.GetFileName(path));
            File.Copy(path, copiedPath);
            var result = Extractor(copiedPath);
            result?.Invoke(copiedPath);
        }
        else if (Directory.Exists(path))
        {
            var modFolder = new Mod(new DirectoryInfo(path));
            modFolder.MoveTo(_workFolder);
            tmpModFolder = modFolder.FullPath;
        }
        else
        {
            throw new Exception("No valid mod folder or archive found");
        }




        var extractedDirs = new DirectoryInfo(_workFolder).EnumerateDirectories().ToArray();
        if (extractedDirs is null || !extractedDirs.Any())
            throw new Exception("No valid mod folder found in archive. Loose files are ignored");

        var ignoredDirs = new List<DirectoryInfo>();
        if (extractedDirs.Length > 1)
            ignoredDirs.AddRange(extractedDirs.Skip(1));

        var newMod = new Mod(extractedDirs.First());

        //newMod.MoveTo(_tmpFolder);

        return new DragAndDropScanResult()
        {
            ExtractedMod = newMod,
            IgnoredMods = ignoredDirs.Select(dir => dir.Name).ToArray()
        };
    }

    private void PrepareWorkFolder()
    {
        Directory.CreateDirectory(_tmpFolder);
        Directory.CreateDirectory(_workFolder);
    }

    private bool IsArchive(string path)
    {
        return Path.GetExtension(path) switch
        {
            ".zip" => true,
            ".rar" => true,
            ".7z" => true,
            _ => false
        };
    }

    private Action<string>? Extractor(string path)
    {
        Action<string>? action = Path.GetExtension(path) switch
        {
            ".zip" => ExtractZip,
            ".rar" => ExtractRar,
            ".7z" => Extract7z,
            _ => null
        };

        return action;
    }

    private void ExtractEntries(IArchive archive)
    {
        foreach (var entry in archive.Entries)
        {
            entry.WriteToDirectory(_workFolder, new ExtractionOptions()
            {
                ExtractFullPath = true,
                Overwrite = true,
                PreserveFileTime = false
            });
        }
    }

    private void ExtractZip(string path)
    {
        using var archive = ZipArchive.Open(path);
        ExtractEntries(archive);
    }


    private void ExtractRar(string path)
    {
        using var archive = RarArchive.Open(path);
        ExtractEntries(archive);
    }

    // ReSharper disable once InconsistentNaming
    private void Extract7z(string path)
    {
        using var archive = ArchiveFactory.Open(path);
        ExtractEntries(archive);
    }

    private bool IsRootModFolder(DirectoryInfo folder)
    {
        foreach (var fileSystemInfo in folder.GetFileSystemInfos())
        {
            var extension = Path.GetExtension(fileSystemInfo.Name);
            if (extension.Equals(".ini"))
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        Directory.Delete(_workFolder, true);
        if (tmpModFolder is not null && Path.Exists(tmpModFolder))
            Directory.Delete(tmpModFolder, true);
    }
}

public class DragAndDropScanResult
{
    public IMod ExtractedMod { get; init; } = null!;
    public string[] IgnoredMods { get; init; } = Array.Empty<string>();
    public string[] IgnoredFiles { get; init; } = Array.Empty<string>();
    public string? ThumbnailPath { get; set; }
}