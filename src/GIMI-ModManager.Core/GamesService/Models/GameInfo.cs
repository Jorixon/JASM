using GIMI_ModManager.Core.GamesService.JsonModels;

namespace GIMI_ModManager.Core.GamesService.Models;

public record GameInfo
{
    internal GameInfo(JsonGame jsonGame, DirectoryInfo assetsDirectoryInfo)
    {
        GameName = jsonGame.GameName?.Trim() ?? "";
        GameShortName = jsonGame.GameShortName?.Trim() ?? "";
        GameIcon = Path.Combine(assetsDirectoryInfo.FullName, "Images", jsonGame.GameIcon?.Trim() ?? "Start_Game.png");
        GameBananaUrl = Uri.TryCreate(jsonGame?.GameBananaUrl, UriKind.Absolute, out var gameBananaUrl)
            ? gameBananaUrl
            : new Uri("https://gamebanana.com/");
        GameModelImporterUrl =
            Uri.TryCreate(jsonGame?.GameModelImporterUrl, UriKind.Absolute, out var gameModelImporterUrl)
                ? gameModelImporterUrl
                : new Uri("https://github.com/SilentNightSound");
    }

    public string GameName { get; }
    public string GameShortName { get; }
    public string GameIcon { get; }
    public Uri GameBananaUrl { get; }
    public Uri GameModelImporterUrl { get; }
}