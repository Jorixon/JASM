namespace GIMI_ModManager.Core.GamesService.Interfaces;

public interface ICustomMod : INameable, IImageSupport
{
    public IModdableObject? ModdableObject { get; }
    public IRarity? Rarity { get; }
    public DateTime? ReleaseDate { get; }
}