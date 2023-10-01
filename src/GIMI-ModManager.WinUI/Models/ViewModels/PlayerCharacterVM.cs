using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Entities.Genshin;

namespace GIMI_ModManager.WinUI.Models.ViewModels;

public class PlayerCharacterVM
{
    public int Id { get; set; } = -1;
    public string DisplayName { get; set; } = string.Empty;
    public string[] Keys { get; set; } = Array.Empty<string>();
    public DateTime ReleaseDate { get; set; } = DateTime.MinValue;
    public string? ImageUri { get; set; }
    public int Rarity { get; set; } = -1;
    public Elements Element { get; set; } 
    public string Weapon { get; set; } = string.Empty;
    public string[] Region { get; set; } = Array.Empty<string>();

    public ICollection<SkinVM> InGameSkins { get; set; } = Array.Empty<SkinVM>();


    public static PlayerCharacterVM FromGenshinCharacter(IGenshinCharacter character)
    {
        return new PlayerCharacterVM
        {
            Id = character.Id,
            DisplayName = character.DisplayName,
            Keys = character.Keys,
            ReleaseDate = character.ReleaseDate,
            ImageUri = character.ImageUri,
            Rarity = character.Rarity,
            Element = character.Element,
            Weapon = character.Weapon,
            Region = character.Region,
            InGameSkins = SkinVM.FromSkin(character.InGameSkins)
        };
    }
}

public partial class SkinVM : ObservableObject
{
    public static readonly Uri PlaceholderImagePath =
        new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\ModPanePlaceholder.webp"));

    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _skinSuffix = string.Empty;
    [ObservableProperty] private string _imageUri = PlaceholderImagePath.ToString();
    public bool DefaultSkin { get; private set; }

    public static SkinVM FromSkin(ISubSkin skin)
    {
        var imageUri = string.IsNullOrWhiteSpace(skin.ImageUri)
            ? skin.Character.ImageUri
            : skin.ImageUri;
        imageUri = string.IsNullOrWhiteSpace(imageUri)
            ? PlaceholderImagePath.ToString()
            : imageUri;

        return new SkinVM
        {
            DisplayName = skin.DisplayName,
            SkinSuffix = skin.SkinSuffix,
            Name = skin.Name,
            ImageUri = imageUri,
            DefaultSkin = skin.DefaultSkin
        };
    }

    public static SkinVM[] FromSkin(IEnumerable<ISubSkin> skins)
    {
        return skins.Select(FromSkin).ToArray();
    }
}