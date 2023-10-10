namespace GIMI_ModManager.Core.GamesService;

public interface IGameService
{
    public string GameName { get; }
    public string GameShortName { get; }

    public Task InitializeAsync(string assetsDirectory, string localSettingsDirectory);
}

public interface IModdableObject
{
    public string? Category { get; }

    /// <summary>
    /// Static should not be changed
    /// </summary>
    public string InternalName { get; }

    /// <summary>
    /// Static should not be changed
    /// </summary>
    public string ModFilesName { get; }

    /// <summary>
    /// Can be customized by user
    /// </summary>
    public string DisplayName { get; }
}

public interface ICharacterSkin : IModdableObject, IRarity, IImageSupport
{
    public ICharacter Character { get; }
}

public interface IModEntity : IModdableObject
{
    public int Id { get; }
}

public interface ICharacter : IModEntity, IRarity, IImageSupport
{
    public IGameClass Class { get; }
    public IGameElement Element { get; }

    public DateTime ReleaseDate { get; }
    public ICollection<IRegion> Regions { get; }
    public ICollection<ICharacterSkin> AdditionalSkins { get; }
}

public interface INpc : IModEntity, IImageSupport
{
}

public interface IUi : IModdableObject
{
}

public interface IGliders : IModEntity
{
}

public interface IWeapon : IModEntity, IRarity
{
    public int WeaponType { get; }
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
    public string RarityName { get; }
}

public interface IImageSupport
{
    public Uri? ImageUri { get; }
}

public interface ICustomMod : IModdableObject
{
    public IImageSupport? Image { get; }
    public IRarity? Rarity { get; }
    public DateTime? ReleaseDate { get; }
}

public interface INameable
{
    public string InternalName { get; }
    public string DisplayName { get; }
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