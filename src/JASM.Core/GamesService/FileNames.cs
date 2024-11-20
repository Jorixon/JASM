namespace GIMI_ModManager.Core.GamesService;

internal static class FileNames
{
    internal const string CustomSettingsFileName = "CustomSettings.json";
    internal const string GameSettingsFileName = "game.json";
    internal const string ElementSettingsFileName = "elements.json";

    internal static class GenshinNames
    {
        internal const string GameClass = "weaponClasses.json";
    }

    internal static class HonkaiNames
    {
        internal const string GameClass = "paths.json";
    }
}

public enum SupportedGames
{
    Genshin,
    Honkai,
    WuWa,
    ZZZ
}