using System.Text.Json.Serialization;

namespace GIMI_ModManager.Core.GamesService.Interfaces;

public interface INpc : IModdableObject, IEquatable<INpc>, IDateSupport
{
    [Newtonsoft.Json.JsonIgnore]
    [JsonIgnore]
    public INpc DefaultNPC { get; }

    public ICollection<IRegion> Regions { get; }
}