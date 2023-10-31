namespace GIMI_ModManager.Core.GamesService.Interfaces;

public interface INpc : IModdableObject
{
    public DateTime? ReleaseDate { get; }
    public ICollection<string> Keys { get; }
}