namespace GIMI_ModManager.Core.GamesService.Interfaces;

public interface INPC : IModdableObject, IImageSupport
{
    public ICollection<IRegion> Regions { get; }
}