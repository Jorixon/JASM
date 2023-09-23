using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Entities.Genshin;
using GIMI_ModManager.WinUI.Models.Options;

namespace GIMI_ModManager.WinUI.Models;

public partial class CharacterGridItemModel : ObservableObject, IEquatable<CharacterGridItemModel>,
    IEquatable<GenshinCharacter>
{
    [ObservableProperty] private GenshinCharacter _character;


    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _warning;
    [ObservableProperty] private bool _isHidden;

    [ObservableProperty] private bool _notification;
    [ObservableProperty] private AttentionType _notificationType;

    public CharacterGridItemModel(GenshinCharacter character)
    {
        Character = character;
    }

    public bool Equals(CharacterGridItemModel? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Character.Equals(other.Character);
    }

    public bool Equals(GenshinCharacter? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Character.Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is CharacterGridItemModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Character.GetHashCode();
    }

    public static bool operator ==(CharacterGridItemModel? left, CharacterGridItemModel? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(CharacterGridItemModel? left, CharacterGridItemModel? right)
    {
        return !Equals(left, right);
    }
}