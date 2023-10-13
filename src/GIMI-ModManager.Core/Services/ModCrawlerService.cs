using GIMI_ModManager.Core.GamesService;
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


        var subSkins = _gameService.GetCharacters().SelectMany(character => character.AdditionalSkins).ToArray();

        foreach (var file in RecursiveGetFiles(folder))
        {
            var subSkin = subSkins.FirstOrDefault(skin => IsOfSkinType(file, skin));
            if (subSkin is null) continue;

            _logger.Verbose("Detected subSkin {subSkin} for folder {folder}", subSkin.DisplayName, folder.FullName);

            yield return subSkin;
        }
    }

    public ICharacterSkin? GetFirstSubSkinRecursive(string absPath, ICharacter? checkForCharacter = null)
    {
        var folder = new DirectoryInfo(absPath);
        if (!folder.Exists) throw new DirectoryNotFoundException($"Could not find folder {folder.FullName}");

        var subSkins = checkForCharacter?.AdditionalSkins.ToArray() ?? _gameService
            .GetCharacters()
            .SelectMany(character => character.AdditionalSkins).ToArray();

        foreach (var file in RecursiveGetFiles(folder))
        {
            var subSkin = subSkins.FirstOrDefault(skin => IsOfSkinType(file, skin));
            if (subSkin is null) continue;

            _logger.Verbose("Detected subSkin {subSkin} for folder {folder}", subSkin.DisplayName, folder.FullName);

            return subSkin;
        }

        return null;
    }

    private static readonly string[] ModExtensions = { ".buf", ".dds", ".ib" };

    private static bool IsOfSkinType(FileInfo file, IModdableObject skin)
    {
        var fileExtensionMatch = ModExtensions.Any(extension =>
            file.Extension.Equals(extension, StringComparison.CurrentCultureIgnoreCase));


        return fileExtensionMatch &&
               file.Name.Trim().StartsWith(skin.ModFilesName, StringComparison.CurrentCultureIgnoreCase);
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
}