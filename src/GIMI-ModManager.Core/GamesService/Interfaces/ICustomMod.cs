namespace GIMI_ModManager.Core.GamesService.Interfaces;

public interface ICustomMod : IImageSupport, IModdableObject
{
    public IRarity? Rarity { get; }
    public DateTime? ReleaseDate { get; }
    public ICollection<string> Keys { get; }
}