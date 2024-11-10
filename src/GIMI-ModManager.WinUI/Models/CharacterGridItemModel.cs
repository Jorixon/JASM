using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.WinUI.Services.Notifications;

namespace GIMI_ModManager.WinUI.Models;

public partial class CharacterGridItemModel : ObservableObject, IEquatable<CharacterGridItemModel>,
    IEquatable<IModdableObject>
{
    [ObservableProperty] private IModdableObject _character;
    [ObservableProperty] private Uri _imageUri;

    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _warning;
    [ObservableProperty] private bool _isHidden;

    [ObservableProperty] private bool _notification;
    [ObservableProperty] private AttentionType _notificationType;

    [ObservableProperty] private int _modCount;

    [ObservableProperty] private string _modCountString = string.Empty;

    [ObservableProperty] private bool _hasMods;
    [ObservableProperty] private bool _hasEnabledMods;

    public ObservableCollection<CharacterModItem> Mods { get; } = new();

    public CharacterGridItemModel(IModdableObject character)
    {
        Character = character;
        ImageUri = character.ImageUri ?? ModModel.PlaceholderImagePath;
    }

    public void SetMods(IEnumerable<CharacterModItem> mods)
    {
        Mods.Clear();
        var enabledMods = 0;
        foreach (var mod in mods)
        {
            Mods.Add(mod);
            if (mod.IsEnabled)
                enabledMods++;
        }

        ModCount = Mods.Count;
        HasMods = ModCount > 0;
        HasEnabledMods = enabledMods > 0;

        if (HasEnabledMods)
            ModCountString = enabledMods == ModCount ? enabledMods.ToString() : $"{enabledMods} / {ModCount}";
        else
            ModCountString = ModCount.ToString();
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


    public override string ToString()
    {
        return Character.DisplayName;
    }
}

public record CharacterModItem(string Name, bool IsEnabled, DateTime DateAdded);