using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.WinUI.Models.ViewModels;

public partial class CharacterVM : ObservableObject, IEquatable<CharacterVM>
{
    public static readonly Uri PlaceholderImagePath =
        new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\ModPanePlaceholder.webp"));

    public string InternalName { get; private set; } = string.Empty;


    [ObservableProperty] private string _displayName = string.Empty;

    [ObservableProperty] private ObservableCollection<string> _keys = new();
    public DateTime ReleaseDate { get; set; } = DateTime.MinValue;
    public Uri ImageUri { get; set; } = PlaceholderImagePath;
    public int Rarity { get; set; } = -1;
    public string Element { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public string[] Region { get; set; } = Array.Empty<string>();

    public ObservableCollection<SkinVM> InGameSkins { get; set; } = new();


    public static CharacterVM FromCharacter(ICharacter character)
    {
        var model = new CharacterVM
        {
            InternalName = character.InternalName,
            DisplayName = character.DisplayName,
            ReleaseDate = character.ReleaseDate,
            ImageUri = character.ImageUri ?? PlaceholderImagePath,
            Rarity = character.Rarity,
            Element = character.Element.DisplayName,
            Class = character.Class.DisplayName,
            Region = character.Regions.Select(x => x.DisplayName).ToArray(),
            InGameSkins = new ObservableCollection<SkinVM>(character.Skins.Select(SkinVM.FromSkin))
        };

        character.Keys.ForEach(key => model.Keys.Add(key));

        return model;
    }

    public CharacterVM()
    {
    }

    public static IEnumerable<CharacterVM> FromCharacters(IEnumerable<ICharacter> character)
    {
        return character.Select(FromCharacter);
    }

    public bool Equals(CharacterVM? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(InternalName, other.InternalName, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is CharacterVM other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(InternalName);
    }

    public static bool operator ==(CharacterVM? left, CharacterVM? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(CharacterVM? left, CharacterVM? right)
    {
        return !Equals(left, right);
    }
}

public partial class SkinVM : ObservableObject
{
    public static readonly Uri PlaceholderImagePath =
        new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\ModPanePlaceholder.webp"));

    public string InternalName { get; init; } = string.Empty;

    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _modFilesPrefix = string.Empty;
    [ObservableProperty] private Uri _imageUri = PlaceholderImagePath;
    public bool IsDefault { get; private set; }

    public static SkinVM FromSkin(ICharacterSkin skin)
    {
        var imageUri = skin.ImageUri ?? skin.Character.ImageUri ?? PlaceholderImagePath;


        return new SkinVM
        {
            DisplayName = skin.DisplayName,
            ModFilesPrefix = skin.ModFilesName,
            InternalName = skin.InternalName,
            ImageUri = imageUri,
            IsDefault = skin.IsDefault
        };
    }

    public static IEnumerable<SkinVM> FromSkin(IEnumerable<ICharacterSkin> skins)
    {
        return skins.Select(FromSkin);
    }

    public SkinVM()
    {
    }
}