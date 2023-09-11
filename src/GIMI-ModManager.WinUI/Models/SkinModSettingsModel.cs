using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities;

namespace GIMI_ModManager.WinUI.Models;

public partial class SkinModSettingsModel : ObservableObject
{
    [ObservableProperty] private string? _customName;

    [ObservableProperty] private string? _author;
    [ObservableProperty] private string? _version;
    [ObservableProperty] private string? _modUrl;
    [ObservableProperty] private string? _imageUri = " "; // If this is null or empty the app will crash...

    public static SkinModSettingsModel FromMod(SkinModSettings mod)
    {
        return new SkinModSettingsModel
        {
            CustomName = mod.CustomName,
            Author = mod.Author,
            Version = mod.Version,
            ModUrl = mod.ModUrl,
            ImageUri = string.IsNullOrEmpty(mod.ImagePath) ? " " : mod.ImagePath,
        };
    }


    protected bool Equals(SkinModSettingsModel other)
    {
        return CustomName == other.CustomName && Author == other.Author && Version == other.Version &&
               ModUrl == other.ModUrl && ImageUri == other.ImageUri;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((SkinModSettingsModel)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CustomName, Author, Version, ModUrl, ImageUri);
    }
}