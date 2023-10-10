using Windows.Storage;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Models.Options;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private const string _defaultApplicationDataFolder = "JASM/ApplicationData";
    private const string _defaultLocalSettingsFile = "LocalSettings.json";

    private readonly IFileService _fileService;
    private readonly LocalSettingsOptions _options;

    private readonly string _localApplicationData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private readonly string _applicationDataFolder;
    private readonly string _localsettingsFile;

    private IDictionary<string, object> _settings;

    private bool _isInitialized;

    public string SettingsLocation =>
        RuntimeHelper.IsMSIX
            ? "Stored in ApplicationData"
            : Path.Combine(_applicationDataFolder, _localsettingsFile);

    public string ApplicationDataFolder => _applicationDataFolder;

    public LocalSettingsService(IFileService fileService, IOptions<LocalSettingsOptions> options)
    {
        _fileService = fileService;
        _options = options.Value;
#if DEBUG
        _applicationDataFolder = Path.Combine(_localApplicationData,
            _options.ApplicationDataFolder ?? _defaultApplicationDataFolder) + "_Debug";

#else
        _applicationDataFolder = Path.Combine(_localApplicationData,
            _options.ApplicationDataFolder ?? _defaultApplicationDataFolder);
#endif
        _localsettingsFile = _options.LocalSettingsFile ?? _defaultLocalSettingsFile;

        _settings = new Dictionary<string, object>();
    }

    private async Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _settings = await Task.Run(() =>
                            _fileService.Read<Dictionary<string, object>>(_applicationDataFolder,
                                _localsettingsFile)) ??
                        new Dictionary<string, object>();

            _isInitialized = true;
        }
    }

    public async Task<T?> ReadSettingAsync<T>(string key)
    {
        if (RuntimeHelper.IsMSIX)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
                return await Json.ToObjectAsync<T>((string)obj).ConfigureAwait(false);
        }
        else
        {
            await InitializeAsync();

            if (_settings != null && _settings.TryGetValue(key, out var obj))
                return await Json.ToObjectAsync<T>((string)obj).ConfigureAwait(false);
        }

        return default;
    }

    public async Task<T> ReadOrCreateSettingAsync<T>(string key) where T : new()
    {
        var setting = await ReadSettingAsync<T>(key);
        return setting ?? new T();
    }

    public async Task SaveSettingAsync<T>(string key, T value) where T : notnull
    {
        if (RuntimeHelper.IsMSIX)
        {
            ApplicationData.Current.LocalSettings.Values[key] = await Json.StringifyAsync(value);
        }
        else
        {
            await InitializeAsync();

            _settings[key] = await Json.StringifyAsync(value);

            await Task.Run(() => _fileService.Save(_applicationDataFolder, _localsettingsFile, _settings));
        }
    }

    public T? ReadSetting<T>(string key)
    {
        if (RuntimeHelper.IsMSIX)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
                return JsonConvert.DeserializeObject<T>((string)obj);
        }
        else
        {
            if (_settings != null && _settings.TryGetValue(key, out var obj))
                return JsonConvert.DeserializeObject<T>((string)obj);
        }

        return default;
    }
}