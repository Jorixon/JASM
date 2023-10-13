using System.Text.Json;
using FuzzySharp;
using GIMI_ModManager.Core.Contracts.Services;
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
    private DirectoryInfo _localSettingsDirectory = null!;
    private DirectoryInfo _languageOverrideDirectory = null!;
    public string GameName { get; private set; } = null!;
    public string GameShortName { get; private set; } = null!;

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
    }


    public async Task InitializeAsync(string assetsDirectory, string localSettingsDirectory,
        ICollection<string>? disabledCharacters = null)
    {
        _assetsDirectory = new DirectoryInfo(assetsDirectory);
        if (!_assetsDirectory.Exists)
            throw new DirectoryNotFoundException($"Directory not found at path: {_assetsDirectory.FullName}");

        _localSettingsDirectory = new DirectoryInfo(localSettingsDirectory);
        if (!_localSettingsDirectory.Exists)
            throw new DirectoryNotFoundException($"Directory not found at path: {_localSettingsDirectory.FullName}");

        await InitializeRegionsAsync();

        await InitializeElementsAsync();

        await InitializeClassesAsync();

        await InitializeCharactersAsync(disabledCharacters ?? Array.Empty<string>()).ConfigureAwait(false);
    }

    public Task SetCharacterDisplayNameAsync(ICharacter character, string newDisplayName)
    {
        throw new NotImplementedException();
    }

    public Task SetCharacterImageAsync(ICharacter character, Uri newImageUri)
    {
        throw new NotImplementedException();
    }

    public Task DisableCharacterAsync(ICharacter character)
    {
        throw new NotImplementedException();
    }

    public Task EnableCharacterAsync(ICharacter character)
    {
        throw new NotImplementedException();
    }

    public Task<ICharacter> CreateCharacterAsync(string internalName, string displayName, int rarity,
        Uri? imageUri = null,
        IGameClass? gameClass = null, IGameElement? gameElement = null, ICollection<IRegion>? regions = null,
        ICollection<ICharacterSkin>? additionalSkins = null, DateTime? releaseDate = null)
    {
        throw new NotImplementedException();
    }

    public ICharacter? GetCharacter(string keywords, IEnumerable<ICharacter>? restrictToCharacters = null,
        int minScore = 100)
    {
        var searchResult = GetCharacters(keywords, restrictToCharacters, minScore);

        return searchResult.Any(kv => kv.Value >= minScore) ? searchResult.MaxBy(x => x.Value).Key : null;
    }

    public ICharacter? GetCharacterByName(string internalName)
    {
        return _characters.FirstOrDefault(x => x.InternalNameEquals(internalName));
    }


    public Dictionary<ICharacter, int> GetCharacters(string searchQuery,
        IEnumerable<ICharacter>? restrictToCharacters = null, int minScore = 100)
    {
        var searchResult = new Dictionary<ICharacter, int>();
        searchQuery = searchQuery.ToLower().Trim();

        foreach (var character in restrictToCharacters ?? GetCharacters())
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

    public List<ICharacter> GetCharacters()
    {
        var characters = _characters.ToList();
        characters.Insert(0, getOthersCharacter());
        characters.Add(getGlidersCharacter());
        characters.Add(getWeaponsCharacter());
        return characters;
    }

    public bool IsMultiMod(INameable modNameable)
    {
        var multiMod = new List<string> { "Gliders", "Weapons", "Others" };

        return multiMod.Any(modNameable.InternalNameEquals);
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

        if (!LanguageOverrideAvailable())
            return;

        throw new NotImplementedException();
    }

    private async Task InitializeCharactersAsync(ICollection<string> disabledCharacters)
    {
        const string characterFileName = "characters.json";
        var imageFolderName = Path.Combine(_assetsDirectory.FullName + "Images", "Characters");

        var jsonCharacters = await SerializeAsync<JsonCharacter>(characterFileName);

        foreach (var jsonCharacter in jsonCharacters)
        {
            if (LanguageOverrideAvailable())
                throw new NotImplementedException();


            var character = Character
                .FromJson(jsonCharacter)
                .SetRegion(Regions.AllRegions)
                .SetElement(Elements.AllElements)
                .SetClass(Classes.AllClasses)
                .SetCharacterOverride(null)
                .CreateCharacter(imageFolder: imageFolderName);

            if (disabledCharacters.Any(x => character.InternalName.Equals(x, StringComparison.OrdinalIgnoreCase)))
                _disabledCharacters.Add(character);
            else
                _characters.Add(character);
        }
    }

    private async Task InitializeClassesAsync()
    {
        const string classFileName = "weaponClasses.json";

        var classes = await SerializeAsync<JsonClasses>(classFileName);

        Classes = new Classes("Classes");

        Classes.Initialize(classes, _assetsDirectory.FullName);

        if (!LanguageOverrideAvailable())
            return;

        throw new NotImplementedException();
    }

    private async Task InitializeElementsAsync()
    {
        const string elementsFileName = "elements.json";

        var elements = await SerializeAsync<JsonElement>(elementsFileName);

        Elements = new Elements("Elements");

        Elements.Initialize(elements, _assetsDirectory.FullName);

        if (!LanguageOverrideAvailable())
            return;

        throw new NotImplementedException();
    }

    private bool LanguageOverrideAvailable()
    {
        return false;
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

    private const int _otherCharacterId = -1234;
    public int OtherCharacterId => _otherCharacterId;

    private Character getOthersCharacter()
    {
        var character = new Character
        {
            Id = _otherCharacterId,
            InternalName = "Others",
            DisplayName = "Others",
            ReleaseDate = DateTime.MinValue,
            Rarity = -1,
            Keys = new[] { "others", "unknown" },
            ImageUri = new Uri(Path.Combine(_assetsDirectory.FullName, "Images", "Characters", "Character_Others.png")),
            Element = Elements.AllElements.First(),
            Class = Classes.AllClasses.First()
        };
        return character;
    }

    private const int _glidersCharacterId = -1235;
    public int GlidersCharacterId => _glidersCharacterId;

    private Character getGlidersCharacter()
    {
        var character = new Character
        {
            Id = _glidersCharacterId,
            InternalName = "Gliders",
            DisplayName = "Gliders",
            ReleaseDate = DateTime.MinValue,
            Rarity = -1,
            Keys = new[] { "gliders", "glider", "wings" },
            ImageUri = new Uri(Path.Combine(_assetsDirectory.FullName, "Images", "Characters",
                "Character_Gliders_Thumb.webp")),
            Element = Elements.AllElements.First(),
            Class = Classes.AllClasses.First()
        };
        return character;
    }

    private const int _weaponsCharacterId = -1236;
    public int WeaponsCharacterId => _weaponsCharacterId;

    private Character getWeaponsCharacter()
    {
        var character = new Character
        {
            Id = _weaponsCharacterId,
            InternalName = "Weapons",
            DisplayName = "Weapons",
            ReleaseDate = DateTime.MinValue,
            Rarity = -1,
            Keys = new[] { "weapon", "claymore", "sword", "polearm", "catalyst", "bow" },
            ImageUri = new Uri(Path.Combine(_assetsDirectory.FullName, "Images", "Characters",
                "Character_Weapons_Thumb.webp")),
            Element = Elements.AllElements.First(),
            Class = Classes.AllClasses.First()
        };
        return character;
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
            InternalName = "None"
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
            InternalName = "None"
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

            var newValue = new T() { DisplayName = value.DisplayName, InternalName = value.InternalName };

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