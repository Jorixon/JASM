namespace GIMI_ModManager.Core.GamesService;

public interface IGameService
{
    public string GameName { get; }
    public string GameShortName { get; }

    public Task InitializeAsync(string assetsDirectory, string localSettingsDirectory,
        ICollection<string>? disabledCharacters = null);

    public Task SetCharacterDisplayNameAsync(ICharacter character, string newDisplayName);
    public Task SetCharacterImageAsync(ICharacter character, Uri newImageUri);

    public Task DisableCharacterAsync(ICharacter character);

    public Task EnableCharacterAsync(ICharacter character);

    public Task<ICharacter> CreateCharacterAsync(string internalName, string displayName, int rarity,
        Uri? imageUri = null, IGameClass? gameClass = null, IGameElement? gameElement = null,
        ICollection<IRegion>? regions = null, ICollection<ICharacterSkin>? additionalSkins = null,
        DateTime? releaseDate = null);

    public ICharacter? GetCharacter(string keywords,
        IEnumerable<ICharacter>? restrictToCharacters = null, int minScore = 100);

    public ICharacter? GetCharacterByName(string internalName);

    public Dictionary<ICharacter, int> GetCharacters(string searchQuery,
        IEnumerable<ICharacter>? restrictToCharacters = null, int minScore = 100);

    public List<IGameElement> GetElements();

    public List<IGameClass> GetClasses();

    public List<IRegion> GetRegions();

    public List<ICharacter> GetCharacters();

    public bool IsMultiMod(INameable modNameable);
    public bool IsMultiMod(string modInternalName);
    public string OtherCharacterInternalName { get; }
    public string GlidersCharacterInternalName { get; }
    public string WeaponsCharacterInternalName { get; }
}

/// <summary>
/// Has specific name for mod files
/// </summary>
public interface IModdableObject
{
    /// <summary>
    /// Static should not be changed
    /// </summary>
    public string ModFilesName { get; }
}

public interface ICharacterSkin : IModdableObject, IRarity, IImageSupport, INameable
{
    /// <summary>
    /// Character this skin belongs to
    /// </summary>
    public ICharacter Character { get; }

    public bool IsDefault { get; }

    public DateTime? ReleaseDate { get; }
}

/// <summary>
/// In game playable character
/// </summary>
public interface ICharacter : IRarity, IImageSupport, INameable, IModdableObject, IEquatable<ICharacterSkin>
{
    public IGameClass Class { get; }
    public IGameElement Element { get; }
    public ICollection<string> Keys { get; }

    public DateTime ReleaseDate { get; }
    public ICollection<IRegion> Regions { get; }
    public ICollection<ICharacterSkin> Skins { get; }
}

public interface INpc : IImageSupport, INameable
{
    public IModdableObject? ModdableObject { get; }
}

public interface IUi : INameable
{
    public IModdableObject? ModdableObject { get; }
}

public interface IGliders : INameable
{
    public IModdableObject? ModdableObject { get; }
}

public interface IWeapon : IRarity, INameable
{
    public IModdableObject? ModdableObject { get; }
}

// Genshin => weapon
// Honkai => Path
public interface IGameClass : IImageSupport, INameable
{
    //public int Id { get; }
}

// Genshin => Element
// Honkai => Element
public interface IGameElement : IImageSupport, INameable
{
    //public int Id { get; }
}

public interface IRegion : INameable
{
    //public int Id { get; }
}

public interface IRarity
{
    public int Rarity { get; }
}

/// <summary>
/// Has image support
/// </summary>
public interface IImageSupport
{
    public Uri? ImageUri { get; internal set; }
}

public interface ICustomMod : IModdableObject, INameable
{
    public IImageSupport? Image { get; }
    public IRarity? Rarity { get; }
    public DateTime? ReleaseDate { get; }
}

public interface INameable
{
    /// <summary>
    /// Can be customized by user
    /// </summary>
    public string DisplayName { get; internal set; }

    /// <summary>
    /// Static should not be changed. Is used to identify the object
    /// </summary>
    public string InternalName { get; internal set; }

    public bool InternalNameEquals(string other)
    {
        return InternalName.Equals(other, StringComparison.OrdinalIgnoreCase);
    }

    public bool InternalNameEquals(INameable other)
    {
        return InternalNameEquals(other.InternalName);
    }
}

public interface ICategory
{
    public ModCategory Category { get; }
    public string InternalCategoryName { get; }
}

public enum ModCategory
{
    None,
    Character,
    NPC,
    Object,
    Ui,
    Gliders,
    Weapons,
    Custom
}