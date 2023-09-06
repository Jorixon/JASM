#nullable enable
using GIMI_ModManager.Core.Contracts.Entities;
using Microsoft.VisualBasic.FileIO;
using System.Security.Cryptography;
using System.Text;
using SearchOption = System.IO.SearchOption;

namespace GIMI_ModManager.Core.Entities;

public class Mod : IMod
{
    private DirectoryInfo _modDirectory;
    public string FullPath => _modDirectory.FullName;
    public string Name => _modDirectory.Name;
    public string OnlyPath => _modDirectory.Parent!.FullName;
    public string CustomName { get; private set; }

    public Mod(DirectoryInfo modDirectory, string customName = "")
    {
        _modDirectory = modDirectory;
        CustomName = customName;
    }

    public bool Exists()
        => _modDirectory.Exists;

    public bool IsEmpty()
        => !_modDirectory.EnumerateFiles().Any();

    public void SetCustomName(string customName) => CustomName = customName;

    public void MoveTo(string absPath)
    {
        if (!Path.IsPathFullyQualified(absPath))
            throw new ArgumentException("Path must be absolute.", nameof(absPath));

        if (Path.GetPathRoot(absPath) != Path.GetPathRoot(FullPath))
        {
            var newModDirectory = new DirectoryInfo(Path.Combine(absPath, Name));
            RecursiveCopyTo(_modDirectory, newModDirectory);
            _modDirectory.Delete(true);
            _modDirectory = newModDirectory;

            return;
        }


        _modDirectory.MoveTo(Path.Combine(absPath, Name));
    }

    public void Rename(string newName)
    {
        _modDirectory.Refresh();
        _modDirectory.MoveTo(Path.Combine(OnlyPath, newName));
    }

    public void Delete(bool moveToRecycleBin = true)
    {
        if (moveToRecycleBin)
        {
            FileSystem.DeleteDirectory(FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            return;
        }

        _modDirectory.Delete(true);
    }

    private void RecursiveCopyTo(DirectoryInfo oldModDirectory, DirectoryInfo newModDirectory)
    {
        newModDirectory.Create();
        foreach (var file in oldModDirectory.EnumerateFiles())
        {
            file.CopyTo(Path.Combine(newModDirectory.FullName, file.Name));
        }

        foreach (var directory in oldModDirectory.EnumerateDirectories())
        {
            var newSubDirectory = new DirectoryInfo(Path.Combine(newModDirectory.FullName, directory.Name));
            RecursiveCopyTo(directory, newSubDirectory);
        }
    }

    public bool Equals(IMod? x, IMod? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return string.Equals(x.FullPath, y.FullPath, StringComparison.InvariantCultureIgnoreCase);
    }

    public bool DeepEquals(IMod? x, IMod? y)
    {
        if (Equals(x, y)) return true;
        if (x is null || y is null) return false;
        throw new NotImplementedException();
    }

    // https://stackoverflow.com/a/31349703
    public byte[] GetContentsHash()
    {
        _modDirectory.Refresh();
        var filePaths = Directory.GetFiles(_modDirectory.FullName, "*", SearchOption.AllDirectories).ToArray();
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

    public int GetHashCode(IMod obj)
    {
        return StringComparer.InvariantCultureIgnoreCase.GetHashCode(obj.FullPath);
    }

    public override string ToString()
    {
        return $"FolderName: {Name} | CustomName: {CustomName} | FullPath: {FullPath}";
    }
}