using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.ModHandling;
// Start with installing (and unzipping) the mod to the temp folder, before this process starts

// If a single folder
// Check if the mod already exists in the mods folder
// If yes -> Ask if the user wants to overwrite the mod or rename the mod or this mod

// If multiple folders / files
// show a dedicated window for the user to decide what to do with the files and folders, set mod name and so on

// Then move the mod to the mods folder
public class ModFolderManager
{
    private readonly ILogger _logger;
    private readonly ModDragAndDropService _modDragAndDropService;

    private DirectoryInfo _workFolder;

    public ModFolderManager(ILogger logger, ModDragAndDropService modDragAndDropService)
    {
        _logger = logger;
        _modDragAndDropService = modDragAndDropService;
    }

    public Task StartModInstallationAsync(DirectoryInfo modFolder)
    {
        throw new NotImplementedException();
    }
}

public sealed class ModInstallation : IDisposable
{
    private readonly ICharacterModList _destinationModList;
    private readonly ModCrawlerService _modCrawlerService = App.GetService<ModCrawlerService>();
    private readonly DirectoryInfo _originalModFolder;
    private readonly List<FileStream> _lockedFiles = new();


    public DirectoryInfo ModFolder { get; private set; }
    private DirectoryInfo? _shaderFixesFolder;
    private List<FileInfo> _shaderFixesFiles = new();


    private ModInstallation(DirectoryInfo originalModFolder, ICharacterModList destinationModList)
    {
        _originalModFolder = originalModFolder;
        _destinationModList = destinationModList;
        ModFolder = new DirectoryInfo(originalModFolder.FullName);
        LockFiles();
    }

    private void LockFiles()
    {
        foreach (var fileInfo in _originalModFolder.GetFiles("*", SearchOption.AllDirectories))
        {
            var fileStream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            _lockedFiles.Add(fileStream);
        }
    }

    // Lock mod folder
    public static ModInstallation Start(DirectoryInfo modFolder, ICharacterModList destinationModList)
    {
        return new ModInstallation(modFolder, destinationModList);
    }


    public void SetRootModFolder(DirectoryInfo newRootFolder)
    {
        if (newRootFolder.FullName == ModFolder.FullName)
            return;

        if (newRootFolder.FullName == _shaderFixesFolder?.FullName)
            throw new ArgumentException("The new root folder is the same as the current shader fixes folder");

        if (!newRootFolder.Exists)
            throw new DirectoryNotFoundException($"The folder {newRootFolder.FullName} does not exist");

        ModFolder = newRootFolder;
    }

    public void SetShaderFixesFolder(DirectoryInfo shaderFixesFolder)
    {
        if (shaderFixesFolder.FullName == ModFolder.FullName)
            throw new ArgumentException("The new shader fixes folder is the same as the current root folder");

        if (!shaderFixesFolder.Exists)
            throw new DirectoryNotFoundException($"The folder {shaderFixesFolder.FullName} does not exist");

        _shaderFixesFiles.Clear();
        _shaderFixesFiles.AddRange(shaderFixesFolder.GetFiles("*.txt", SearchOption.TopDirectoryOnly));
        _shaderFixesFolder = shaderFixesFolder;
    }


    public DirectoryInfo? AutoSetModRootFolder()
    {
        var jasmConfigFile = _modCrawlerService.GetFirstJasmConfigFileAsync(_originalModFolder);
        DirectoryInfo? modRootFolder = null;
        if (jasmConfigFile is not null)
        {
            modRootFolder = new DirectoryInfo(jasmConfigFile.DirectoryName!);
        }
        else
        {
            var mergedIniFile = _modCrawlerService.GetMergedIniFile(_originalModFolder);
            if (mergedIniFile is not null)
                modRootFolder = new DirectoryInfo(mergedIniFile.DirectoryName!);
        }

        if (modRootFolder is null)
            return null;

        SetRootModFolder(modRootFolder);
        return modRootFolder;
    }

    public DirectoryInfo? AutoSetShaderFixesFolder()
    {
        var shaderFixesFolder = _modCrawlerService.GetShaderFixesFolder(_originalModFolder);
        if (shaderFixesFolder is null)
            return null;

        SetShaderFixesFolder(shaderFixesFolder);
        return shaderFixesFolder;
    }


    public ISkinMod? AnyDuplicateName()
    {
        var skinEntries = _destinationModList.Mods;

        foreach (var skinEntry in skinEntries)
        {
            if (ModFolderHelpers.FolderNameEquals(skinEntry.Mod.Name, ModFolder.Name))
                return skinEntry.Mod;
        }

        return null;
    }

    public void RenameAndAdd()
    {
    }

    public void AddAndReplace()
    {
    }

    public void AddModAsync()
    {
    }

    public void Dispose()
    {
        foreach (var fileStream in _lockedFiles)
            fileStream.Dispose();
    }
}