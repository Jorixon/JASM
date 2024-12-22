using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace GIMI_ModManager.Core.GamesService;

internal class GameSettingsManager
{
    private readonly ILogger _logger;
    internal string SettingsFile { get; }
    private readonly DirectoryInfo _customImageFolder;
    internal readonly DirectoryInfo CustomCharacterImageFolder;

    private GameServiceRoot? _settings;

    internal GameSettingsManager(ILogger logger, DirectoryInfo settingsDirectory)
    {
        _logger = logger.ForContext<GameSettingsManager>();
        SettingsFile = Path.Combine(settingsDirectory.FullName, "GameService.json");
        _customImageFolder = new DirectoryInfo(Path.Combine(settingsDirectory.FullName, "CustomImages"));
        _customImageFolder.Create();

        CustomCharacterImageFolder = new DirectoryInfo(Path.Combine(_customImageFolder.FullName, "CustomModdableObjects",
            Category.CreateForCharacter().InternalName));
        CustomCharacterImageFolder.Create();
    }


    // TODO: Handle cleanup of unused images
    //internal async Task PostGameServiceInitializeAsync(List<IModdableObject> allModdableObjects)
    //{
    //    await ReadSettingsAsync(useCache: false).ConfigureAwait(false);
    //    var customModObjects = allModdableObjects.Where(mo => mo.IsCustomModObject).ToList();
    //    var nonCustomModObjects = allModdableObjects.Where(mo => !mo.IsCustomModObject).ToList();


    //    // Remove invalid overrides
    //    var missingOverrideObject = _settings.CharacterOverrides.Where(kv => allModdableObjects.All(mo => !mo.InternalNameEquals(kv.Key))).ToList();


    //    var overrideImagesToDelete = new Dictionary<string, Uri>();


    //    foreach (var (key, value) in _settings.CharacterOverrides)
    //    {
    //        if (value.Image.IsNullOrEmpty())
    //        {
    //            value.Image = null;
    //            continue;
    //        }


    //        if (!Uri.TryCreate(value.Image, UriKind.Absolute, out var imageUri))
    //        {
    //            value.Image = null;
    //            continue;
    //        }


    //        if (!File.Exists(imageUri.LocalPath))
    //        {
    //            _logger.Warning("Image override for {Key} does not exist", key);
    //            value.Image = null;
    //            continue;
    //        }

    //        if (missingOverrideObject.Any(kv => kv.Key == key))
    //        {
    //            overrideImagesToDelete.Add(key, imageUri);
    //            value.Image = null;
    //        }
    //    }

    //    foreach (var (key, value) in missingOverrideObject)
    //    {
    //        _settings.CharacterOverrides.Remove(key);
    //    }

    //    foreach (var (_, imageUri) in overrideImagesToDelete)
    //    {
    //        File.Delete(imageUri.LocalPath);
    //    }


    //    // Remove images that are not in use

    //    //var usedImageUris = _settings.CharacterOverrides
    //    //    .Select(kv => kv.Value.Image)
    //    //    .Where(i => !i.IsNullOrEmpty())
    //    //    .Select(i => new Uri(i!))
    //    //    .Concat(_settings.CustomCharacters.Where(kv => !kv.Value.Image.IsNullOrEmpty()).Select(c => c.Value.Image))
    //    //    .ToList();


    //    throw new NotImplementedException();
    //}

    [MemberNotNull(nameof(_settings))]
    internal async Task<GameServiceRoot> ReadSettingsAsync(bool useCache = true)
    {
        if (useCache && _settings != null)
            return _settings;

        if (!File.Exists(SettingsFile))
        {
            _settings = new GameServiceRoot();
            await SaveSettingsAsync().ConfigureAwait(false);
            return _settings;
        }

        try
        {
            var rootSettings =
                JsonConvert.DeserializeObject<GameServiceRootUntyped>(await File.ReadAllTextAsync(SettingsFile)
                    .ConfigureAwait(false)) ??
                new GameServiceRootUntyped();

            var characterOverrides = await ParseUntypedJsonAsync<JsonCharacterOverride>(rootSettings.CharacterOverrides).ConfigureAwait(false);

            var characterCategory = Category.CreateForCharacter();
            var customCharacters = await ParseUntypedJsonAsync<JsonCustomCharacter>(rootSettings.CustomModObjects[characterCategory.InternalName])
                .ConfigureAwait(false);

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

        Task<Dictionary<string, T>> ParseUntypedJsonAsync<T>(Dictionary<string, object>? unTypedDictionary)
        {
            if (unTypedDictionary == null)
                return Task.FromResult(new Dictionary<string, T>());

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

    private async Task SaveSettingsAsync()
    {
        if (_settings is null)
            return;

        await File.WriteAllTextAsync(SettingsFile, JsonConvert.SerializeObject(_settings.ToJson(), Formatting.Indented)).ConfigureAwait(false);
    }

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
        => Path.Combine(_customImageFolder.FullName, $"{id.Id}{imageFile.Extension}");

    private string GetAbsImagePath(DirectoryInfo imageFolder, string imageFileName)
        => Path.Combine(imageFolder.FullName, imageFileName);

    internal async Task AddCustomCharacterAsync(ICharacter customCharacter)
    {
        await ReadSettingsAsync().ConfigureAwait(false);


        var jsonCustomCharacter = JsonCustomCharacter.FromCharacter(customCharacter);

        _settings.CustomCharacters[customCharacter.InternalName.Id] = jsonCustomCharacter;

        await SaveSettingsAsync().ConfigureAwait(false);
    }

    internal async Task ReplaceCustomCharacterAsync(ICharacter customCharacter)
    {
        await ReadSettingsAsync().ConfigureAwait(false);

        if (!_settings.CustomCharacters.TryGetValue(customCharacter.InternalName.Id, out _))
            throw new InvalidOperationException("Character to edit not found. Try restarting JASM");


        var newJsonCustomCharacter = JsonCustomCharacter.FromCharacter(customCharacter);

        _settings.CustomCharacters[customCharacter.InternalName.Id] = newJsonCustomCharacter;

        await SaveSettingsAsync().ConfigureAwait(false);
    }

    internal async Task DeleteCustomCharacterAsync(InternalName internalName)
    {
        await ReadSettingsAsync(useCache: false).ConfigureAwait(false);

        if (!_settings.CustomCharacters.Remove(internalName, out var customCharacter))
            throw new InvalidOperationException("Character to delete not found. Try restarting JASM");

        if (!customCharacter.Image.IsNullOrEmpty())
        {
            var imageFilePath = GetAbsImagePath(CustomCharacterImageFolder, customCharacter.Image);

            if (File.Exists(imageFilePath))
                File.Delete(imageFilePath);
            else
                _logger.Warning("Image file for {InternalName} not found at {ImageFilePath}", internalName, imageFilePath);
        }


        await SaveSettingsAsync().ConfigureAwait(false);
    }
}

internal class GameServiceRoot
{
    public Dictionary<string, JsonCharacterOverride> CharacterOverrides { get; init; } = new();
    public Dictionary<string, JsonCustomCharacter> CustomCharacters { get; init; } = new();

    internal GameServiceRootUntyped ToJson()
    {
        var characterOverrides = CharacterOverrides.ToDictionary(kv => kv.Key, kv => (object)kv.Value);

        var customModObjects = new Dictionary<string, Dictionary<string, object>>
        {
            { Category.CreateForCharacter().InternalName, CustomCharacters.ToDictionary(kv => kv.Key, kv => (object)kv.Value) }
        };


        return new GameServiceRootUntyped
        {
            CharacterOverrides = characterOverrides,
            CustomModObjects = customModObjects
        };
    }
}

internal class GameServiceRootUntyped
{
    public Dictionary<string, object> CharacterOverrides { get; set; } = new();
    public Dictionary<string, Dictionary<string, object>> CustomModObjects { get; set; } = new();
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

    /// <summary>
    /// Full image path
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Image { get; set; }
}

internal class JsonCustomCharacter
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? DisplayName { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]

    public string[]? Keys { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]

    public string? ReleaseDate { get; set; }

    /// <summary>
    /// Only the filename + extension
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Image { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? Rarity { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Element { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Class { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string[]? Region { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ModFilesName { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsMultiMod { get; set; }


    public static JsonCustomCharacter FromCharacter(ICharacter customCharacter)
    {
        var jsonCustomCharacter = new JsonCustomCharacter
        {
            DisplayName = customCharacter.DisplayName,
            Keys = customCharacter.Keys.ToArray(),
            ReleaseDate = customCharacter.ReleaseDate == null || customCharacter.ReleaseDate == DateTime.MinValue ||
                          customCharacter.ReleaseDate == DateTime.MaxValue
                ? null
                : customCharacter.ReleaseDate.Value.ToString("O"),
            Image = customCharacter.ImageUri != null ? Path.GetFileName(customCharacter.ImageUri.LocalPath) : null,
            Rarity = customCharacter.Rarity,
            Element = customCharacter.Element.Equals(Models.Element.NoneElement()) ? null : customCharacter.Element.InternalName.ToString(),
            Class = customCharacter.Class.Equals(Models.Class.NoneClass()) ? null : customCharacter.Class.InternalName.ToString(),
            Region = customCharacter.Regions.Count == 0 ? null : customCharacter.Regions.Select(r => r.InternalName.ToString()).ToArray(),
            ModFilesName = customCharacter.ModFilesName == "" ? null : customCharacter.ModFilesName,
            IsMultiMod = customCharacter.IsMultiMod == false ? null : customCharacter.IsMultiMod
        };

        return jsonCustomCharacter;
    }


    //[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    //public JsonCustomCharacterSkin[]? InGameSkins { get; set; }
}

//internal class JsonCustomCharacterSkin : JsonBaseNameable
//{
//    public string? ModFilesName { get; set; }

//    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
//    public string? Image { get; set; }

//    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
//    public string? ReleaseDate { get; set; }

//    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
//    public int? Rarity { get; set; }
//}