#nullable enable
namespace GIMI_ModManager.Core.Entities;

public record GenshinCharacter : IGenshinCharacter, IEqualityComparer<GenshinCharacter>
{
    public int Id { get; set; } = -1;
    public string DisplayName { get; set; } = string.Empty;
    public string[] Keys { get; set; } = Array.Empty<string>();
    public DateTime ReleaseDate { get; set; } = DateTime.MinValue;
    public string? ImageUri { get; set; }
    public int Rarity { get; set; }
    public string Element { get; set; } = string.Empty;
    public string Weapon { get; set; } = string.Empty;
    public string[] Region { get; set; } = Array.Empty<string>();

    public bool Equals(GenshinCharacter? x, GenshinCharacter? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.Id == y.Id;
    }

    public int GetHashCode(GenshinCharacter obj)
    {
        return obj.Id;
    }
}

public interface IGenshinCharacter
{
    int Id { get; }
    public string DisplayName { get; set; }
    public string[] Keys { get; set; }
    public DateTime ReleaseDate { get; set; }
    public string? ImageUri { get; set; }
    public int Rarity { get; set; }
    public string Element { get; set; }
    public string Weapon { get; set; }
    public string[] Region { get; set; }
}