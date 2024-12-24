using System.Diagnostics;
using GIMI_ModManager.Core.GamesService.Interfaces;

namespace GIMI_ModManager.Core.GamesService.Models;

[DebuggerDisplay("{" + nameof(DisplayName) + "}")]
internal class Element : IGameElement, IEquatable<Element>
{
    public Element()
    {
    }

    public InternalName InternalName { get; init; } = null!;
    public string DisplayName { get; set; } = null!;
    public Uri? ImageUri { get; set; } = null;


    public static IGameElement NoneElement()
    {
        return new Element
        {
            InternalName = new InternalName("None"),
            DisplayName = "None",
            ImageUri = null
        };
    }

    public bool Equals(INameable? other) => InternalName.DefaultEquatable(this, other);

    public bool Equals(Element? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return InternalName.Equals(other.InternalName);
    }

    public bool Equals(IGameElement? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return InternalName.Equals(other.InternalName);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is Element other && Equals(other);
    }

    public override int GetHashCode()
    {
        return InternalName.GetHashCode();
    }

    public static bool operator ==(Element? left, Element? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Element? left, Element? right)
    {
        return !Equals(left, right);
    }
}