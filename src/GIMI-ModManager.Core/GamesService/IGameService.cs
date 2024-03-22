using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;

namespace GIMI_ModManager.Core.GamesService;

public interface IGameService
{
    public GameInfo GameInfo { get; }
    public string GameName { get; }
    public string GameShortName { get; }
    public string GameIcon { get; }
    public Uri GameBananaUrl { get; }

    public event EventHandler? Initialized;

    public Task<GameInfo?> GetGameInfoAsync(SupportedGames game);

    public Task<ICollection<InternalName>> PreInitializedReadModObjectsAsync(string assetsDirectory);

    public Task InitializeAsync(string assetsDirectory, string localSettingsDirectory);

    public Task InitializeAsync(InitializationOptions options);

    public Task SetCharacterDisplayNameAsync(ICharacter character, string newDisplayName);
    public Task SetCharacterImageAsync(ICharacter character, Uri newImageUri);

    public Task DisableCharacterAsync(ICharacter character);

    public Task EnableCharacterAsync(ICharacter character);
    public Task ResetOverrideForCharacterAsync(ICharacter character);


    public Task<ICharacter> CreateCharacterAsync(string internalName, string displayName, int rarity,
        Uri? imageUri = null, IGameClass? gameClass = null, IGameElement? gameElement = null,
        ICollection<IRegion>? regions = null, ICollection<ICharacterSkin>? additionalSkins = null,
        DateTime? releaseDate = null);

    public ICharacter? QueryCharacter(string keywords,
        IEnumerable<ICharacter>? restrictToCharacters = null, int minScore = 100);

    public ICharacter? GetCharacterByIdentifier(string internalName, bool includeDisabledCharacters = false);


    public Dictionary<ICharacter, int> QueryCharacters(string searchQuery,
        IEnumerable<ICharacter>? restrictToCharacters = null, int minScore = 100,
        bool includeDisabledCharacters = false);

    public Dictionary<IModdableObject, int> QueryModdableObjects(string searchQuery,
        ICategory? category = null, int minScore = 100);

    public List<IGameElement> GetElements();

    public List<IGameClass> GetClasses();

    public List<IRegion> GetRegions();

    public List<ICharacter> GetCharacters(bool includeDisabled = false);
    public List<ICharacter> GetDisabledCharacters();

    public List<ICategory> GetCategories();
    public List<IModdableObject> GetModdableObjects(ICategory category, GetOnly getOnlyStatus = GetOnly.Enabled);
    public List<IModdableObject> GetAllModdableObjects(GetOnly getOnlyStatus = GetOnly.Enabled);

    public List<T> GetAllModdableObjectsAsCategory<T>(GetOnly getOnlyStatus = GetOnly.Enabled)
        where T : IModdableObject;

    public IModdableObject? GetModdableObjectByIdentifier(InternalName internalName,
        GetOnly getOnlyStatus = GetOnly.Enabled);

    public bool IsMultiMod(IModdableObject moddableObject);
    public string OtherCharacterInternalName { get; }
    public string GlidersCharacterInternalName { get; }
    public string WeaponsCharacterInternalName { get; }
}

public class InitializationOptions
{
    public required string AssetsDirectory { get; set; }
    public required string LocalSettingsDirectory { get; set; }
    public ICollection<string>? DisabledCharacters { get; set; }

    public bool CharacterSkinsAsCharacters { get; set; }
}

public enum GetOnly
{
    Enabled,
    Disabled,
    Both
}

// Genshin => weapon
// Honkai => Path
public interface IGameClass : IImageSupport, INameable
{
}

// Genshin => Element
// Honkai => Element
public interface IGameElement : IImageSupport, INameable
{
}

// Genshin => Mondstadt
// Honkai => Belobog
public interface IRegion : INameable
{
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
    /// <summary>
    /// This is null if no image is available
    /// </summary>
    public Uri? ImageUri { get; internal set; }
}

public interface IDateSupport
{
    public DateTime? ReleaseDate { get; internal set; }
}

/// <summary>
/// Base Interface that allows identification by internal name
/// </summary>
public interface INameable : IEquatable<INameable>
{
    /// <summary>
    /// Is displayed to the user.
    /// Can also be customized by user
    /// </summary>
    public string DisplayName { get; internal set; }

    /// <summary>
    /// Should not be changed. Is used to identify the object
    /// </summary>
    public InternalName InternalName { get; internal init; }

    public bool InternalNameEquals(string? other) => InternalName.Equals(other);

    public bool InternalNameEquals(INameable other) => InternalNameEquals(other.InternalName);
}

public interface IUi : IModdableObject
{
}

public interface IGliders : IModdableObject
{
}

public interface IWeapon : IModdableObject, IRarity
{
    public IGameClass GameClass { get; }
}

public interface ICategory : INameable, IEquatable<ICategory>
{
    public string DisplayNamePlural { get; internal set; }
    ModCategory ModCategory { get; init; }
    public Type ModdableObjectType { get; }
}

public enum ModCategory
{
    Character,
    NPC,
    Object,
    Ui,
    Gliders,
    Weapons,
    Custom
}