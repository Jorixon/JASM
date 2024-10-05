using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using Serilog;

namespace GIMI_ModManager.Core.Services;

/// <summary>
/// This service is used to detect what mod is for what moddable object
/// It does this by "crawling" the mod folder and comparing the file names to the moddable object's ModFilesName
/// Though a better approach would be to read the mod files for hashes to be completely certain.
/// </summary>
public class ModCrawlerService
{
    private readonly IGameService _gameService;
    private readonly ILogger _logger;

    public ModCrawlerService(ILogger logger, IGameService gameService)
    {
        _gameService = gameService;
        _logger = logger.ForContext<ModCrawlerService>();
    }


    public IEnumerable<IModdableObject> GetMatchingModdableObjects(
        string absPath, ICollection<IModdableObject>? searchOnlyModdableObjects = null)
    {
        var folder = new DirectoryInfo(absPath);
        if (!folder.Exists) throw new DirectoryNotFoundException($"Could not find folder {folder.FullName}");

        var moddableObjects = searchOnlyModdableObjects ?? _gameService.GetAllModdableObjects();

        foreach (var file in folder.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var moddableObject = moddableObjects.FirstOrDefault(mo => IsOfModType(file, mo));
            if (moddableObject is null) continue;

            _logger.Verbose("Detected moddableObject {moddableObject} for file {file}", moddableObject.InternalName,
                file.FullName);

            yield return moddableObject;
        }
    }

    public IEnumerable<ICharacterSkin> GetSubSkinsRecursive(string absPath)
    {
        var folder = new DirectoryInfo(absPath);
        if (!folder.Exists) throw new DirectoryNotFoundException($"Could not find folder {folder.FullName}");


        var subSkins = _gameService.GetCharacters().SelectMany(character => character.Skins)
            .OrderBy(skin => skin.IsDefault).ToArray();

        foreach (var file in RecursiveGetFiles(folder))
        {
            var subSkin = subSkins.FirstOrDefault(skin => IsOfSkinType(file, skin));
            if (subSkin is null) continue;

            _logger.Verbose("Detected subSkin {subSkin} for folder {folder}", subSkin.InternalName, folder.FullName);

            yield return subSkin;
        }
    }

    public ICharacterSkin? GetFirstSubSkinRecursive(string absPath, string? internalName = null,
        CancellationToken cancellationToken = default)
    {
        var folder = new DirectoryInfo(absPath);
        if (!folder.Exists) throw new DirectoryNotFoundException($"Could not find folder {folder.FullName}");


        var characters = _gameService.GetCharacters();


        var subSkins = internalName.IsNullOrEmpty()
            ? characters.SelectMany(character => character.Skins)
            : characters.First(ch => ch.InternalNameEquals(internalName)).Skins;
        cancellationToken.ThrowIfCancellationRequested();

        // Order by default skin first, so that 
        subSkins = subSkins.OrderBy(skin => skin.IsDefault).ToArray();


        foreach (var file in RecursiveGetFiles(folder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var subSkin = subSkins.FirstOrDefault(skin => IsOfSkinType(file, skin));
            if (subSkin is null) continue;

            _logger.Verbose("Detected subSkin {subSkin} for folder {folder}", subSkin.InternalName, folder.FullName);

            return subSkin;
        }

        return null;
    }


    private bool IsOfSkinType(FileInfo file, ICharacterSkin skin)
        => StartsWithModFilesName(file, skin.ModFilesName);

    private bool IsOfModType(FileInfo file, IModdableObject moddableObject)
        => StartsWithModFilesName(file, moddableObject.ModFilesName);

    public static readonly string[] ModExtensions = { ".buf", ".dds", ".ib" };

    private bool StartsWithModFilesName(FileInfo file, string modFilesName)
    {
        if (modFilesName.IsNullOrEmpty()) return false;

        var fileExtensionMatch = ModExtensions.Any(extension =>
            file.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase));


        var isMatch = fileExtensionMatch &&
                      file.Name.Trim().StartsWith(modFilesName, StringComparison.OrdinalIgnoreCase);

        if (isMatch)
            _logger.Verbose("Mod Match: {FileName} ~= {ModFilesName}", file.Name, modFilesName);

        return isMatch;
    }


    private static IEnumerable<FileInfo> RecursiveGetFiles(DirectoryInfo directoryInfo)
    {
        var files = directoryInfo.GetFiles();

        foreach (var fileInfo in files)
            yield return fileInfo;

        foreach (var directory in directoryInfo.GetDirectories())
            foreach (var directoryFiles in RecursiveGetFiles(directory))
                yield return directoryFiles;
    }


    public FileInfo? GetFirstJasmConfigFileAsync(DirectoryInfo directoryInfo, bool recursive = true)
    {
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        return directoryInfo.GetFiles(Constants.ModConfigFileName, searchOption).FirstOrDefault();
    }

    public FileInfo? GetMergedIniFile(DirectoryInfo directoryInfo)
    {
        foreach (var file in directoryInfo.EnumerateFiles("*.ini", SearchOption.AllDirectories))
        {
            if (Constants.ScriptIniNames.Any(iniNames =>
                    iniNames.Equals(file.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                return file;
        }

        return null;
    }

    public DirectoryInfo? GetShaderFixesFolder(DirectoryInfo directoryInfo)
    {
        foreach (var dir in directoryInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
        {
            if (dir.Name.Trim().Equals(Constants.ShaderFixesFolderName, StringComparison.OrdinalIgnoreCase))
                return dir;
        }

        return null;
    }
}