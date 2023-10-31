using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.WinUI.Services.Notifications;

namespace GIMI_ModManager.WinUI.Models;

public partial class CharacterGridItemModel : ObservableObject, IEquatable<CharacterGridItemModel>,
    IEquatable<ICharacter>, IEquatable<IModdableObject>
{
    [ObservableProperty] private ICharacter _character;


    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _warning;
    [ObservableProperty] private bool _isHidden;

    [ObservableProperty] private bool _notification;
    [ObservableProperty] private AttentionType _notificationType;

    public CharacterGridItemModel(ICharacter character)
    {
        Character = character;
    }

    public bool Equals(CharacterGridItemModel? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Character.Equals(other.Character);
    }

    public bool Equals(ICharacter? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Character.InternalNameEquals(other.InternalName);
    }

    public bool Equals(IModdableObject? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Character.InternalNameEquals(other.InternalName);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is CharacterGridItemModel other && Equals(other);
    }

    public override int GetHashCode() => Character.GetHashCode();

    public static bool operator ==(CharacterGridItemModel? left, CharacterGridItemModel? right) => Equals(left, right);

    public static bool operator !=(CharacterGridItemModel? left, CharacterGridItemModel? right) => !Equals(left, right);
}