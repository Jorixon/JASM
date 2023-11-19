using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using Serilog;

namespace GIMI_ModManager.Core.Services;

public class ModCrawlerService
{
    private readonly IGameService _gameService;
    private readonly ILogger _logger;

    public ModCrawlerService(ILogger logger, IGameService gameService)
    {
        _gameService = gameService;
        _logger = logger.ForContext<ModCrawlerService>();
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

    public ICharacterSkin? GetFirstSubSkinRecursive(string absPath, string? internalName = null)
    {
        var folder = new DirectoryInfo(absPath);
        if (!folder.Exists) throw new DirectoryNotFoundException($"Could not find folder {folder.FullName}");


        var characters = _gameService.GetCharacters();


        var subSkins = internalName.IsNullOrEmpty()
            ? characters
                .SelectMany(character => character.Skins)
            : characters.First(ch => ch.InternalNameEquals(internalName)).Skins;

        // Order by default skin first, so that 
        subSkins = subSkins.OrderBy(skin => skin.IsDefault).ToArray();


        foreach (var file in RecursiveGetFiles(folder))
        {
            var subSkin = subSkins.FirstOrDefault(skin => IsOfSkinType(file, skin));
            if (subSkin is null) continue;

            _logger.Verbose("Detected subSkin {subSkin} for folder {folder}", subSkin.InternalName, folder.FullName);

            return subSkin;
        }

        return null;
    }

    private static readonly string[] ModExtensions = { ".buf", ".dds", ".ib" };

    private bool IsOfSkinType(FileInfo file, ICharacterSkin skin)
    {
        var fileExtensionMatch = ModExtensions.Any(extension =>
            file.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase));

        if (skin.ModFilesName.IsNullOrEmpty())
            return false;

        var isMatch = fileExtensionMatch &&
                      file.Name.Trim().StartsWith(skin.ModFilesName, StringComparison.OrdinalIgnoreCase);

        if (isMatch)
            _logger.Verbose("Skin Match: {FileName} ~= {ModFilesName}", file.Name, skin.ModFilesName);

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


    public FileInfo? GetFirstJasmConfigFileAsync(DirectoryInfo directoryInfo)
    {
        return directoryInfo.GetFiles(Constants.ModConfigFileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    public FileInfo? GetMergedIniFile(DirectoryInfo directoryInfo)
    {
        foreach (var file in directoryInfo.EnumerateFiles("*.ini", SearchOption.AllDirectories))
        {
            if (file.Name.Trim().Equals(Constants.MergedIniName, StringComparison.OrdinalIgnoreCase))
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