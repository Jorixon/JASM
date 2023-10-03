using GIMI_ModManager.Core.Entities.Genshin;
using Serilog;

namespace GIMI_ModManager.Core.Services;

public class ModCrawlerService
{
    private readonly IGenshinService _genshinService;
    private readonly ILogger _logger;

    public ModCrawlerService(ILogger logger, IGenshinService genshinService)
    {
        _genshinService = genshinService;
        _logger = logger.ForContext<ModCrawlerService>();
    }

    public IEnumerable<ISubSkin> GetSubSkinsRecursive(string absPath)
    {
        var folder = new DirectoryInfo(absPath);
        if (!folder.Exists) throw new DirectoryNotFoundException($"Could not find folder {folder.FullName}");


        var subSkins = _genshinService.GetCharacters().SelectMany(character => character.InGameSkins)
            .OrderBy(skin => skin.DefaultSkin).ToArray();

        foreach (var file in RecursiveGetFiles(folder))
        {
            var subSkin = subSkins.FirstOrDefault(skin => IsOfSkinType(file, skin));
            if (subSkin is null) continue;

            _logger.Debug("Detected subSkin {subSkin} for folder {folder}", subSkin.Name, folder.FullName);

            yield return subSkin;
        }
    }

    public ISubSkin? GetFirstSubSkinRecursive(string absPath, IGenshinCharacter? checkForCharacter = null)
    {
        var folder = new DirectoryInfo(absPath);
        if (!folder.Exists) throw new DirectoryNotFoundException($"Could not find folder {folder.FullName}");

        var subSkins = checkForCharacter?.InGameSkins.OrderBy(skin => skin.DefaultSkin).ToArray() ?? _genshinService
            .GetCharacters()
            .SelectMany(character => character.InGameSkins)
            .OrderBy(skin => skin.DefaultSkin).ToArray();

        foreach (var file in RecursiveGetFiles(folder))
        {
            var subSkin = subSkins.FirstOrDefault(skin => IsOfSkinType(file, skin));
            if (subSkin is null) continue;

            _logger.Debug("Detected subSkin {subSkin} for folder {folder}", subSkin.Name, folder.FullName);

            return subSkin;
        }

        return null;
    }

    private static readonly string[] ModExtensions = { ".buf", ".dds", ".ib" };

    private static bool IsOfSkinType(FileInfo file, ISubSkin skin)
    {
        var fileExtensionMatch = ModExtensions.Any(extension =>
            file.Extension.Equals(extension, StringComparison.CurrentCultureIgnoreCase));


        return fileExtensionMatch && file.Name.Trim().StartsWith(skin.Name, StringComparison.CurrentCultureIgnoreCase);
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