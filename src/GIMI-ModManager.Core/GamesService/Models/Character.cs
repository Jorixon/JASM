using System.Diagnostics;
using GIMI_ModManager.Core.GamesService.Exceptions;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Internals;
using GIMI_ModManager.Core.GamesService.JsonModels;
using GIMI_ModManager.Core.Helpers;
using Serilog;

namespace GIMI_ModManager.Core.GamesService.Models;

[DebuggerDisplay("{" + nameof(DisplayName) + "}")]
public class Character : ICharacter, IEquatable<Character>
{
    private Uri? _imageUri;
    public ICategory ModCategory { get; } = Category.CreateForCharacter();
    public InternalName InternalName { get; init; } = null!;
    public string ModFilesName { get; internal set; } = string.Empty;
    public bool IsMultiMod { get; init; }
    public string DisplayName { get; set; } = null!;
    public int Rarity { get; internal set; }

    public Uri? ImageUri
    {
        get => _imageUri;
        set
        {
            _imageUri = value;
            if (Skins.Count == 0) return;
            Skins.First(sk => sk.IsDefault).ImageUri = value;
        }
    }

    public ICharacter DefaultCharacter { get; internal set; } = null!;
    public IGameClass Class { get; internal set; } = null!;
    public IGameElement Element { get; internal set; } = null!;
    public ICollection<string> Keys { get; set; } = Array.Empty<string>();
    public DateTime? ReleaseDate { get; set; }
    public ICollection<IRegion> Regions { get; internal set; } = Array.Empty<IRegion>();
    public ICollection<ICharacterSkin> Skins { get; internal set; } = new List<ICharacterSkin>();

    public override string ToString()
    {
        return $"{DisplayName} ({InternalName})";
    }

    internal static CharacterBuilder FromJson(JsonCharacter jsonCharacter)
    {
        var internalName = jsonCharacter.InternalName ??
                           throw new InvalidJsonConfigException("InternalName can never be missing or null");

        var character = new Character
        {
            InternalName = new InternalName(jsonCharacter.InternalName),
            ModFilesName = jsonCharacter.ModFilesName ?? string.Empty,
            DisplayName = jsonCharacter.DisplayName ?? internalName,
            IsMultiMod = jsonCharacter.IsMultiMod ?? false,
            Keys = jsonCharacter.Keys ?? Array.Empty<string>(),
            Rarity = jsonCharacter.Rarity is >= 0 and <= 5 ? jsonCharacter.Rarity.Value : -1,
            ReleaseDate = DateTime.TryParse(jsonCharacter.ReleaseDate, out var date) ? date : DateTime.MaxValue,
            Skins = new List<ICharacterSkin>()
        };

        // Add default skin
        character.Skins.Add(CreateDefaultSkin(character));


        if (jsonCharacter.InGameSkins == null) return new CharacterBuilder(character, jsonCharacter);

        // Add additional skins
        foreach (var jsonCharacterInGameSkin in jsonCharacter.InGameSkins)
        {
            var skin = CharacterSkin.FromJson(character, jsonCharacterInGameSkin);
            character.Skins.Add(skin);
        }

        return new CharacterBuilder(character, jsonCharacter);
    }


    internal static CharacterBuilder FromCustomCharacter(InternalCreateCharacterRequest createCharacterRequest)
    {
        if (string.IsNullOrWhiteSpace(createCharacterRequest.InternalName))
            throw new InvalidModdableObjectException("InternalName cannot be null or empty");

        var character = new Character
        {
            InternalName = createCharacterRequest.InternalName,
            ModFilesName = createCharacterRequest.ModFilesName ?? string.Empty,
            DisplayName = createCharacterRequest.DisplayName ?? createCharacterRequest.InternalName,
            IsMultiMod = createCharacterRequest.IsMultiMod,
            Keys = createCharacterRequest.Keys?.ToArray() ?? [],
            Rarity = createCharacterRequest.Rarity is >= 0 and <= 5 ? createCharacterRequest.Rarity : -1,
            ReleaseDate = createCharacterRequest.ReleaseDate ?? DateTime.MaxValue,
            Skins = new List<ICharacterSkin>()
        };

        // Add default skin
        character.Skins.Add(CreateDefaultSkin(character));

        return new CharacterBuilder(character, createCharacterRequest);
    }

    internal static CharacterBuilder FromJson(string internalName, JsonCustomCharacter jsonCustomCharacter)
    {
        var jsonCharacter = new JsonCharacter()
        {
            InternalName = internalName,
            DisplayName = jsonCustomCharacter.DisplayName,
            Keys = jsonCustomCharacter.Keys,
            Image = jsonCustomCharacter.Image,
            Rarity = jsonCustomCharacter.Rarity,
            Element = jsonCustomCharacter.Element,
            Class = jsonCustomCharacter.Class,
            Region = jsonCustomCharacter.Region,
            ReleaseDate = jsonCustomCharacter.ReleaseDate,
            ModFilesName = jsonCustomCharacter.ModFilesName,
            IsMultiMod = jsonCustomCharacter.IsMultiMod
        };

        return FromJson(jsonCharacter);
    }


    internal IEnumerable<ICharacterSkin> ClearAndReturnSkins()
    {
        var skins = Skins.Where(s => !s.IsDefault).ToArray();

        var defaultSkin = Skins.First(s => s.IsDefault);

        Skins.Clear();

        Skins.Add(defaultSkin);

        DefaultCharacter = Clone();

        return skins;
    }

    internal Character FromCharacterSkin(ICharacterSkin characterSkin)
    {
        if (characterSkin.IsDefault)
            throw new InvalidOperationException("Cannot create character from default skin");

        var character = Clone(characterSkin.InternalName);

        character.ModFilesName = characterSkin.ModFilesName;
        character.DisplayName = DisplayName + " " + characterSkin.DisplayName;
        character.Keys = new List<string>() { DisplayName, InternalName };
        character.Rarity = characterSkin.Rarity is > 0 and <= 5 ? characterSkin.Rarity : character.Rarity;
        character.ReleaseDate = characterSkin.ReleaseDate == DateTime.MaxValue ? ReleaseDate : character.ReleaseDate;
        character.ImageUri = characterSkin.ImageUri;

        character.Skins = new List<ICharacterSkin>() { CreateDefaultSkin(character) };
        character.DefaultCharacter = character.Clone();

        return character;
    }

    internal Character Clone(InternalName? internalName = null)
    {
        return new Character
        {
            InternalName = internalName ?? InternalName,
            ModFilesName = ModFilesName,
            DisplayName = DisplayName,
            IsMultiMod = IsMultiMod,
            Keys = Keys,
            Rarity = Rarity,
            ReleaseDate = ReleaseDate,
            Skins = Skins.Select(skin => skin.Clone()).ToArray(),
            Class = Class,
            Element = Element,
            Regions = Regions,
            ImageUri = ImageUri
        };
    }

    private static ICharacterSkin CreateDefaultSkin(Character character) =>
        new CharacterSkin(character)
        {
            InternalName = new InternalName("Default_" + character.InternalName),
            ModFilesName = character.ModFilesName,
            DisplayName = "Default",
            Rarity = character.Rarity,
            ReleaseDate = character.ReleaseDate,
            Character = character,
            IsDefault = true
        };

    internal Character()
    {
    }

    public Character(string internalName, string displayName)
    {
        InternalName = new InternalName(internalName);
        DisplayName = displayName;
        Class = Models.Class.NoneClass();
        Element = Models.Element.NoneElement();
    }


    public class InvalidJsonConfigException : Exception
    {
        public InvalidJsonConfigException(string message) : base(message)
        {
        }
    }

    public bool Equals(Character? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(InternalName, other.InternalName, StringComparison.OrdinalIgnoreCase);
    }

    public bool Equals(ICharacter? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(InternalName, other.InternalName, StringComparison.OrdinalIgnoreCase);
    }

    public bool Equals(IModdableObject? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(InternalName, other.InternalName, StringComparison.OrdinalIgnoreCase);
    }

    public bool Equals(INameable? other)
    {
        return InternalName.DefaultEquatable(this, other);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is Character other && Equals(other);
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return StringComparer.OrdinalIgnoreCase.GetHashCode(InternalName);
    }

    public static bool operator ==(Character? left, Character? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Character? left, Character? right)
    {
        return !Equals(left, right);
    }
}

internal sealed class CharacterBuilder
{
    private readonly Character _character;
    private readonly string[]? _regions;
    private readonly string? _class;
    private readonly string? _element;
    private readonly string? _image;
    private readonly JsonCharacterSkin[]? _jsonCharacterSkins;

    private bool _regionSet;
    private bool _classSet;
    private bool _elementSet;

    public CharacterBuilder(Character character, JsonCharacter jsonCharacter)
    {
        _character = character;
        _regions = jsonCharacter.Region;
        _class = jsonCharacter.Class;
        _element = jsonCharacter.Element;
        _image = jsonCharacter.Image;
        _jsonCharacterSkins = jsonCharacter.InGameSkins;
    }

    public CharacterBuilder(Character character, InternalCreateCharacterRequest characterRequest)
    {
        _character = character;
        _regions = characterRequest.Region;
        _class = characterRequest.Class;
        _element = characterRequest.Element;
        _image = characterRequest.Image;
    }

    public CharacterBuilder SetRegion(IReadOnlyCollection<IRegion> regions)
    {
        if (_regionSet)
            throw new InvalidOperationException("Region already set");

        var connectedRegions = new List<IRegion>();

        foreach (var internalRegionName in _regions ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(internalRegionName))
                throw new InvalidOperationException("Region cannot be null or empty");

            var region = regions.FirstOrDefault(region =>
                region.InternalName.Equals(internalRegionName));

            if (region is not null)
            {
                connectedRegions.Add(region);
            }
            else
                Log.Debug("Region {Region} not found in regions list", internalRegionName);
        }

        _character.Regions = connectedRegions;

        if (connectedRegions.Count == 0)
            Log.Debug("No regions found for character {Character}", _character.DisplayName);

        _regionSet = true;
        return this;
    }

    public CharacterBuilder SetClass(IReadOnlyCollection<IGameClass> gameClasses)
    {
        if (_classSet)
            throw new InvalidOperationException("Class already set");

        _character.Class = gameClasses.FirstOrDefault(gameClass =>
                               gameClass.InternalNameEquals(_class))
                           ?? Class.NoneClass();

        _classSet = true;
        return this;
    }

    public CharacterBuilder SetElement(IReadOnlyCollection<IGameElement> elements)
    {
        if (_elementSet)
            throw new InvalidOperationException("Element already set");

        _character.Element = elements.FirstOrDefault(element =>
                                 element.InternalNameEquals(_element))
                             ?? Element.NoneElement();


        _elementSet = true;
        return this;
    }


    private Uri? GetImage(string imageFolder, string? image)
    {
        if (image.IsNullOrEmpty()) return null;

        var imagePath = Path.Combine(imageFolder, image);
        if (File.Exists(imagePath))
            return new Uri(imagePath);

        Log.Debug("Image {Image} for {Name} {CharacterName} is invalid", image, _character.DisplayName,
            _character.InternalName);
        return null;
    }

    public Character CreateCharacter(string? imageFolder = null, string? characterSkinImageFolder = null)
    {
        if (!_regionSet)
            throw new InvalidOperationException("Region not set");

        if (!_classSet)
            throw new InvalidOperationException("Class not set");

        if (!_elementSet)
            throw new InvalidOperationException("Element not set");

        if (!_image.IsNullOrEmpty() && !imageFolder.IsNullOrEmpty())
        {
            // Set images
            _character.ImageUri = GetImage(imageFolder, _image);

            _character.Skins.First().ImageUri = _character.ImageUri;
        }


        foreach (var characterSkin in _character.Skins)
        {
            var jsonCharacterSkin = _jsonCharacterSkins?.FirstOrDefault(skin => skin.InternalName is not null &&
                                                                                skin.InternalName.Equals(characterSkin.InternalName,
                                                                                    StringComparison.OrdinalIgnoreCase));

            if (jsonCharacterSkin is null || characterSkinImageFolder.IsNullOrEmpty())
                continue;

            characterSkin.ImageUri = GetImage(characterSkinImageFolder, jsonCharacterSkin.Image);
        }


        _character.DefaultCharacter = _character.Clone();

        return _character;
    }
}