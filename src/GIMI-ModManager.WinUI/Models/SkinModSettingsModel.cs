using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Entities;

namespace GIMI_ModManager.WinUI.Models;

public partial class SkinModSettingsModel : ObservableObject
{
    [ObservableProperty] private string? _customName;

    [ObservableProperty] private string? _author;
    [ObservableProperty] private string? _version;
    [ObservableProperty] private string? _modUrl;
    [ObservableProperty] private string? _imageUri = " ";
    [ObservableProperty] private string? _relativeImagePath;

    public static SkinModSettingsModel FromMod(SkinModSettings mod)
    {
        return new SkinModSettingsModel
        {
            CustomName = mod.CustomName,
            Author = mod.Author,
            Version = mod.Version,
            ModUrl = mod.ModUrl?.ToString(),
            ImageUri = mod.ImageUri?.ToString() ?? " ",
            RelativeImagePath = mod.RelativeImagePath
        };
    }
}