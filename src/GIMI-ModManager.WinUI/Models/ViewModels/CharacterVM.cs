using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.WinUI.Models.ViewModels;

public partial class CharacterVM : ObservableObject
{
    public static readonly Uri PlaceholderImagePath =
        new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\ModPanePlaceholder.webp"));

    public string InternalName { get; private set; } = null!;
    [ObservableProperty] private string _displayName = string.Empty;

    [ObservableProperty] private ObservableCollection<string> _keys = new();
    public DateTime ReleaseDate { get; set; } = DateTime.MinValue;
    public Uri ImageUri { get; set; } = null!;
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
            InGameSkins = new ObservableCollection<SkinVM>(character.AdditionalSkins.Select(SkinVM.FromSkin))
        };

        character.Keys.ForEach(key => model.Keys.Add(key));
    }
}

public partial class SkinVM : ObservableObject
{
    public static readonly Uri PlaceholderImagePath =
        new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\ModPanePlaceholder.webp"));

    public string InternalName { get; private set; } = null!;

    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _modFilesPrefix = string.Empty;
    [ObservableProperty] private string _imageUri = PlaceholderImagePath.ToString();

    public static SkinVM FromSkin(ICharacterSkin skin)
    {
        var imageUri = skin.ImageUri ?? skin.Character.ImageUri ?? PlaceholderImagePath;


        return new SkinVM
        {
            DisplayName = skin.DisplayName,
            ModFilesPrefix = skin.ModFilesName,
            InternalName = skin.InternalName,
            ImageUri = imageUri.ToString()
        };
    }

    public static IEnumerable<SkinVM> FromSkin(IEnumerable<ICharacterSkin> skins)
    {
        return skins.Select(FromSkin);
    }
}