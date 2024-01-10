using System.Diagnostics;
using Serilog;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using System.Security.Cryptography;
using System.Text;

namespace GIMI_ModManager.Core.Services;

public class ArchiveService
{
    private readonly ILogger _logger;
    private readonly ExtractTool _extractTool;

    public ArchiveService(ILogger logger)
    {
        _logger = logger.ForContext<ArchiveService>();
        _extractTool = GetExtractTool();
    }

    public DirectoryInfo ExtractArchive(string archivePath, string destinationPath)
    {
        var archive = new FileInfo(archivePath);
        if (!archive.Exists)
            throw new FileNotFoundException("Archive not found", archivePath);

        if (!IsArchive(archivePath))
            throw new InvalidOperationException("File is not an archive");

        var destinationDirectory = Directory.CreateDirectory(destinationPath);

        var extractedFolder = Path.Combine(destinationDirectory.FullName, archive.Name);

        if (Directory.Exists(extractedFolder))
            throw new InvalidOperationException("Destination folder already exists, could not extract folder");

        Directory.CreateDirectory(extractedFolder);

        var extractor = Extractor(extractedFolder);

        extractor?.Invoke(archive.FullName, extractedFolder);

        return new DirectoryInfo(extractedFolder);
    }

    // https://stackoverflow.com/a/31349703
    public byte[] GetContentsHash(string path)
    {

        var filePaths = new List<string>();

        if (File.Exists(path))
            filePaths.Add(path);
        else if (Directory.Exists(path))
            filePaths.AddRange(Directory.GetFiles(path, "*", SearchOption.AllDirectories));
        else
            throw new FileNotFoundException("File or directory not found", path);


        using var md5 = MD5.Create();
        foreach (var filePath in filePaths)
        {
            // hash path
            var pathBytes = Encoding.UTF8.GetBytes(filePath);
            md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

            // hash contents
            var contentBytes = File.ReadAllBytes(filePath);

            md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
        }

        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        return md5.Hash ?? Array.Empty<byte>();
    }

    public bool IsHashEqual(byte[] hash1, byte[] hash2)
    {
        return hash1.SequenceEqual(hash2);
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

    private Action<string, string>? Extractor(string archivePath)
    {
        Action<string, string>? action = null;

        if (_extractTool == ExtractTool.Bundled7Zip)
            action = Extract7Z;
        else if (_extractTool == ExtractTool.SharpCompress)
            action = Path.GetExtension(archivePath) switch
            {
                ".zip" => SharpExtract,
                ".rar" => SharpExtract,
                ".7z" => SharpExtract,
                _ => null
            };
        else if (_extractTool == ExtractTool.System7Zip) throw new NotImplementedException();

        return action;
    }

    private void ExtractEntries(IArchive archive, string extractPath)
    {
        _logger.Information("Extracting {ArchiveType} archive", archive.Type);
        foreach (var entry in archive.Entries)
        {
            _logger.Debug("Extracting {EntryName}", entry.Key);
            entry.WriteToDirectory(extractPath, new ExtractionOptions()
            {
                ExtractFullPath = true,
                Overwrite = true,
                PreserveFileTime = false
            });
        }
    }

    private void SharpExtract(string archivePath, string extractPath)
    {
        using var archive = ZipArchive.Open(archivePath);
        ExtractEntries(archive, extractPath);
    }


    private enum ExtractTool
    {
        Bundled7Zip, // 7zip bundled with JASM
        SharpCompress, // SharpCompress library
        System7Zip // 7zip installed on the system
    }

    private ExtractTool GetExtractTool()
    {
        var bundled7ZFolder = Path.Combine(AppContext.BaseDirectory, @"Assets\7z\");
        if (File.Exists(Path.Combine(bundled7ZFolder, "7z.exe")) &&
            File.Exists(Path.Combine(bundled7ZFolder, "7-zip.dll")) &&
            File.Exists(Path.Combine(bundled7ZFolder, "7z.dll")))
        {
            _logger.Debug("Using bundled 7zip");
            return ExtractTool.Bundled7Zip;
        }

        _logger.Information("Bundled 7zip not found, using SharpCompress library");
        return ExtractTool.SharpCompress;
    }


    private void Extract7Z(string archivePath, string extractPath)
    {
        var sevenZipPath = Path.Combine(AppContext.BaseDirectory, @"Assets\7z\7z.exe");
        var process = new Process
        {
            StartInfo =
            {
                FileName = sevenZipPath,
                Arguments = $"x \"{archivePath}\" -o\"{extractPath}\" -y",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        _logger.Information("Extracting 7z archive with command: {Command}", process.StartInfo.Arguments);
        process.Start();
        process.WaitForExit();
        _logger.Information("7z extraction finished with exit code {ExitCode}", process.ExitCode);
    }
}