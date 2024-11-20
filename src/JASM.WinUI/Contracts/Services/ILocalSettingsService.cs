using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.Contracts.Services;

public interface ILocalSettingsService
{
    /// <summary>
    /// JASM/ folder
    /// </summary>
    public string GameScopedSettingsLocation { get; }

    /// <summary>
    /// JASM/ApplicationData_Genshin
    /// </summary>
    public string ApplicationDataFolder { get; }

    public void SetApplicationDataFolderName(string folderName);

    public Task<T?> ReadSettingAsync<T>(string key, SettingScope settingScope = SettingScope.Game);

    public Task<T> ReadOrCreateSettingAsync<T>(string key, SettingScope settingScope = SettingScope.Game)
        where T : new();

    public Task SaveSettingAsync<T>(string key, T value, SettingScope settingScope = SettingScope.Game)
        where T : notnull;

    public T? ReadSetting<T>(string key, SettingScope settingScope = SettingScope.Game);
}