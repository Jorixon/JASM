namespace GIMI_ModManager.WinUI.Contracts.Services;

public interface ILocalSettingsService
{
    /// <summary>
    /// JASM/ folder
    /// </summary>
    public string SettingsLocation { get; }

    /// <summary>
    /// JASM/ApplicationData_Genshin
    /// </summary>
    public string ApplicationDataFolder { get; }

    public void SetApplicationDataFolderName(string folderName);

    Task<T?> ReadSettingAsync<T>(string key);

    Task<T> ReadOrCreateSettingAsync<T>(string key) where T : new();

    Task SaveSettingAsync<T>(string key, T value) where T : notnull;

    T? ReadSetting<T>(string key);
}