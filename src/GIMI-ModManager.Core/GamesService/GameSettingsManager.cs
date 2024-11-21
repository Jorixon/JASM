using System.Diagnostics;
using GIMI_ModManager.Core.GamesService.JsonModels;
using GIMI_ModManager.Core.GamesService.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace GIMI_ModManager.Core.GamesService;

internal class GameSettingsManager
{
    private readonly ILogger _logger;
    private readonly string _settingsFile;
    private readonly DirectoryInfo _customImageFolder;
    internal readonly DirectoryInfo CustomCharacterImageFolder;
    internal readonly DirectoryInfo CustomCharacterSkinImageFolder;

    private GameServiceRoot? _settings;

    internal GameSettingsManager(ILogger logger, DirectoryInfo settingsDirectory)
    {
        _logger = logger.ForContext<GameSettingsManager>();
        _settingsFile = Path.Combine(settingsDirectory.FullName, "GameService.json");
        _customImageFolder = new DirectoryInfo(Path.Combine(settingsDirectory.FullName, "CustomImages"));
        _customImageFolder.Create();

        CustomCharacterImageFolder = new DirectoryInfo(Path.Combine(_customImageFolder.FullName, "Characters"));
        CustomCharacterImageFolder.Create();

        CustomCharacterSkinImageFolder = new DirectoryInfo(Path.Combine(_customImageFolder.FullName, "CharacterSkins"));
        CustomCharacterSkinImageFolder.Create();
    }

    internal async Task<GameServiceRoot> ReadSettingsAsync(bool useCache = true)
    {
        if (useCache && _settings != null)
            return _settings;

        if (!File.Exists(_settingsFile))
        {
            _settings = new GameServiceRoot();
            await SaveSettingsAsync().ConfigureAwait(false);
            return _settings;
        }

        try
        {
            var rootSettings =
                JsonConvert.DeserializeObject<GameServiceRootUntyped>(await File.ReadAllTextAsync(_settingsFile)
                    .ConfigureAwait(false)) ??
                new GameServiceRootUntyped();

            var characterOverrides = await ParseUntypedJsonAsync<JsonCharacterOverride>(rootSettings.CharacterOverrides).ConfigureAwait(false);

            var customCharacters = await ParseUntypedJsonAsync<JsonCustomCharacter>(rootSettings.CustomCharacters).ConfigureAwait(false);

            _settings = new GameServiceRoot
            {
                CharacterOverrides = characterOverrides,
                CustomCharacters = customCharacters
            };
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to read settings file");
            _settings = new GameServiceRoot();
        }


        return _settings;

        Task<Dictionary<string, T>> ParseUntypedJsonAsync<T>(Dictionary<string, object> unTypedDictionary)
        {
            var characterOverrides = new Dictionary<string, T>();
            foreach (var (key, value) in unTypedDictionary)
            {
                try
                {
                    if (value is not JObject json)
                    {
                        Debugger.Break();
                        _logger.Error("Failed to parse character override for {Key}", key);
                        continue;
                    }

                    var typedJson = json.ToObject<T>();

                    if (typedJson == null)
                        continue;
                    characterOverrides.Add(key, typedJson);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Failed to parse character override for {Key}", key);
                }
            }

            return Task.FromResult(characterOverrides);
        }
    }

    private Task SaveSettingsAsync() =>
        File.WriteAllTextAsync(_settingsFile, JsonConvert.SerializeObject(_settings, Formatting.Indented));

    internal async Task SetDisplayNameOverride(InternalName id, string displayName)
    {
        var settings = await ReadSettingsAsync().ConfigureAwait(false);
        if (settings.CharacterOverrides.TryGetValue(id.Id, out var @override))
            @override.DisplayName = displayName;

        else
            settings.CharacterOverrides.Add(id.Id, new JsonCharacterOverride() { DisplayName = displayName });


        await SaveSettingsAsync().ConfigureAwait(false);
    }

    internal async Task SetImageOverride(InternalName id, Uri image)
    {
        var settings = await ReadSettingsAsync().ConfigureAwait(false);

        var imageFile = new FileInfo(image.LocalPath);
        var imagePath = imageFile.CopyTo(GetAbsImagePath(id, imageFile)).FullName;

        if (settings.CharacterOverrides.TryGetValue(id.Id, out var @override))
            @override.Image = imagePath;

        else
            settings.CharacterOverrides.Add(id.Id, new JsonCharacterOverride() { Image = imagePath });

        await SaveSettingsAsync().ConfigureAwait(false);
    }

    internal async Task SetIsDisabledOverride(InternalName id, bool isDisabled)
    {
        var settings = await ReadSettingsAsync().ConfigureAwait(false);
        if (settings.CharacterOverrides.TryGetValue(id.Id, out var @override))
            @override.IsDisabled = isDisabled;

        else
            settings.CharacterOverrides.Add(id.Id, new JsonCharacterOverride() { IsDisabled = isDisabled });

        await SaveSettingsAsync().ConfigureAwait(false);
    }

    internal async Task RemoveOverride(InternalName id)
    {
        var settings = await ReadSettingsAsync().ConfigureAwait(false);

        var characterOverride = settings.CharacterOverrides.FirstOrDefault(kv => id.Equals(kv.Key)).Value;

        if (characterOverride?.Image != null)
        {
            if (File.Exists(characterOverride.Image))
                File.Delete(characterOverride.Image);
        }

        settings.CharacterOverrides.Remove(id.Id);

        settings.CharacterOverrides.Add(id.Id,
            new JsonCharacterOverride() { IsDisabled = characterOverride?.IsDisabled });

        await SaveSettingsAsync().ConfigureAwait(false);
    }


    private string GetAbsImagePath(InternalName id, FileSystemInfo imageFile)
    {
        return Path.Combine(_customImageFolder.FullName, $"{id.Id}{imageFile.Extension}");
    }
}

internal class GameServiceRoot
{
    public Dictionary<string, JsonCharacterOverride> CharacterOverrides { get; set; } = new();
    public Dictionary<string, JsonCustomCharacter> CustomCharacters { get; set; } = new();
}

internal class GameServiceRootUntyped
{
    public Dictionary<string, object> CharacterOverrides { get; set; } = new();
    public Dictionary<string, object> CustomCharacters { get; set; } = new();
}

internal class JsonCharacterOverride
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? DisplayName { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsDisabled { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ICollection<string>? Keys { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ICollection<string>? DisabledKeys { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Image { get; set; }
}

internal class JsonCustomCharacter
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? DisplayName { get; set; }
    public string[]? Keys { get; set; }
    public string? ReleaseDate { get; set; }
    public string? Image { get; set; }
    public int? Rarity { get; set; }
    public string? Element { get; set; }

    public string? Class { get; set; }

    public string[]? Region { get; set; }

    public string? ModFilesName { get; set; }
    public bool? IsMultiMod { get; set; }


    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public JsonCustomCharacterSkin[]? InGameSkins { get; set; }
}

internal class JsonCustomCharacterSkin : JsonBaseNameable
{
    public string? ModFilesName { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Image { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ReleaseDate { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? Rarity { get; set; }
}