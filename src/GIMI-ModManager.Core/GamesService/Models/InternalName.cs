namespace GIMI_ModManager.Core.GamesService.Models;

public class InternalName : IInternalName, IEquatable<InternalName>, IEquatable<string>
{
    public string Id { get; }


    public InternalName(string id)
    {
        ArgumentNullException.ThrowIfNull(id, nameof(id));
        Id = id.Trim().ToLower();
    }

    public bool Equals(InternalName? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Id, other.Id, StringComparison.CurrentCultureIgnoreCase);
    }

    public bool Equals(IInternalName? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Id, other.Id, StringComparison.CurrentCultureIgnoreCase);
    }

    public bool Equals(string? other)
    {
        if (ReferenceEquals(null, other)) return false;
        return string.Equals(Id, other, StringComparison.CurrentCultureIgnoreCase);
    }

    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) || obj is InternalName other && Equals(other);

    public override int GetHashCode() => StringComparer.CurrentCultureIgnoreCase.GetHashCode(Id);

    public static bool operator ==(InternalName? left, InternalName? right) => Equals(left, right);

    public static bool operator !=(InternalName? left, InternalName? right) => !Equals(left, right);

    public override string ToString() => Id;

    public static implicit operator string(InternalName internalName) => internalName.Id;
}

public interface IInternalName : IEquatable<IInternalName>
{
    public string Id { get; }
}