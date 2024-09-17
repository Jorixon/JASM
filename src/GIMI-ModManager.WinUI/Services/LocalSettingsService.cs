using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Contracts.Services;
using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private const string _defaultApplicationDataFolder = "ApplicationData";
    private const string _defaultLocalSettingsFile = "LocalSettings.json";
    private const string _jasm = "JASM";

    private readonly IFileService _fileService;

    private readonly string _localApplicationData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private string _applicationDataFolder;
    private readonly string _localsettingsFile;

    private IDictionary<string, object> _settings;

    private bool _isInitialized;

    public string SettingsLocation => Path.Combine(_applicationDataFolder, _localsettingsFile);

    public string ApplicationDataFolder => _applicationDataFolder;

    public LocalSettingsService(IFileService fileService)
    {
        _fileService = fileService;
#if DEBUG
        _applicationDataFolder = Path.Combine(_localApplicationData, _jasm, _defaultApplicationDataFolder) + "_Debug";

#else
        _applicationDataFolder = Path.Combine(_localApplicationData, _jasm, _defaultApplicationDataFolder);
#endif
        _localsettingsFile = _defaultLocalSettingsFile;

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

    public void SetApplicationDataFolderName(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
            throw new ArgumentException("Folder name cannot be null or empty", nameof(folderName));

        _applicationDataFolder = Path.Combine(_localApplicationData, _jasm, folderName);
#if DEBUG
        _applicationDataFolder += "_Debug";
#endif

        _isInitialized = false;
        _settings = new Dictionary<string, object>();
    }

    public async Task<T?> ReadSettingAsync<T>(string key)
    {
        await InitializeAsync();

        if (_settings != null && _settings.TryGetValue(key, out var obj))
            return await Json.ToObjectAsync<T>((string)obj).ConfigureAwait(false);

        return default;
    }

    public async Task<T> ReadOrCreateSettingAsync<T>(string key) where T : new()
    {
        var setting = await ReadSettingAsync<T>(key);
        return setting ?? new T();
    }

    public async Task SaveSettingAsync<T>(string key, T value) where T : notnull
    {
        await InitializeAsync();

        _settings[key] = await Json.StringifyAsync(value);

        await Task.Run(() => _fileService.Save(_applicationDataFolder, _localsettingsFile, _settings));
    }

    public T? ReadSetting<T>(string key)
    {
        if (_settings != null && _settings.TryGetValue(key, out var obj))
            return JsonConvert.DeserializeObject<T>((string)obj);

        return default;
    }
}

public enum SettingScope
{
    /// <summary>
    /// For settings that are for the entire application.
    /// </summary>
    App,

    /// <summary>
    /// Settings that are specific to the game.
    /// </summary>
    Game
}