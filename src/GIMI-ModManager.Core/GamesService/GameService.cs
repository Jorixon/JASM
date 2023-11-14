using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FuzzySharp;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.JsonModels;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.Helpers;
using Serilog;
using JsonElement = GIMI_ModManager.Core.GamesService.JsonModels.JsonElement;

namespace GIMI_ModManager.Core.GamesService;

public class GameService : IGameService
{
    private readonly ILogger _logger;
    private readonly ILanguageLocalizer _localizer;

    private DirectoryInfo _assetsDirectory = null!;
    private DirectoryInfo? _languageOverrideDirectory;

    private GameSettingsManager _gameSettingsManager = null!;


    public GameInfo GameInfo { get; private set; }
    public string GameName => GameInfo.GameName;
    public string GameShortName => GameInfo.GameShortName;
    public string GameIcon => GameInfo.GameIcon;
    public Uri GameBananaUrl => GameInfo.GameBananaUrl;

    private readonly List<ICharacter> _characters = new();

    private readonly List<ICharacter> _disabledCharacters = new();

    internal Elements Elements { get; private set; } = null!;

    internal Classes Classes { get; private set; } = null!;

    internal Regions Regions { get; private set; } = null!;

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

    public async Task InitializeAsync(string assetsDirectory, string localSettingsDirectory,
        ICollection<string>? disabledCharacters = null)
    {
        if (_initialized)
            throw new InvalidOperationException("GameService is already initialized");

        _assetsDirectory = new DirectoryInfo(assetsDirectory);
        if (!_assetsDirectory.Exists)
            throw new DirectoryNotFoundException($"Directory not found at path: {_assetsDirectory.FullName}");

        var settingsDirectory = new DirectoryInfo(localSettingsDirectory);
        settingsDirectory.Create();

        _gameSettingsManager = new GameSettingsManager(settingsDirectory);

        await InitializeGameInfoAsync();

        await InitializeRegionsAsync();

        await InitializeElementsAsync();

        await InitializeClassesAsync();

        await InitializeCharactersAsync();

        _initialized = true;
    }


    public async Task SetCharacterDisplayNameAsync(ICharacter character, string newDisplayName)
    {
        ArgumentException.ThrowIfNullOrEmpty(newDisplayName, nameof(newDisplayName));

        await _gameSettingsManager.SetDisplayNameOverride(character.InternalName, newDisplayName);
        character.DisplayName = newDisplayName;
    }

    public async Task SetCharacterImageAsync(ICharacter character, Uri newImageUri)
    {
        ArgumentNullException.ThrowIfNull(newImageUri, nameof(newImageUri));

        if (!File.Exists(newImageUri.LocalPath))
            throw new ArgumentException($"Image file does not exist at {newImageUri.LocalPath}", nameof(newImageUri));

        await _gameSettingsManager.SetImageOverride(character.InternalName, newImageUri);
        character.ImageUri = newImageUri;
    }

    public async Task DisableCharacterAsync(ICharacter character)
    {
        await _gameSettingsManager.SetIsDisabledOverride(character.InternalName, true);
        _characters.Remove(character);
        _disabledCharacters.Add(character);
    }

    public async Task EnableCharacterAsync(ICharacter character)
    {
        await _gameSettingsManager.SetIsDisabledOverride(character.InternalName, false);
        _disabledCharacters.Remove(character);
        _characters.Add(character);
    }

    public async Task ResetOverrideForCharacterAsync(ICharacter character)
    {
        await _gameSettingsManager.RemoveOverride(character.InternalName);
        character.DisplayName = character.DefaultCharacter.DisplayName;
        character.ImageUri = character.DefaultCharacter.ImageUri;
        character.Keys = character.DefaultCharacter.Keys;
    }

    public Task<ICharacter> CreateCharacterAsync(string internalName, string displayName, int rarity,
        Uri? imageUri = null,
        IGameClass? gameClass = null, IGameElement? gameElement = null, ICollection<IRegion>? regions = null,
        ICollection<ICharacterSkin>? additionalSkins = null, DateTime? releaseDate = null) =>
        throw new NotImplementedException();

    public ICharacter? QueryCharacter(string keywords, IEnumerable<ICharacter>? restrictToCharacters = null,
        int minScore = 100)
    {
        var searchResult = QueryCharacters(keywords, restrictToCharacters, minScore);

        return searchResult.Any(kv => kv.Value >= minScore) ? searchResult.MaxBy(x => x.Value).Key : null;
    }

    public ICharacter? GetCharacterByIdentifier(string internalName, bool includeDisabledCharacters = false)
    {
        var characters =
            GetCharacters().AsEnumerable();

        if (includeDisabledCharacters)
            characters = characters.Concat(GetDisabledCharacters());

        return characters.FirstOrDefault(x => x.InternalNameEquals(internalName));
    }

    public IModdableObject? GetModdableObjectByIdentifier(InternalName internalName)
    {
        // Might want to use reflection to get all properties that are of type IModdableObject
        // For now we just check _characters
        return GetCharacterByIdentifier(internalName);
    }


    public Dictionary<ICharacter, int> QueryCharacters(string searchQuery,
        IEnumerable<ICharacter>? restrictToCharacters = null, int minScore = 100,
        bool includeDisabledCharacters = false)
    {
        var searchResult = new Dictionary<ICharacter, int>();
        searchQuery = searchQuery.ToLower().Trim();

        var charactersToSearch = restrictToCharacters ?? (includeDisabledCharacters
            ? GetCharacters().Concat(GetDisabledCharacters())
            : GetCharacters()).AsEnumerable();

        foreach (var character in charactersToSearch)
        {
            var loweredDisplayName = character.DisplayName.ToLower();

            var result = 0;

            // If the search query contains the display name, we give it a lot of points
            var sameChars = loweredDisplayName.Split().Count(searchQuery.Contains);
            result += sameChars * 50;


            // A character can have multiple keys, so we take the best one. The keys are only used to help with searching
            var bestKeyMatch = character.Keys.Max(key => Fuzz.Ratio(key, searchQuery));
            result += bestKeyMatch;

            if (character.Keys.Any(key => key.Equals(searchQuery, StringComparison.CurrentCultureIgnoreCase)))
                result += 100;


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

            result += sameStartChars * 5; // Give more points for same start chars

            result += loweredDisplayName.Split()
                .Max(name => Fuzz.PartialRatio(name, searchQuery)); // Do a partial ratio for each name

            if (result < minScore) continue;

            searchResult.Add(character, result);
        }

        return searchResult;
    }

    public List<IGameElement> GetElements() => Elements.AllElements.ToList();

    public List<IGameClass> GetClasses() => Classes.AllClasses.ToList();

    public List<IRegion> GetRegions() => Regions.AllRegions.ToList();

    public List<ICharacter> GetCharacters() => _characters.ToList();

    public List<ICharacter> GetDisabledCharacters() => _disabledCharacters.ToList();


    public bool IsMultiMod(IModdableObject moddableObject) =>
        IsMultiMod(moddableObject.InternalName) || moddableObject.IsMultiMod;

    public bool IsMultiMod(string modInternalName)
    {
        var multiMod = new List<string> { "Gliders", "Weapons", "Others" };

        return multiMod.Any(name => name.Equals(modInternalName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task InitializeGameInfoAsync()
    {
        const string gameFileName = "game.json";
        var gameFilePath = Path.Combine(_assetsDirectory.FullName, gameFileName);
        if (!File.Exists(gameFilePath))
            throw new FileNotFoundException($"{gameFileName} File not found at path: {gameFilePath}");

        var jsonGameInfo = JsonSerializer.Deserialize<JsonGame>(await File.ReadAllTextAsync(gameFilePath));

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
            JsonSerializer.Deserialize<IEnumerable<JsonRegion>>(await File.ReadAllTextAsync(regionsFilePath),
                _jsonSerializerOptions) ?? throw new InvalidOperationException("Regions file is empty");

        Regions = Regions.InitializeRegions(regions);

        if (LanguageOverrideAvailable())
            await MapDisplayNames(regionFileName, Regions.AllRegions);
    }

    private async Task InitializeCharactersAsync()
    {
        const string characterFileName = "characters.json";
        var imageFolderName = Path.Combine(_assetsDirectory.FullName, "Images", "Characters");
        var characterSkinPath = Path.Combine(_assetsDirectory.FullName, "Images", "AltCharacterSkins");

        var jsonCharacters = await SerializeAsync<JsonCharacter>(characterFileName);
        var overrideSettings = await _gameSettingsManager.ReadSettingsAsync();

        foreach (var jsonCharacter in jsonCharacters)
        {
            var character = Character
                .FromJson(jsonCharacter)
                .SetRegion(Regions.AllRegions)
                .SetElement(Elements.AllElements)
                .SetClass(Classes.AllClasses)
                .CreateCharacter(imageFolder: imageFolderName, characterSkinImageFolder: characterSkinPath);

            _characters.Add(character);
        }

        _characters.Insert(0, getOthersCharacter());
        _characters.Add(getGlidersCharacter());
        _characters.Add(getWeaponsCharacter());

        if (LanguageOverrideAvailable())
            await MapDisplayNames(characterFileName, _characters);

        var disabledCharacters = new List<ICharacter>();
        foreach (var character in _characters)
        {
            var characterOverride = overrideSettings.CharacterOverrides.FirstOrDefault(x =>
                x.Key.Equals(character.InternalName.Id, StringComparison.OrdinalIgnoreCase)).Value;

            if (characterOverride is null) continue;

            MapCustomOverrides(character, characterOverride);

            if (characterOverride?.IsDisabled is not null && characterOverride.IsDisabled.Value)
            {
                disabledCharacters.Add(character);
            }
        }

        foreach (var disabledCharacter in disabledCharacters)
        {
            _characters.Remove(disabledCharacter);
            _disabledCharacters.Add(disabledCharacter);
        }
    }

    private async Task InitializeClassesAsync()
    {
        const string classFileName = "weaponClasses.json";

        var classes = await SerializeAsync<JsonClasses>(classFileName);

        Classes = new Classes("Classes");

        Classes.Initialize(classes, _assetsDirectory.FullName);

        if (LanguageOverrideAvailable())
            await MapDisplayNames(classFileName, Classes.AllClasses);
    }

    private async Task InitializeElementsAsync()
    {
        const string elementsFileName = "elements.json";

        var elements = await SerializeAsync<JsonElement>(elementsFileName);

        Elements = new Elements("Elements");

        Elements.Initialize(elements, _assetsDirectory.FullName);

        if (LanguageOverrideAvailable())
            await MapDisplayNames(elementsFileName, Elements.AllElements);
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


    private async Task<IEnumerable<T>> SerializeAsync<T>(string fileName)
    {
        var objFileName = fileName;
        var objFilePath = Path.Combine(_assetsDirectory.FullName, objFileName);

        if (!File.Exists(objFilePath))
            throw new FileNotFoundException($"{objFileName} File not found at path: {objFilePath}");

        return JsonSerializer.Deserialize<IEnumerable<T>>(await File.ReadAllTextAsync(objFilePath),
            _jsonSerializerOptions) ?? throw new InvalidOperationException($"{objFileName} file is empty");
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
            ImageUri = new Uri(Path.Combine(_assetsDirectory.FullName, "Images", "Characters", "Character_Others.png")),
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
                "Character_Gliders_Thumb.webp")),
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
                "Character_Weapons_Thumb.webp")),
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

        var json = JsonSerializer.Deserialize<ICollection<JsonOverride>>(await File.ReadAllTextAsync(filePath),
            _jsonSerializerOptions);

        if (json is null)
            return;

        foreach (var nameable in nameables)
        {
            var jsonOverride = json.FirstOrDefault(x => x.InternalName == nameable.InternalName);
            if (jsonOverride is null)
            {
                _logger.Debug("Nameable {NameableName} not found in {FilePath}", nameable.InternalName, filePath);
                continue;
            }

            if (jsonOverride.DisplayName.IsNullOrEmpty()) continue;

            nameable.DisplayName = jsonOverride.DisplayName;

            if (nameable is IImageSupport imageSupportedValue && !jsonOverride.Image.IsNullOrEmpty())
                _logger.Debug("Image override is not implemented");

            if (nameable is not ICharacter character) continue;

            character.Skins.ForEach(skin =>
            {
                var skinOverride =
                    jsonOverride?.InGameSkins?.FirstOrDefault(x =>
                        x.InternalName is not null &&
                        x.InternalName.Equals(skin.InternalName, StringComparison.OrdinalIgnoreCase));

                if (skinOverride is null)
                {
                    _logger.Debug("Skin {SkinName} not found in {FilePath}", skin.InternalName, filePath);
                    return;
                }

                if (skinOverride.DisplayName.IsNullOrEmpty()) return;

                skin.DisplayName = skinOverride.DisplayName;

                if (skinOverride.Image.IsNullOrEmpty()) return;
                _logger.Debug("Image override is not implemented");
            });

            if (jsonOverride.OverrideKeys is not null && jsonOverride.Keys is not null && jsonOverride.Keys.Any())
            {
                if (jsonOverride.OverrideKeys.Value)
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

    private void MapCustomOverrides(ICharacter character, JsonCharacterOverride @override)
    {
        if (!@override.DisplayName.IsNullOrEmpty())
            character.DisplayName = @override.DisplayName;

        if (!@override.Image.IsNullOrEmpty())
            character.ImageUri = Uri.TryCreate(@override.Image, UriKind.Absolute, out var uri) ? uri : null;
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


        await MapDisplayNames("characters.json", _characters);

        await MapDisplayNames("elements.json", Elements.AllElements);

        await MapDisplayNames("weaponClasses.json", Classes.AllClasses);

        await MapDisplayNames("regions.json", Regions.AllRegions);
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
                { DisplayName = value.DisplayName, InternalName = new InternalName(value.InternalName) };

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

