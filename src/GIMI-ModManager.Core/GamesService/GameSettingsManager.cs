using GIMI_ModManager.Core.GamesService.Models;
using Newtonsoft.Json;

namespace GIMI_ModManager.Core.GamesService;

internal class GameSettingsManager
{
    private readonly string _settingsFile;
    private readonly DirectoryInfo _customImageFolder;

    private GameServiceRoot? _settings;

    internal GameSettingsManager(DirectoryInfo settingsDirectory)
    {
        _settingsFile = Path.Combine(settingsDirectory.FullName, "GameService.json");
        _customImageFolder = new DirectoryInfo(Path.Combine(settingsDirectory.FullName, "CustomImages"));
        _customImageFolder.Create();
    }

    internal async Task<GameServiceRoot> ReadSettingsAsync()
    {
        if (!File.Exists(_settingsFile))
        {
            _settings = new GameServiceRoot();
            await SaveSettingsAsync().ConfigureAwait(false);
            return _settings;
        }


        var rootSettings =
            JsonConvert.DeserializeObject<GameServiceRoot>(await File.ReadAllTextAsync(_settingsFile)
                .ConfigureAwait(false)) ??
            new GameServiceRoot();

        _settings = rootSettings;
        return _settings;
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