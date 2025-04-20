using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FuzzySharp;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService.Exceptions;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.JsonModels;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.GamesService.Requests;
using GIMI_ModManager.Core.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using JsonElement = GIMI_ModManager.Core.GamesService.JsonModels.JsonElement;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace GIMI_ModManager.Core.GamesService;

public class GameService : IGameService
{
    private readonly ILogger _logger;
    private readonly ILanguageLocalizer _localizer;

    private InitializationOptions _options = null!;
    private DirectoryInfo _assetsDirectory = null!;
    private DirectoryInfo? _languageOverrideDirectory;

    private GameSettingsManager _gameSettingsManager = null!;


    public GameInfo GameInfo { get; private set; } = null!;
    public string GameName => GameInfo.GameName;
    public string GameShortName => GameInfo.GameShortName;
    public string GameIcon => GameInfo.GameIcon;
    public string GameServiceSettingsFilePath => _gameSettingsManager.SettingsFilePath;
    public Uri GameBananaUrl => GameInfo.GameBananaUrl;
    public event EventHandler? Initialized;

    private readonly EnableableList<ICharacter> _characters = new();

    private readonly EnableableList<INpc> _npcs = new();
    private readonly EnableableList<IGameObject> _gameObjects = new();
    private readonly EnableableList<IWeapon> _weapons = new();

    private readonly List<IModdableObject> _duplicateInternalNames = new();

    private readonly List<ICategory> _categories = new();

    private Elements Elements { get; set; } = null!;

    private Classes Classes { get; set; } = null!;

    private Regions Regions { get; set; } = null!;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public GameService(ILogger logger, ILanguageLocalizer localizer)
    {
        _logger = logger;
        _localizer = localizer;
        _localizer.LanguageChanged += LanguageChangedHandler;
    }

    private bool _initialized;

    public Task InitializeAsync(string assetsDirectory, string localSettingsDirectory)
    {
        var options = new InitializationOptions
        {
            AssetsDirectory = assetsDirectory,
            LocalSettingsDirectory = localSettingsDirectory
        };

        return InitializeAsync(options);
    }


    public async Task InitializeAsync(InitializationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        _options = options;

        if (_initialized)
            throw new InvalidOperationException("GameService is already initialized");

        _assetsDirectory = new DirectoryInfo(options.AssetsDirectory);
        if (!_assetsDirectory.Exists)
            throw new DirectoryNotFoundException($"Directory not found at path: {_assetsDirectory.FullName}");

        var settingsDirectory = new DirectoryInfo(options.LocalSettingsDirectory);
        settingsDirectory.Create();

        _gameSettingsManager = new GameSettingsManager(_logger, settingsDirectory);

        await InitializeGameInfoAsync().ConfigureAwait(false);

        await InitializeRegionsAsync().ConfigureAwait(false);

        await InitializeElementsAsync().ConfigureAwait(false);

        await InitializeClassesAsync().ConfigureAwait(false);

        await InitializeCharactersAsync().ConfigureAwait(false);

        await InitializeNpcsAsync().ConfigureAwait(false);

        await InitializeObjectsAsync().ConfigureAwait(false);

        await InitializeWeaponsAsync().ConfigureAwait(false);

        await MapCategoriesLanguageOverrideAsync().ConfigureAwait(false);

        await InitializeCustomCharactersAsync().ConfigureAwait(false);

        CheckIfDuplicateInternalNameExists();

        _initialized = true;
        Initialized?.Invoke(this, EventArgs.Empty);
    }


    public static async Task<GameInfo?> GetGameInfoAsync(SupportedGames game)
    {
        var gameAssetDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Games", game.ToString());

        var gameFilePath = Path.Combine(gameAssetDir, "game.json");

        if (!File.Exists(gameFilePath))
            return null;

        var jsonGameInfo =
            JsonSerializer.Deserialize<JsonGame>(await File.ReadAllTextAsync(gameFilePath).ConfigureAwait(false));

        if (jsonGameInfo is null)
            throw new InvalidOperationException($"{gameFilePath} file is empty");

        return new GameInfo(jsonGameInfo, new DirectoryInfo(gameAssetDir));
    }

    public async Task<ICollection<InternalName>> PreInitializedReadModObjectsAsync(string assetsDirectory)
    {
        if (_initialized)
        {
            _logger.Warning("GameService is already initialized");
            return GetAllModdableObjects(GetOnly.Both).Select(x => x.InternalName).ToList();
        }

        _assetsDirectory = new DirectoryInfo(assetsDirectory);
        if (!_assetsDirectory.Exists)
            throw new DirectoryNotFoundException($"Directory not found at path: {_assetsDirectory.FullName}");

        var modDirectories = new List<InternalName>();

        foreach (var predefinedCategory in Category.GetAllPredefinedCategories())
        {
            var jsonFilePath = Path.Combine(_assetsDirectory.FullName, predefinedCategory.InternalName.Id + ".json");
            if (!File.Exists(jsonFilePath))
                continue;
            try
            {
                var json = await File.ReadAllTextAsync(jsonFilePath).ConfigureAwait(false);
                var jsonBaseNameables = JsonSerializer.Deserialize<IEnumerable<JsonBaseNameable>>(json,
                    _jsonSerializerOptions);

                jsonBaseNameables ??= Array.Empty<JsonBaseNameable>();

                foreach (var jsonBaseNameable in jsonBaseNameables)
                {
                    if (jsonBaseNameable.InternalName.IsNullOrEmpty())
                        continue;

                    var internalName = new InternalName(jsonBaseNameable.InternalName);
                    if (modDirectories.Contains(internalName))
                        continue;
                    modDirectories.Add(internalName);
                }
            }
            catch (Exception)
            {
                _logger.Warning("Error while reading {JsonFile} during PreInitializedReadModObjectsAsync",
                    jsonFilePath);
            }
        }

        return modDirectories;
    }

    // TODO: Not happy with this, should consider reworking custom characters and overrides
    public async Task SetCharacterOverrideAsync(ICharacter character, OverrideCharacterRequest request)
    {
        if (character.IsCustomModObject)
            throw new InvalidOperationException($"Use {nameof(EditCustomCharacterAsync)} to modify a custom character");

        if (!request.AnyValuesSet)
            throw new ArgumentException("No values were set for editing character");

        await _gameSettingsManager.SetCharacterOverrideAsync(character.InternalName, (@override) =>
        {
            if (request.DisplayName.IsSet)
                @override.DisplayName = request.DisplayName;

            if (request.IsMultiMod.IsSet)
                @override.IsMultiMod = request.IsMultiMod;

            if (request.Keys.IsSet)
                @override.Keys = request.Keys;
        }).ConfigureAwait(false);

        if (request.DisplayName.IsSet)
            character.DisplayName = request.DisplayName.ValueToSet;

        if (request.IsMultiMod.IsSet)
            character.IsMultiMod = request.IsMultiMod.ValueToSet;

        if (request.Keys.IsSet)
            character.Keys = request.Keys.ValueToSet;


        if (request.Image.IsSet)
        {
            var image = await _gameSettingsManager.SetCharacterImageOverrideAsync(character.InternalName, request.Image).ConfigureAwait(false);
            if (image != null)
                character.ImageUri = image;
            else
            {
                if (File.Exists(character.DefaultCharacter.ImageUri?.LocalPath))
                    character.ImageUri = character.DefaultCharacter.ImageUri;
            }
        }
    }

    public async Task DisableCharacterAsync(ICharacter character)
    {
        if (character.IsCustomModObject)
            throw new InvalidOperationException($"Use {nameof(EditCustomCharacterAsync)} to modify a custom character");

        await _gameSettingsManager.SetIsDisabledOverride(character.InternalName, true).ConfigureAwait(false);
        var internalCharacter =
            _characters.FirstOrDefault(x => x.ModdableObject.InternalName == character.InternalName);

        if (internalCharacter is not null)
            internalCharacter.IsEnabled = false;
        else
            throw new InvalidOperationException($"Character with internal name {character?.InternalName} not found");
    }

    public async Task EnableCharacterAsync(ICharacter character)
    {
        if (character.IsCustomModObject)
            throw new InvalidOperationException($"Use {nameof(EditCustomCharacterAsync)} to modify a custom character");

        await _gameSettingsManager.SetIsDisabledOverride(character.InternalName, false).ConfigureAwait(false);
        var internalCharacter =
            _characters.FirstOrDefault(x => x.ModdableObject.InternalName == character.InternalName);

        if (internalCharacter is not null)
            internalCharacter.IsEnabled = true;
        else
            throw new InvalidOperationException($"Character with internal name {character?.InternalName} not found");
    }

    public async Task ResetOverrideForCharacterAsync(ICharacter character)
    {
        if (character.IsCustomModObject)
            throw new InvalidOperationException($"Use {nameof(EditCustomCharacterAsync)} to modify a custom character");

        await _gameSettingsManager.RemoveOverride(character.InternalName).ConfigureAwait(false);
        character.DisplayName = character.DefaultCharacter.DisplayName;
        character.ImageUri = character.DefaultCharacter.ImageUri;
        character.Keys = character.DefaultCharacter.Keys;
        character.IsMultiMod = character.DefaultCharacter.IsMultiMod;
    }

    public List<IModdableObject> GetModdableObjects(ICategory category, GetOnly getOnlyStatus = GetOnly.Enabled)
    {
        if (category.ModCategory == ModCategory.Character)
            return _characters.GetOfType(getOnlyStatus).Cast<IModdableObject>().ToList();

        if (category.ModCategory == ModCategory.NPC)
            return _npcs.GetOfType(getOnlyStatus).Cast<IModdableObject>().ToList();

        if (category.ModCategory == ModCategory.Object)
            return _gameObjects.GetOfType(getOnlyStatus).Cast<IModdableObject>().ToList();

        if (category.ModCategory == ModCategory.Weapons)
            return _weapons.GetOfType(getOnlyStatus).Cast<IModdableObject>().ToList();

        throw new ArgumentException($"Category {category.InternalName} is not supported");
    }


    public List<IModdableObject> GetAllModdableObjects(GetOnly getOnlyStatus = GetOnly.Enabled)
    {
        var moddableObjects = new List<IModdableObject>();

        moddableObjects.AddRange(_characters.GetOfType(getOnlyStatus));
        moddableObjects.AddRange(_npcs.GetOfType(getOnlyStatus));
        moddableObjects.AddRange(_gameObjects.GetOfType(getOnlyStatus));
        moddableObjects.AddRange(_weapons.GetOfType(getOnlyStatus));

        return moddableObjects;
    }

    public List<T> GetAllModdableObjectsAsCategory<T>(GetOnly getOnlyStatus = GetOnly.Enabled) where T : IModdableObject
    {
        if (typeof(T) == typeof(ICharacter))
            return _characters.GetOfType(getOnlyStatus).Cast<T>().ToList();

        if (typeof(T) == typeof(INpc))
            return _npcs.GetOfType(getOnlyStatus).Cast<T>().ToList();

        if (typeof(T) == typeof(IGameObject))
            return _gameObjects.GetOfType(getOnlyStatus).Cast<T>().ToList();

        if (typeof(T) == typeof(IWeapon))
            return _weapons.GetOfType(getOnlyStatus).Cast<T>().ToList();


        throw new ArgumentException($"Type {typeof(T)} is not supported");
    }

    // TODO: Not happy with this, should consider reworking custom characters and overrides
    public async Task<ICharacter> CreateCharacterAsync(CreateCharacterRequest characterRequest)
    {
        if (characterRequest.InternalName?.Id == null || characterRequest.InternalName.Id.IsNullOrEmpty())
            throw new InvalidModdableObjectException("InternalName must not be null or empty");

        if (GetAllModdableObjects(GetOnly.Both).Any(m => m.InternalNameEquals(characterRequest.InternalName)))
            throw new InvalidModdableObjectException("A moddable object with the same internal name already exists. InternalName must be unique");


        var internalCreateRequest = new JsonCustomCharacter()
        {
            DisplayName = characterRequest.DisplayName,
            Keys = characterRequest.Keys?.ToArray(),
            Class = characterRequest.Class,
            Element = characterRequest.Element,
            Image = characterRequest.Image?.LocalPath,
            IsMultiMod = characterRequest.IsMultiMod,
            Region = characterRequest.Region?.ToArray(),
            ModFilesName = characterRequest.ModFilesName,
            Rarity = characterRequest.Rarity,
            ReleaseDate = characterRequest.ReleaseDate?.ToString("O")
        };

        FileInfo? sourceImage = null;
        if (characterRequest.Image is not null)
        {
            if (!characterRequest.Image.IsFile || !File.Exists(characterRequest.Image.LocalPath))
                throw new InvalidModdableObjectException("Image must be a valid existing filesystem file");

            var imageExtension = Path.GetExtension(characterRequest.Image.LocalPath);

            if (imageExtension.IsNullOrEmpty())
                throw new InvalidModdableObjectException("Image must have an extension");

            if (Constants.SupportedImageExtensions.All(x => x != imageExtension))
                throw new InvalidModdableObjectException("Image must have a supported extension. Supported extensions are: " +
                                                         string.Join(", ", Constants.SupportedImageExtensions));

            sourceImage = new FileInfo(characterRequest.Image.LocalPath);
        }


        Character character;
        try
        {
            character = Character
                .FromJson(characterRequest.InternalName, internalCreateRequest)
                .SetRegion(Regions.AllRegions)
                .SetElement(Elements.AllElements)
                .SetClass(Classes.AllClasses)
                .CreateCharacter();
        }
        catch (Exception e)
        {
            throw new InvalidModdableObjectException($"Failed to create character: {e.Message}", e);
        }


        // If character was successfully created, now we can copy the image to the correct folder if it exists

        if (sourceImage is not null)
        {
            try
            {
                var destinationImagePath = _gameSettingsManager.CreateCustomCharacterImagePath(character.InternalName, sourceImage.Extension);
                sourceImage.CopyTo(destinationImagePath.LocalPath, true);

                if (File.Exists(destinationImagePath.LocalPath))
                    character.ImageUri = destinationImagePath;
            }
            catch (Exception e)
            {
                throw new InvalidModdableObjectException($"Failed to copy custom image: {e.Message}", e);
            }
        }

        character.DefaultCharacter = character.Clone();

        try
        {
            await _gameSettingsManager.AddCustomCharacterAsync(character).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw new InvalidModdableObjectException($"Failed to save custom character: {e.Message}", e);
        }


        _characters.Add(character);

        return character;
    }

    private readonly JsonSerializerSettings _jsonCharacterExportSettings = new()
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        Converters = new List<JsonConverter> { new StringEnumConverter() }
    };

    public async Task<(string json, ICharacter character)> CreateJsonCharacterExportAsync(CreateCharacterRequest characterRequest)
    {
        var internalCreateRequest = new JsonCustomCharacter()
        {
            DisplayName = characterRequest.DisplayName,
            Keys = characterRequest.Keys?.ToArray(),
            Class = characterRequest.Class,
            Element = characterRequest.Element,
            Image = characterRequest.Image?.LocalPath,
            IsMultiMod = characterRequest.IsMultiMod,
            Region = characterRequest.Region?.ToArray(),
            ModFilesName = characterRequest.ModFilesName,
            Rarity = characterRequest.Rarity,
            ReleaseDate = characterRequest.ReleaseDate?.ToString("O")
        };

        if (characterRequest.Image is not null)
        {
            if (!characterRequest.Image.IsFile || !File.Exists(characterRequest.Image.LocalPath))
                throw new InvalidModdableObjectException("Image must be a valid existing filesystem file");

            var imageExtension = Path.GetExtension(characterRequest.Image.LocalPath);

            if (imageExtension.IsNullOrEmpty())
                throw new InvalidModdableObjectException("Image must have an extension");

            if (Constants.SupportedImageExtensions.All(x => x != imageExtension))
                throw new InvalidModdableObjectException("Image must have a supported extension. Supported extensions are: " +
                                                         string.Join(", ", Constants.SupportedImageExtensions));
        }


        Character character;
        try
        {
            character = Character
                .FromJson(characterRequest.InternalName, internalCreateRequest)
                .SetRegion(Regions.AllRegions)
                .SetElement(Elements.AllElements)
                .SetClass(Classes.AllClasses)
                .CreateCharacter();
        }
        catch (Exception e)
        {
            throw new InvalidModdableObjectException($"Failed to create character: {e.Message}", e);
        }

        var isValidImage = characterRequest.Image is not null && characterRequest.Image.IsFile && File.Exists(characterRequest.Image.LocalPath);


        var jsonCharacter = new JsonCharacter()
        {
            InternalName = character.InternalName.Id,
            DisplayName = character.DisplayName.IsNullOrEmpty() ? null : character.DisplayName,
            Keys = character.Keys.Count == 0 ? null : character.Keys.ToArray(),
            Class = Class.NoneClass().Equals(character.Class) ? null : character.Class.InternalName.Id,
            Element = Element.NoneElement().Equals(character.Element) ? null : character.Element.InternalName.Id,
            Region = character.Regions.Count == 0 ? null : character.Regions.Select(x => x.InternalName.Id).ToArray(),
            Image = isValidImage
                ? character.InternalName + Path.GetExtension(characterRequest.Image!.LocalPath)
                : null,
            IsMultiMod = character.IsMultiMod ? true : null,
            ModFilesName = character.ModFilesName.IsNullOrEmpty() ? null : character.ModFilesName,
            Rarity = character.Rarity,
            ReleaseDate = character.ReleaseDate?.ToString("O").Split('.').ElementAtOrDefault(0),
            InGameSkins = null
        };

        if (isValidImage)
            character.ImageUri = character.ImageUri = characterRequest.Image;

        var json = JsonConvert.SerializeObject(jsonCharacter, _jsonCharacterExportSettings);

        return (json, character);
    }

    // TODO: Not happy with this, should consider reworking custom characters and overrides
    public async Task<ICharacter> EditCustomCharacterAsync(InternalName internalName, EditCustomCharacterRequest characterRequest)
    {
        var character = GetAllModdableObjectsAsCategory<ICharacter>(GetOnly.Both)
            .Where(c => c.IsCustomModObject)
            .FirstOrDefault(c => c.InternalNameEquals(internalName));

        if (character is null)
            throw new ModObjectNotFoundException($"Character with internal name {internalName} not found");


        if (character is not Character customCharacter)
            throw new InvalidModdableObjectException("Currently only characters are supported");

        if (!characterRequest.AnyValuesSet)
        {
            _logger.Warning("No values were set for editing character {InternalName}", internalName);
            return customCharacter;
        }

        var revertsIfError = new List<Action>();

        if (characterRequest.DisplayName.IsSet)
        {
            revertsIfError.Add(() => character.DisplayName = character.DefaultCharacter.DisplayName);
            character.DisplayName = characterRequest.DisplayName;
        }

        if (characterRequest.IsMultiMod.IsSet)
        {
            revertsIfError.Add(() => character.IsMultiMod = character.DefaultCharacter.IsMultiMod);
            character.IsMultiMod = characterRequest.IsMultiMod;
        }

        if (characterRequest.Keys.IsSet)
        {
            revertsIfError.Add(() => character.Keys = character.DefaultCharacter.Keys);
            character.Keys = characterRequest.Keys.ValueToSet;
        }


        try
        {
            await _gameSettingsManager.ReplaceCustomCharacterAsync(character).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to edit custom character {InternalName}", internalName);
            revertsIfError.ForEach(revert => revert());
            throw;
        }

        // No revert if error after this point
        if (characterRequest.Image.IsSet)
        {
            try
            {
                await _gameSettingsManager.SetCustomCharacterImageAsync(character, characterRequest.Image).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to update image for custom character {InternalName}", internalName);
            }
        }

        customCharacter.DefaultCharacter = customCharacter.Clone();
        return customCharacter;
    }

    public async Task<ICharacter> DeleteCustomCharacterAsync(InternalName internalName)
    {
        var character = GetAllModdableObjectsAsCategory<ICharacter>(GetOnly.Both)
            .Where(c => c.IsCustomModObject)
            .FirstOrDefault(c => c.InternalNameEquals(internalName));

        if (character is null)
            throw new ModObjectNotFoundException($"Character with internal name {internalName} not found");


        await _gameSettingsManager.DeleteCustomCharacterAsync(character.InternalName).ConfigureAwait(false);

        var result = _characters.Remove(character);

        if (!result)
            _logger.Warning("Failed to remove character {InternalName} from internal GameService list", internalName);

        return character;
    }

    public ICharacter? QueryCharacter(string keywords, IEnumerable<ICharacter>? restrictToCharacters = null,
        int minScore = 100)
    {
        var searchResult = QueryCharacters(keywords, restrictToCharacters, minScore);

        return searchResult.Any(kv => kv.Value >= minScore) ? searchResult.MaxBy(x => x.Value).Key : null;
    }

    public ICharacter? GetCharacterByIdentifier(string internalName, bool includeDisabledCharacters = false)
    {
        var characters =
            GetAllModdableObjectsAsCategory<ICharacter>(includeDisabledCharacters ? GetOnly.Both : GetOnly.Enabled);

        return characters.FirstOrDefault(x => x.InternalNameEquals(internalName));
    }

    public IModdableObject? GetModdableObjectByIdentifier(InternalName? internalName,
        GetOnly getOnlyStatus = GetOnly.Enabled)
    {
        return GetAllModdableObjects(getOnlyStatus).FirstOrDefault(x => x.InternalNameEquals(internalName));
    }


    public Dictionary<ICharacter, int> QueryCharacters(string searchQuery,
        IEnumerable<ICharacter>? restrictToCharacters = null, int minScore = 100,
        bool includeDisabledCharacters = false)
    {
        var searchResult = new Dictionary<ICharacter, int>();
        searchQuery = searchQuery.ToLower().Trim();

        var charactersToSearch = restrictToCharacters ?? (includeDisabledCharacters
            ? GetAllModdableObjectsAsCategory<ICharacter>(GetOnly.Both)
            : GetAllModdableObjectsAsCategory<ICharacter>());

        foreach (var character in charactersToSearch)
        {
            var result = GetBaseSearchResult(searchQuery, character);

            // A character can have multiple keys, so we take the best one. The keys are only used to help with searching
            if (character.Keys.Count > 0)
            {
                var bestKeyMatch = character.Keys.Max(key => Fuzz.Ratio(key, searchQuery));
                result += bestKeyMatch;
            }

            if (character.Keys.Any(key => key.Equals(searchQuery, StringComparison.CurrentCultureIgnoreCase)))
                result += 100;

            if (result < minScore) continue;

            searchResult.Add(character, result);
        }

        return searchResult;
    }


    public Dictionary<IModdableObject, int> QueryModdableObjects(string searchQuery, ICategory? category = null,
        int minScore = 100)
    {
        var searchResult = new Dictionary<IModdableObject, int>();
        searchQuery = searchQuery.ToLower().Trim();


        if (category?.ModCategory == ModCategory.Character)
            return QueryCharacters(searchQuery, GetAllModdableObjectsAsCategory<ICharacter>(), minScore)
                .ToDictionary(x => x.Key as IModdableObject, x => x.Value);


        var charactersToSearch = category is null
            ? GetAllModdableObjects()
            : GetModdableObjects(category);


        foreach (var moddableObject in charactersToSearch)
        {
            var result = GetBaseSearchResult(searchQuery, moddableObject);

            if (result < minScore) continue;

            searchResult.Add(moddableObject, result);
        }

        return searchResult;
    }

    private static int GetBaseSearchResult(string searchQuery, INameable character)
    {
        var loweredDisplayName = character.DisplayName.ToLower();

        var result = 0;

        // If the search query contains the display name, we give it a lot of points
        var sameChars = loweredDisplayName.Split().Count(searchQuery.Contains);
        result += sameChars * 60;

        var splitNames = loweredDisplayName.Split();
        var sameStartChars = 0;
        var bestResultOfNames = 0;
        // This loop will give points for each name that starts with the same chars as the search query
        foreach (var name in splitNames)
        {
            sameStartChars = 0;
            foreach (var @char in searchQuery)
            {
                if (name.ElementAtOrDefault(sameStartChars) == default(char)) continue;

                if (name[sameStartChars] != @char) continue;

                sameStartChars++;
                if (sameStartChars > bestResultOfNames)
                    bestResultOfNames = sameStartChars;
            }
        }

        result += sameStartChars * 11; // Give more points for same start chars

        result += loweredDisplayName.Split(' ', options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Max(name => Fuzz.PartialRatio(name, searchQuery)); // Do a partial ratio for each name
        return result;
    }


    public List<IGameElement> GetElements() => Elements.AllElements.ToList();

    public List<IGameClass> GetClasses() => Classes.AllClasses.ToList();

    public List<IRegion> GetRegions() => Regions.AllRegions.ToList();

    [Obsolete($"Use {nameof(GetAllModdableObjectsAsCategory)} instead")]
    public List<ICharacter> GetCharacters(bool includeDisabled = false) =>
        includeDisabled ? _characters.GetOfType(GetOnly.Both).ToList() : _characters.WhereEnabled().ToList();

    [Obsolete($"Use {nameof(GetAllModdableObjectsAsCategory)} instead")]
    public List<ICharacter> GetDisabledCharacters() => _characters.WhereDisabled().ToList();

    public List<ICategory> GetCategories() => new(_categories);


    public bool IsMultiMod(IModdableObject moddableObject) =>
        moddableObject.IsMultiMod || IsMultiMod(moddableObject.InternalName);

    private static bool IsMultiMod(string modInternalName)
    {
        var legacyPredefinedMultiMod = new List<string> { "gliders", "weapons", "others" };

        return legacyPredefinedMultiMod.Any(name => name.Equals(modInternalName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task InitializeGameInfoAsync()
    {
        const string gameFileName = "game.json";
        var gameFilePath = Path.Combine(_assetsDirectory.FullName, gameFileName);
        if (!File.Exists(gameFilePath))
            throw new FileNotFoundException($"{gameFileName} File not found at path: {gameFilePath}");

        var jsonGameInfo =
            JsonSerializer.Deserialize<JsonGame>(await File.ReadAllTextAsync(gameFilePath).ConfigureAwait(false));

        if (jsonGameInfo is null)
            throw new InvalidOperationException($"{gameFilePath} file is empty");

        GameInfo = new GameInfo(jsonGameInfo, _assetsDirectory);
    }

    private async Task InitializeRegionsAsync()
    {
        var regionFileName = "regions.json";
        var regionsFilePath = Path.Combine(_assetsDirectory.FullName, regionFileName);

        if (!File.Exists(regionsFilePath))
            throw new FileNotFoundException($"{regionFileName} File not found at path: {regionsFilePath}");

        var regions =
            JsonSerializer.Deserialize<IEnumerable<JsonRegion>>(
                await File.ReadAllTextAsync(regionsFilePath).ConfigureAwait(false),
                _jsonSerializerOptions) ?? throw new InvalidOperationException("Regions file is empty");

        Regions = Regions.InitializeRegions(regions);

        if (LanguageOverrideAvailable())
            await MapDisplayNames(regionFileName, Regions.AllRegions).ConfigureAwait(false);
    }

    private async Task InitializeCharactersAsync()
    {
        const string characterFileName = "characters.json";
        var imageFolderName = Path.Combine(_assetsDirectory.FullName, "Images", "Characters");
        var characterSkinPath = Path.Combine(_assetsDirectory.FullName, "Images", "AltCharacterSkins");

        var jsonCharacters = await SerializeAsync<JsonCharacter>(characterFileName).ConfigureAwait(false);
        var customSettings = await _gameSettingsManager.ReadSettingsAsync().ConfigureAwait(false);

        foreach (var jsonCharacter in jsonCharacters)
        {
            var character = Character
                .FromJson(jsonCharacter)
                .SetRegion(Regions.AllRegions)
                .SetElement(Elements.AllElements)
                .SetClass(Classes.AllClasses)
                .CreateCharacter(imageFolder: imageFolderName, characterSkinImageFolder: characterSkinPath);

            _characters.Add(character);

            if (!_options.CharacterSkinsAsCharacters) continue;

            foreach (var skin in character.ClearAndReturnSkins())
            {
                var newCharacter = character.FromCharacterSkin(skin);
                _characters.Add(newCharacter);
            }
        }


        _characters.Insert(0, getOthersCharacter());
        _characters.Add(getGlidersCharacter());
        _characters.Add(getWeaponsCharacter());

        if (LanguageOverrideAvailable())
            await MapDisplayNames(characterFileName, _characters.ToEnumerable()).ConfigureAwait(false);

        foreach (var enableableCharacter in _characters)
        {
            var character = enableableCharacter.ModdableObject;
            var characterOverride = customSettings.CharacterOverrides.FirstOrDefault(x =>
                x.Key.Equals(character.InternalName.Id, StringComparison.OrdinalIgnoreCase)).Value;

            if (characterOverride is null) continue;

            await MapCustomOverrides(character, characterOverride).ConfigureAwait(false);

            if (characterOverride?.IsDisabled is not null && characterOverride.IsDisabled.Value)
            {
                enableableCharacter.IsEnabled = false;
            }
        }

        _categories.Add(Category.CreateForCharacter());
    }

    private async Task InitializeNpcsAsync()
    {
        var npcFileName = "npcs.json";
        var imageFolderName = Path.Combine(_assetsDirectory.FullName, "Images", "Npcs");

        var jsonNpcs = await SerializeAsync<JsonNpc>(npcFileName).ConfigureAwait(false);

        foreach (var jsonNpc in jsonNpcs)
        {
            var npc = Npc.FromJson(jsonNpc, imageFolderName);

            _npcs.Add(npc);
        }

        if (LanguageOverrideAvailable())
            await MapDisplayNames(npcFileName, _npcs.ToEnumerable()).ConfigureAwait(false);

        if (_npcs.Any())
            _categories.Add(Category.CreateForNpc());
    }

    private async Task InitializeObjectsAsync()
    {
        var objectFileName = "objects.json";
        var imageFolderName = Path.Combine(_assetsDirectory.FullName, "Images",
            Path.GetFileNameWithoutExtension(objectFileName));

        var objects = await BaseModdableObjectMapper(objectFileName, imageFolderName, Category.CreateForObjects())
            .ConfigureAwait(false);

        if (LanguageOverrideAvailable())
            await MapDisplayNames(objectFileName, objects).ConfigureAwait(false);

        if (objects.Any())
        {
            _categories.Add(Category.CreateForObjects());
            foreach (var moddableObject in objects)
                _gameObjects.Add(new GameObject(moddableObject));
        }
        else
            _logger.Debug("No gameObjects found in {ObjectFileName}", objectFileName);
    }

    //private async Task InitializeGlidersAsync()
    private async Task InitializeWeaponsAsync()
    {
        if (!Classes.AllClasses.Any())
            throw new InvalidOperationException("Classes must be initialized before weapons");

        const string weaponFileName = "weapons.json";
        var imageFolderName = Path.Combine(_assetsDirectory.FullName, "Images", "Weapons");
        var jsonWeapons = await SerializeAsync<JsonWeapon>(weaponFileName).ConfigureAwait(false);

        foreach (var jsonWeapon in jsonWeapons)
        {
            var weapon = Weapon.FromJson(jsonWeapon, imageFolderName, jsonWeapon.Rarity, Classes.AllClasses);

            _weapons.Add(new Enableable<IWeapon>(weapon));
        }

        if (LanguageOverrideAvailable())
            await MapDisplayNames(weaponFileName, _weapons.ToEnumerable()).ConfigureAwait(false);

        if (_weapons.Any())
            _categories.Add(Category.CreateForWeapons());
    }
    //private async Task InitializeCustomAsync()


    private async Task InitializeClassesAsync()
    {
        const string classFileName = "weaponClasses.json";

        var classes = await SerializeAsync<JsonClasses>(classFileName).ConfigureAwait(false);

        Classes = new Classes("Classes");

        Classes.Initialize(classes, _assetsDirectory.FullName);

        if (LanguageOverrideAvailable())
            await MapDisplayNames(classFileName, Classes.AllClasses).ConfigureAwait(false);
    }

    private async Task InitializeElementsAsync()
    {
        const string elementsFileName = "elements.json";

        var elements = await SerializeAsync<JsonElement>(elementsFileName).ConfigureAwait(false);

        Elements = new Elements("Elements");

        Elements.Initialize(elements, _assetsDirectory.FullName);

        if (LanguageOverrideAvailable())
            await MapDisplayNames(elementsFileName, Elements.AllElements).ConfigureAwait(false);
    }

    private async Task MapCategoriesLanguageOverrideAsync()
    {
        const string categoriesFileName = "categories.json";

        if (LanguageOverrideAvailable())
            await MapDisplayNames(categoriesFileName, GetCategories()).ConfigureAwait(false);
    }

    private async Task InitializeCustomCharactersAsync()
    {
        var allCharacters = GetAllModdableObjectsAsCategory<ICharacter>(GetOnly.Both).ToList();
        var otherModdableObjects = GetAllModdableObjects(GetOnly.Both).Except(allCharacters).ToList();

        var customSettings = await _gameSettingsManager.ReadSettingsAsync().ConfigureAwait(false);

        var customCharacters = customSettings.CustomCharacters;

        foreach (var (internalName, jsonCustomCharacter) in customCharacters)
        {
            // Check against legacy predefined multi mods
            if (getOthersCharacter().InternalName.Equals(internalName) ||
                getGlidersCharacter().InternalName.Equals(internalName) ||
                getWeaponsCharacter().InternalName.Equals(internalName))
            {
                _logger.Error("Internal name {InternalName} is reserved for predefined multi mods. However, a custom character was found using it.",
                    internalName);
                continue;
            }

            var otherModConflict = otherModdableObjects.FirstOrDefault(x => x.InternalNameEquals(internalName));

            if (otherModConflict is not null)
            {
                _logger.Warning(
                    "Internal name {InternalName} is used by another moddable object type. However, a custom character was found using it. JASM will prioritize custom character",
                    internalName);
                _duplicateInternalNames.Add(otherModConflict);

                switch (otherModConflict)
                {
                    case INpc npc:
                        _npcs.Remove(npc);
                        break;
                    case IGameObject gameObject:
                        _gameObjects.Remove(gameObject);
                        break;
                    case IWeapon weapon:
                        _weapons.Remove(weapon);
                        break;
#if DEBUG
                    default:
                        throw new InvalidOperationException(
                            $"Unknown moddable object type {otherModConflict.GetType()} for internal name {internalName}");
#endif
                }
            }

            var existingCharacter = allCharacters.FirstOrDefault(x => x.InternalNameEquals(internalName));

            // For characters category, custom characters override existing characters
            if (existingCharacter is not null)
            {
                _duplicateInternalNames.Add(existingCharacter);
                _characters.Remove(existingCharacter);

                _logger.Warning(
                    "Custom character found with internal name {InternalName}. However a predefined character also exists. JASM will prioritize custom character",
                    internalName);
            }


            var character = Character
                .FromJson(internalName, jsonCustomCharacter)
                .SetRegion(Regions.AllRegions)
                .SetElement(Elements.AllElements)
                .SetClass(Classes.AllClasses)
                .CreateCharacter(imageFolder: _gameSettingsManager.CustomCharacterImageFolder.FullName);


            _characters.Add(character);
        }

        try
        {
            await _gameSettingsManager.CleanupUnusedImagesAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Debugger.Break();
            _logger.Error(e, "Failed to cleanup unused images");
        }
    }

    [MemberNotNullWhen(true, nameof(_languageOverrideDirectory))]
    private bool LanguageOverrideAvailable()
    {
        var currentLanguage = _localizer.CurrentLanguage;

        _languageOverrideDirectory =
            new DirectoryInfo(Path.Combine(_assetsDirectory.FullName, "Languages", currentLanguage.LanguageCode));

        if (currentLanguage.LanguageCode == "en-us")
            return false;


        return _languageOverrideDirectory.Exists && _languageOverrideDirectory.GetFiles().Any();
    }


    private Task<IEnumerable<T>> SerializeAsync<T>(string fileName, bool throwIfNotFound = true)
    {
        var objFilePath = Path.Combine(_assetsDirectory.FullName, fileName);

        return SerializeFileAsync<T>(filePath: objFilePath, throwIfNotFound: throwIfNotFound);
    }

    private async Task<IEnumerable<T>> SerializeFileAsync<T>(string filePath, bool throwIfNotFound = true)
    {
        if (!File.Exists(filePath))
        {
            if (throwIfNotFound)
                throw new FileNotFoundException($"File not found at path: {filePath}");
            else
                return Array.Empty<T>();
        }

        return JsonSerializer.Deserialize<IEnumerable<T>>(
            await File.ReadAllTextAsync(filePath).ConfigureAwait(false),
            _jsonSerializerOptions) ?? throw new InvalidOperationException($"{filePath} is empty");
    }

    public string OtherCharacterInternalName => "Others";

    private Character getOthersCharacter()
    {
        var character = new Character
        {
            //Id = _otherCharacterId,
            InternalName = new InternalName(OtherCharacterInternalName),
            DisplayName = OtherCharacterInternalName,
            ReleaseDate = DateTime.MinValue,
            Rarity = -1,
            Regions = new List<IRegion>(),
            Keys = new[] { "others", "unknown" },
            ImageUri = new Uri(Path.Combine(_assetsDirectory.FullName, "Images", "Characters", "Others.png")),
            Element = Elements.AllElements.First(),
            Class = Classes.AllClasses.First(),
            IsMultiMod = true
        };
        AddDefaultSkin(character);
        return character;
    }

    public string GlidersCharacterInternalName => "Gliders";

    private Character getGlidersCharacter()
    {
        var character = new Character
        {
            //Id = _glidersCharacterId,
            InternalName = new InternalName(GlidersCharacterInternalName),
            DisplayName = GlidersCharacterInternalName,
            ReleaseDate = DateTime.MinValue,
            Rarity = -1,
            Regions = new List<IRegion>(),
            Keys = new[] { "gliders", "glider", "wings" },
            ImageUri = new Uri(Path.Combine(_assetsDirectory.FullName, "Images", "Characters",
                "Gliders.webp")),
            Element = Elements.AllElements.First(),
            Class = Classes.AllClasses.First(),
            IsMultiMod = true
        };
        AddDefaultSkin(character);
        return character;
    }

    public string WeaponsCharacterInternalName => "Weapons";

    private Character getWeaponsCharacter()
    {
        var character = new Character
        {
            //Id = _weaponsCharacterId,
            InternalName = new InternalName(WeaponsCharacterInternalName),
            DisplayName = WeaponsCharacterInternalName,
            ReleaseDate = DateTime.MinValue,
            Rarity = -1,
            Regions = new List<IRegion>(),
            Keys = new[] { "weapon", "claymore", "sword", "polearm", "catalyst", "bow" },
            ImageUri = new Uri(Path.Combine(_assetsDirectory.FullName, "Images", "Characters",
                "Weapons.webp")),
            Element = Elements.AllElements.First(),
            Class = Classes.AllClasses.First(),
            IsMultiMod = true
        };

        AddDefaultSkin(character);
        return character;
    }

    private void AddDefaultSkin(ICharacter character)
    {
        character.Skins.Add(new CharacterSkin(character)
        {
            InternalName = new InternalName("Default_" + character.InternalName),
            ModFilesName = "",
            DisplayName = "Default",
            Rarity = character.Rarity,
            ReleaseDate = character.ReleaseDate,
            Character = character,
            IsDefault = true
        });
    }


    private async Task MapDisplayNames(string fileName, IEnumerable<INameable> nameables)
    {
        var filePath = Path.Combine(_languageOverrideDirectory!.FullName, fileName);

        if (!File.Exists(filePath))
        {
            _logger.Debug("File {FileName} not found at path: {FilePath}, no translation available", fileName,
                filePath);
            return;
        }

        var json = JsonSerializer.Deserialize<ICollection<JsonOverride>>(
            await File.ReadAllTextAsync(filePath).ConfigureAwait(false),
            _jsonSerializerOptions);

        if (json is null)
            return;

        foreach (var nameable in nameables)
        {
            var jsonOverride = json.FirstOrDefault(x => nameable.InternalNameEquals(x.InternalName));
            if (jsonOverride is null)
            {
                _logger.Debug("Nameable {NameableName} not found in {FilePath}", nameable.InternalName, filePath);
                continue;
            }

            if (!jsonOverride.DisplayName.IsNullOrEmpty())
            {
                nameable.DisplayName = jsonOverride.DisplayName;
            }


            if (nameable is IImageSupport imageSupportedValue && !jsonOverride.Image.IsNullOrEmpty())
                _logger.Warning("Image override is not implemented");

            if (nameable is ICategory category && !jsonOverride.DisplayNamePlural.IsNullOrEmpty())
            {
                category.DisplayNamePlural = jsonOverride.DisplayNamePlural;
            }

            if (nameable is not ICharacter character) continue;

            character.Skins.ForEach(skin =>
            {
                var skinOverride =
                    jsonOverride?.InGameSkins?.FirstOrDefault(x =>
                        x.InternalName is not null &&
                        skin.InternalNameEquals(x.InternalName));

                if (skinOverride is null)
                {
                    _logger.Debug("Skin {SkinName} not found in {FilePath}", skin.InternalName, filePath);
                    return;
                }

                if (skinOverride.DisplayName.IsNullOrEmpty()) return;

                skin.DisplayName = skinOverride.DisplayName;

                if (skinOverride.Image.IsNullOrEmpty()) return;
                _logger.Warning("Image override is not implemented for character skins");
            });

            if (jsonOverride.RemoveExistingKeys is not null && jsonOverride.Keys is not null &&
                jsonOverride.Keys.Count != 0)
            {
                if (jsonOverride.RemoveExistingKeys.Value)
                {
                    character.Keys.Clear();
                }

                foreach (var jsonOverrideKey in jsonOverride.Keys)
                {
                    if (character.Keys.Contains(jsonOverrideKey)) continue;
                    character.Keys.Add(jsonOverrideKey);
                }
            }
        }
    }

    private async Task MapCustomOverrides(ICharacter character, JsonCharacterOverride @override)
    {
        if (!@override.DisplayName.IsNullOrEmpty())
            character.DisplayName = @override.DisplayName;

        if (@override.IsMultiMod is not null)
            character.IsMultiMod = @override.IsMultiMod.Value;

        if (@override.Keys is not null)
            character.Keys = @override.Keys.ToList();

        if (!@override.Image.IsNullOrEmpty())
        {
            // Used to save a full image path before, now only the filename is saved, need to check both cases

            var uri = Uri.TryCreate(@override.Image, UriKind.Absolute, out var urii) ? urii : null;

            if (uri is not null)
            {
                character.ImageUri = uri;
            }
            else
            {
                var imagePath = await _gameSettingsManager.GetCharacterImageOverrideAsync(character.InternalName).ConfigureAwait(false);
                character.ImageUri = imagePath;
            }
        }
    }

    private async void LanguageChangedHandler(object? sender, EventArgs args)
    {
        if (!_initialized)
            return;

        _logger.Debug("Language changed to {Language}", _localizer.CurrentLanguage.LanguageCode);

        _languageOverrideDirectory =
            new DirectoryInfo(Path.Combine(_assetsDirectory.FullName, "Languages",
                _localizer.CurrentLanguage.LanguageCode));

        if (_localizer.CurrentLanguage.LanguageCode == "en-us")
        {
            _languageOverrideDirectory = new DirectoryInfo(Path.Combine(_assetsDirectory.FullName));
        }


        await MapDisplayNames("characters.json", _characters.ToEnumerable()).ConfigureAwait(false);
        await MapDisplayNames("npcs.json", _npcs.ToEnumerable()).ConfigureAwait(false);
        await MapDisplayNames("elements.json", Elements.AllElements).ConfigureAwait(false);
        await MapDisplayNames("weaponClasses.json", Classes.AllClasses).ConfigureAwait(false);
        await MapDisplayNames("regions.json", Regions.AllRegions).ConfigureAwait(false);
    }

    private async Task<IModdableObject[]> BaseModdableObjectMapper(string jsonFileName, string imageFolder,
        ICategory category)
    {
        var jsonBaseModdableObjects = await SerializeAsync<JsonBaseModdableObject>(jsonFileName).ConfigureAwait(false);

        var list = new List<IModdableObject>();

        foreach (var jsonBaseModdableObject in jsonBaseModdableObjects)
        {
            var moddableObject = BaseModdableObject
                .FromJson(jsonBaseModdableObject, category, imageFolder);

            list.Add(moddableObject);
        }

        return list.ToArray();
    }

    private void CheckIfDuplicateInternalNameExists()
    {
        var allNameables = GetAllModdableObjects(GetOnly.Both);

        var duplicates = allNameables
            .GroupBy(x => x.InternalName)
            .Where(g => g.Count() > 1)
            .Select(y => y.Key)
            .ToList();

        if (duplicates.Any())
            throw new InvalidOperationException(
                $"Duplicate internal names found: {string.Join(", ", duplicates)}");
    }
}

public class Regions
{
    private readonly List<Region> _regions;
    public IReadOnlyCollection<IRegion> AllRegions => _regions;

    private Regions(List<Region> regions)
    {
        _regions = regions;
    }

    internal static Regions InitializeRegions(IEnumerable<JsonRegion> regions)
    {
        var regionsList = new List<Region>();
        foreach (var region in regions)
        {
            if (string.IsNullOrWhiteSpace(region.InternalName) ||
                string.IsNullOrWhiteSpace(region.DisplayName))
                throw new InvalidOperationException("Region has invalid data");

            regionsList.Add(new Region(region.InternalName, region.DisplayName));
        }

        return new Regions(regionsList);
    }

    internal void InitializeLanguageOverrides(IEnumerable<JsonRegion> regions)
    {
        foreach (var region in regions)
        {
            var regionToOverride = _regions.FirstOrDefault(x => x.InternalName == region.InternalName);
            if (regionToOverride == null)
            {
                Log.Debug("Region {RegionName} not found in regions list", region.InternalName);
                continue;
            }

            if (string.IsNullOrWhiteSpace(region.DisplayName))
            {
                Log.Warning("Region {RegionName} has invalid display name", region.InternalName);
                continue;
            }

            regionToOverride.DisplayName = region.DisplayName;
        }
    }
}

internal class Classes : BaseMapper<Class>
{
    public IReadOnlyList<IGameClass> AllClasses => Values;

    public Classes(string name) : base(name)
    {
        Values.Add(new Class()
        {
            DisplayName = "None",
            InternalName = new InternalName("None")
        });
    }
}

internal class Elements : BaseMapper<Element>
{
    public IReadOnlyList<IGameElement> AllElements => Values;

    public Elements(string name) : base(name)
    {
        Values.Add(new Element()
        {
            DisplayName = "None",
            InternalName = new InternalName("None")
        });
    }
}

internal abstract class BaseMapper<T> where T : class, INameable, new()
{
    public string Name { get; }
    protected readonly List<T> Values;

    protected BaseMapper(string name)
    {
        Values = new List<T>();
        Name = name;
    }

    internal void Initialize(IEnumerable<JsonBaseNameable> newValues, string assetsDirectory)
    {
        foreach (var value in newValues)
        {
            if (string.IsNullOrWhiteSpace(value.InternalName) ||
                string.IsNullOrWhiteSpace(value.DisplayName))
                throw new InvalidOperationException($"{Name} has invalid data");

            var newValue = new T()
            {
                DisplayName = value.DisplayName,
                InternalName = new InternalName(value.InternalName)
            };

            // check if T is of type IImageSupport
            if (newValue is IImageSupport imageSupport && value is JsonElement element &&
                !element.Image.IsNullOrEmpty())
            {
                var imageFolder = Path.Combine(assetsDirectory, "Images", Name);

                var imageUri = Uri.TryCreate(Path.Combine(imageFolder, element.Image), UriKind.Absolute,
                    out var uri)
                    ? uri
                    : null;

                if (imageUri is null)
                    Log.Warning("Image {Image} for {Name} {ElementName} is invalid", element.Image, Name,
                        newValue.DisplayName);

                if (!File.Exists(imageUri?.LocalPath ?? string.Empty))
                {
                    Log.Debug("Image {Image} for {Name} {ElementName} does not exist", element.Image, Name,
                        newValue.DisplayName);
                    return;
                }

                imageSupport.ImageUri = imageUri;
            }

            Values.Add(newValue);
        }
    }

    internal void InitializeLanguageOverrides(IEnumerable<JsonBaseNameable> overrideValues)
    {
        foreach (var value in overrideValues)
        {
            var regionToOverride = Values.FirstOrDefault(x => x.InternalName == value.InternalName);
            if (regionToOverride == null)
            {
                Log.Debug("Region {Name} not found in regions list", Name);
                continue;
            }

            if (string.IsNullOrWhiteSpace(value.DisplayName))
            {
                Log.Warning("Region {Name} has invalid display name", Name);
                continue;
            }

            regionToOverride.DisplayName = value.DisplayName;
        }
    }
}

internal sealed class Enableable<T> where T : IModdableObject
{
    public Enableable(T moddableObject, bool isEnabled = true)
    {
        ModdableObject = moddableObject;
        IsEnabled = isEnabled;
    }

    public bool IsEnabled { get; internal set; }
    public T ModdableObject { get; init; }

    public static implicit operator T(Enableable<T> enableable) => enableable.ModdableObject;

    public static implicit operator Enableable<T>(T moddableObject) => new(moddableObject);

    public override string ToString() => $"Enabled: {IsEnabled} | {ModdableObject.InternalName}";
}

internal sealed class EnableableList<T> : List<Enableable<T>> where T : IModdableObject
{
    public EnableableList(IEnumerable<Enableable<T>> enableables) : base(enableables)
    {
    }

    public EnableableList()
    {
    }

    public static implicit operator List<T>(EnableableList<T> enableableList) =>
        enableableList.Select(x => x.ModdableObject).ToList();

    public static implicit operator EnableableList<T>(List<T> moddableObjects) =>
        new(moddableObjects.Select(x => new Enableable<T>(x)));

    public IEnumerable<T> ToEnumerable() => this.Select(x => x.ModdableObject);

    public IEnumerable<T> WhereEnabled() => this.Where(x => x.IsEnabled).Select(x => x.ModdableObject);

    public IEnumerable<T> WhereDisabled() => this.Where(x => !x.IsEnabled).Select(x => x.ModdableObject);

    public IEnumerable<T> GetOfType(GetOnly type) => type switch
    {
        GetOnly.Enabled => WhereEnabled(),
        GetOnly.Disabled => WhereDisabled(),
        _ => ToEnumerable()
    };


    /// <inheritdoc cref="List{T}.Remove"/>
    public bool Remove(T moddableObject)
    {
        var enabledWrapper = this.FirstOrDefault(e => moddableObject.Equals(e.ModdableObject));

        if (enabledWrapper is null)
            return false;

        return base.Remove(enabledWrapper);
    }
}