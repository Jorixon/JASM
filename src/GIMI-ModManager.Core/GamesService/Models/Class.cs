using System.Diagnostics;
using GIMI_ModManager.Core.GamesService.Interfaces;

namespace GIMI_ModManager.Core.GamesService.Models;

[DebuggerDisplay("{" + nameof(DisplayName) + "}")]
internal class Class : IGameClass, IEquatable<Class>
{
    public Class()
    {
    }

    public InternalName InternalName { get; init; } = null!;
    public string DisplayName { get; set; } = null!;
    public Uri? ImageUri { get; set; } = null;

    public static Class NoneClass()
    {
        return new Class
        {
            InternalName = new InternalName("None"),
            DisplayName = "None",
            ImageUri = null
        };
    }

    public bool Equals(INameable? other) => InternalName.DefaultEquatable(this, other);

    public bool Equals(Class? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return InternalName.Equals(other.InternalName);
    }

    public bool Equals(IGameClass? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return InternalName.Equals(other.InternalName);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is Class other && Equals(other);
    }

    public override int GetHashCode()
    {
        return InternalName.GetHashCode();
    }

    public static bool operator ==(Class? left, Class? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Class? left, Class? right)
    {
        return !Equals(left, right);
    }
}