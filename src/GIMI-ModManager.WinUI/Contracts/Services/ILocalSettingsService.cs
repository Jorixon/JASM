namespace GIMI_ModManager.WinUI.Contracts.Services;

public interface ILocalSettingsService
{
    public string SettingsLocation { get; }
    public string ApplicationDataFolder { get; }
    Task<T?> ReadSettingAsync<T>(string key);

    Task<T> ReadOrCreateSettingAsync<T>(string key) where T : new();

    Task SaveSettingAsync<T>(string key, T value) where T : notnull;

    T? ReadSetting<T>(string key);
}