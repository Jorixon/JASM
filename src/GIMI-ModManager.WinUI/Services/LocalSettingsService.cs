using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Contracts.Services;
using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private const string _defaultApplicationDataFolder = "ApplicationData";
    private const string _defaultLocalSettingsFileName = "LocalSettings.json";
    private const string _defaultAppScopedSettingsFileName = "LocalAppSettings.json";
    private const string _jasm = "JASM";

    private readonly IFileService _fileService;

    private readonly string _localApplicationData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private readonly string _jasmApplicationDataFolder;
    private string _applicationDataFolder;
    private readonly string _localSettingsFile;
    private readonly string _appScopedSettingsFile = _defaultAppScopedSettingsFileName;

    private IDictionary<string, object> _appScopedSettings;
    private IDictionary<string, object> _gameScopedSettings;

    private bool _isInitialized;

    public string GameScopedSettingsLocation => Path.Combine(_applicationDataFolder, _localSettingsFile);
    public string AppScopedSettingsLocation => Path.Combine(_localApplicationData, _jasm, _appScopedSettingsFile);

    public string ApplicationDataFolder => _applicationDataFolder;


    public LocalSettingsService(IFileService fileService)
    {
        _fileService = fileService;
        _jasmApplicationDataFolder = Path.Combine(_localApplicationData, _jasm);
#if DEBUG
        _applicationDataFolder = Path.Combine(_jasmApplicationDataFolder, _defaultApplicationDataFolder) + "_Debug";

#else
        _applicationDataFolder = Path.Combine(_jasmApplicationDataFolder, _defaultApplicationDataFolder);
#endif

#if DEBUG
        _appScopedSettingsFile = Path.GetFileNameWithoutExtension(_appScopedSettingsFile) + "_Debug" +
                                 Path.GetExtension(_appScopedSettingsFile);
#endif

        _localSettingsFile = _defaultLocalSettingsFileName;

        _appScopedSettings = new Dictionary<string, object>();
        _gameScopedSettings = new Dictionary<string, object>();
    }

    private async Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            var readAppSettingsTask = Task.Run(() =>
            {
                if (!File.Exists(AppScopedSettingsLocation))
                    File.Create(AppScopedSettingsLocation).Dispose();

                return _fileService.Read<Dictionary<string, object>>(_jasmApplicationDataFolder, _appScopedSettingsFile);
            });

            _gameScopedSettings = await Task.Run(() =>
            {
                if (!File.Exists(GameScopedSettingsLocation))
                    File.Create(GameScopedSettingsLocation).Dispose();


                return _fileService.Read<Dictionary<string, object>>(_applicationDataFolder, _localSettingsFile);
            });

            _appScopedSettings = await readAppSettingsTask;

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
        _gameScopedSettings = new Dictionary<string, object>();
    }

    public async Task<T?> ReadSettingAsync<T>(string key, SettingScope settingScope = SettingScope.Game)
    {
        await InitializeAsync();

        var settings = GetSettings(settingScope);

        if (settings.TryGetValue(key, out var obj))
            return await Json.ToObjectAsync<T>((string)obj).ConfigureAwait(false);


        return default;
    }

    public async Task<T> ReadOrCreateSettingAsync<T>(string key, SettingScope settingScope = SettingScope.Game)
        where T : new()
    {
        var setting = await ReadSettingAsync<T>(key, settingScope);
        return setting ?? new T();
    }

    public async Task SaveSettingAsync<T>(string key, T value, SettingScope settingScope = SettingScope.Game)
        where T : notnull
    {
        await InitializeAsync();

        var settings = GetSettings(settingScope);

        settings[key] = await Json.StringifyAsync(value).ConfigureAwait(false);

        var json = await Json.StringifyAsync(settings).ConfigureAwait(false);

        var folderPath = settingScope == SettingScope.App ? _jasmApplicationDataFolder : _applicationDataFolder;

        var fileName = settingScope == SettingScope.App ? _appScopedSettingsFile : _localSettingsFile;

        await Task.Run(() => _fileService.Save(folderPath, fileName, json, serializeContent: false)).ConfigureAwait(false);
    }

    public T? ReadSetting<T>(string key, SettingScope settingScope = SettingScope.Game)
    {
        var settings = GetSettings(settingScope);

        if (settings.TryGetValue(key, out var obj))
            return JsonConvert.DeserializeObject<T>((string)obj);

        return default;
    }

    private IDictionary<string, object> GetSettings(SettingScope settingScope)
    {
        return settingScope switch
        {
            SettingScope.App => _appScopedSettings,
            SettingScope.Game => _gameScopedSettings,
            _ => throw new ArgumentOutOfRangeException(nameof(settingScope), settingScope, null)
        };
    }
}

public enum SettingScope
{
    /// <summary>
    /// For settings that are for the entire application.
    /// </summary>
    App,

    /// <summary>
    /// Settings that are specific to the game profile.
    /// </summary>
    Game
}