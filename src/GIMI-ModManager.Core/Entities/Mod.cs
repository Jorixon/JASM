#nullable enable
using GIMI_ModManager.Core.Contracts.Entities;
using Microsoft.VisualBasic.FileIO;

namespace GIMI_ModManager.Core.Entities;

public sealed class Mod : IMod
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

    public bool DeepEquals(Mod? x, Mod? y)
    {
        if (!Equals(x, y)) return false;
        throw new NotImplementedException();
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