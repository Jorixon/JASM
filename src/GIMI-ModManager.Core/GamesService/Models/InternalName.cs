using System.Globalization;

namespace GIMI_ModManager.Core.GamesService.Models;

public sealed class InternalName : IEquatable<InternalName>, IEquatable<string>
{
    public string Id { get; }


    public InternalName(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id, nameof(id));
        Id = id.Trim().ToLower(CultureInfo.InvariantCulture);
    }

    public bool Equals(InternalName? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
    }

    public bool Equals(string? other)
    {
        if (ReferenceEquals(null, other)) return false;
        return string.Equals(Id, other, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) || obj is InternalName other && Equals(other);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Id);

    public static bool operator ==(InternalName? left, InternalName? right) => Equals(left, right);

    public static bool operator !=(InternalName? left, InternalName? right) => !Equals(left, right);

    public override string ToString() => Id;

    public static implicit operator string(InternalName internalName) => internalName.Id;
}