#nullable enable
using System.Reflection;
using FuzzySharp;
using GIMI_ModManager.Core.Entities;
using Newtonsoft.Json;
using Serilog;

namespace GIMI_ModManager.Core.Services;

public class GenshinService : IGenshinService
{
    private readonly ILogger? _logger;

    private List<GenshinCharacter> _characters = new();

    private string _assetsUriPath = string.Empty;

    public GenshinService(ILogger? logger = null)
    {
        _logger = logger?.ForContext<GenshinService>();
    }

    public async Task InitializeAsync(string assetsUriPath)
    {
        _logger?.Debug("Initializing GenshinService");
        var uri = new Uri(Path.Combine(assetsUriPath, "characters.json"));
        _assetsUriPath = assetsUriPath;
        var json = await File.ReadAllTextAsync(uri.LocalPath);


        var characters = JsonConvert.DeserializeObject<List<GenshinCharacter>>(json);
        //var characters = new[] { characterss };
        if (characters == null || !characters.Any())
        {
            _logger?.Error("Failed to deserialize GenshinCharacter list");
            return;
        }

        foreach (var character in characters) SetImageUriForCharacter(assetsUriPath, character);

        _characters.AddRange(characters);
        _characters.Add(getGlidersCharacter(assetsUriPath));
        _characters.Add(getOthersCharacter(assetsUriPath));
        _characters.Add(getWeaponsCharacter(assetsUriPath));
    }

    private static void SetImageUriForCharacter(string assetsUriPath, GenshinCharacter character)
    {
        if (character.ImageUri is not null && character.ImageUri.StartsWith("Character_"))
            character.ImageUri = $"{assetsUriPath}/Images/{character.ImageUri}";
    }

    public IEnumerable<GenshinCharacter> GetCharacters()
    {
        return _characters;
    }

    public GenshinCharacter? GetCharacter(string keywords,
        IEnumerable<GenshinCharacter>? restrictToGenshinCharacters = null, int fuzzRatio = 70)
    {
        var searchResult = new Dictionary<GenshinCharacter, int>();

        foreach (var character in restrictToGenshinCharacters ?? _characters)
        {
            var result = Fuzz.PartialRatio(keywords, character.DisplayName);

            if (keywords.Contains(character.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                keywords.Contains(character.DisplayName.Trim(), StringComparison.OrdinalIgnoreCase))
                return character;

            if (keywords.ToLower().Split().Any(modKeyWord =>
                    character.Keys.Any(characterKeyWord => characterKeyWord.ToLower() == modKeyWord)))
                return character;

            if (result == 100) return character;

            if (result > fuzzRatio) searchResult.Add(character, result);
        }

        return searchResult.Any() ? searchResult.MaxBy(s => s.Value).Key : null;
    }

    public Dictionary<GenshinCharacter, int> GetCharacters(string searchQuery,
        IEnumerable<GenshinCharacter>? restrictToGenshinCharacters = null, int minScore = 100)
    {
        var searchResult = new Dictionary<GenshinCharacter, int>();
        searchQuery = searchQuery.ToLower();

        foreach (var character in restrictToGenshinCharacters ?? _characters)
        {
            var loweredDisplayName = character.DisplayName.ToLower();

            var result = 0;

            // If the search query contains the display name, we give it a lot of points
            var sameChars = loweredDisplayName.Split().Count(searchQuery.Contains);
            result += sameChars * 50;


            // A character can have multiple keys, so we take the best one. The keys are only used to help with searching
            var bestKeyMatch = character.Keys.Max(key => Fuzz.Ratio(key, searchQuery));
            result += bestKeyMatch;


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


    private const int _otherCharacterId = -1234;
    public int OtherCharacterId => _otherCharacterId;

    private static GenshinCharacter getOthersCharacter(string assetsUriPath)
    {
        var character = new GenshinCharacter
        {
            Id = _otherCharacterId,
            DisplayName = "Others",
            ReleaseDate = DateTime.MinValue,
            Rarity = -1,
            Keys = new[] { "others", "unknown" },
            ImageUri = "Character_Others.png",
            Element = string.Empty,
            Weapon = string.Empty
        };
        SetImageUriForCharacter(assetsUriPath, character);
        return character;
    }

    private const int _glidersCharacterId = -1235;
    public int GlidersCharacterId => _glidersCharacterId;

    private static GenshinCharacter getGlidersCharacter(string assetsUriPath)
    {
        var character = new GenshinCharacter
        {
            Id = _glidersCharacterId,
            DisplayName = "Gliders",
            ReleaseDate = DateTime.MinValue,
            Rarity = -1,
            Keys = new[] { "gliders", "glider", "wings" },
            ImageUri = "Character_Gliders_Thumb.webp"
        };
        SetImageUriForCharacter(assetsUriPath, character);
        return character;
    }

    private const int _weaponsCharacterId = -1236;
    public int WeaponsCharacterId => _weaponsCharacterId;

    private static GenshinCharacter getWeaponsCharacter(string assetsUriPath)
    {
        var character = new GenshinCharacter
        {
            Id = _weaponsCharacterId,
            DisplayName = "Weapons",
            ReleaseDate = DateTime.MinValue,
            Rarity = -1,
            Keys = new[] { "weapon", "claymore", "sword", "polearm", "catalyst", "bow" },
            ImageUri = "Character_Weapons_Thumb.webp"
        };
        SetImageUriForCharacter(assetsUriPath, character);
        return character;
    }

    public GenshinCharacter? GetCharacter(int id)
    {
        return _characters.FirstOrDefault(c => c.Id == id);
    }

    public bool IsMultiModCharacter(GenshinCharacter character)
    {
        return IsMultiModCharacter(character.Id);
    }

    public bool IsMultiModCharacter(int characterId)
    {
        return characterId == OtherCharacterId || characterId == GlidersCharacterId ||
               characterId == WeaponsCharacterId;
    }
}

public interface IGenshinService
{
    public Task InitializeAsync(string jsonFile);
    public IEnumerable<GenshinCharacter> GetCharacters();

    public GenshinCharacter? GetCharacter(string keywords,
        IEnumerable<GenshinCharacter>? restrictToGenshinCharacters = null, int fuzzRatio = 70);

    public Dictionary<GenshinCharacter, int> GetCharacters(string searchQuery,
        IEnumerable<GenshinCharacter>? restrictToGenshinCharacters = null, int minScore = 70);

    public GenshinCharacter? GetCharacter(int id);
    public int OtherCharacterId { get; }
    public int GlidersCharacterId { get; }
    public int WeaponsCharacterId { get; }

    public bool IsMultiModCharacter(GenshinCharacter character);
    public bool IsMultiModCharacter(int characterId);
}

internal static class GenshinCharacters
{
    internal static IEnumerable<GenshinCharacter> AllCharacters()
    {
        return typeof(GenshinCharacters).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(GenshinCharacter))
            .Select(f => (GenshinCharacter)f.GetValue(null)!);
    }


    internal static readonly GenshinCharacter Amber = new()
    {
        DisplayName = "Amber",
        ReleaseDate = new DateTime(2020, 9, 28),
        Rarity = 4,
        Element = "Pyro",
        Weapon = "Bow",
        Region = new[] { "Mondstadt" }
    };

    internal static readonly GenshinCharacter Barbara = new()
    {
        DisplayName = "Barbara",
        ReleaseDate = new DateTime(2020, 9, 28),
        Rarity = 4,
        Element = "Hydro",
        Weapon = "Catalyst",
        Region = new[] { "Mondstadt" }
    };

    internal static readonly GenshinCharacter Deluc = new()
    {
        DisplayName = "Deluc",
        ReleaseDate = new DateTime(2020, 9, 28),
        Rarity = 5,
        Element = "Pyro",
        Weapon = "Claymore",
        Region = new[] { "Mondstadt" }
    };
}