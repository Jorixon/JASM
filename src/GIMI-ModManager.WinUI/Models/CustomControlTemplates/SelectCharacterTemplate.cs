using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.WinUI.Models.ViewModels;

namespace GIMI_ModManager.WinUI.Models.CustomControlTemplates;

public partial class SelectCharacterTemplate : ObservableObject
{
    [ObservableProperty] private string _imagePath = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] bool _isSelected;

    public SelectCharacterTemplate(SkinVM skinVm)
    {
        ImagePath = skinVm.ImageUri;
        DisplayName = skinVm.DisplayName;
    }

    public SelectCharacterTemplate()
    {
    }
}