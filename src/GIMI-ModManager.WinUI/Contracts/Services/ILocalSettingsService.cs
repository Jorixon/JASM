namespace GIMI_ModManager.WinUI.Contracts.Services;

public interface ILocalSettingsService
{
    public string SettingsLocation { get; }
    Task<T?> ReadSettingAsync<T>(string key);

    Task SaveSettingAsync<T>(string key, T value);

    T? ReadSetting<T>(string key);
}
