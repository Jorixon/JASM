using CommunityToolkit.Mvvm.ComponentModel;

namespace GIMI_ModManager.WinUI.Models.CustomControlTemplates;

public partial class SelectCharacterTemplate : ObservableObject
{
    [ObservableProperty] private string _imagePath = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] bool _isSelected;
}