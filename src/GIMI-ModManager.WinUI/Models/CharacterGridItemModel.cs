using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.WinUI.Services.Notifications;

namespace GIMI_ModManager.WinUI.Models;

public partial class CharacterGridItemModel : ObservableObject, IEquatable<CharacterGridItemModel>,
    IEquatable<IModdableObject>
{
    [ObservableProperty] private IModdableObject _character;


    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _warning;
    [ObservableProperty] private bool _isHidden;

    [ObservableProperty] private bool _notification;
    [ObservableProperty] private AttentionType _notificationType;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasMods))]
    private int _modCount;

    [ObservableProperty] private bool _hasMods;

    [ObservableProperty] private ObservableCollection<CharacterModItem> _mods = new();

    public CharacterGridItemModel(IModdableObject character, int modCount = 0)
    {
        Character = character;
        ModCount = modCount;
        _hasMods = modCount > 0;
    }

    public bool Equals(CharacterGridItemModel? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Character.Equals(other.Character);
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

public record CharacterModItem(string Name, DateTime DateAdded);