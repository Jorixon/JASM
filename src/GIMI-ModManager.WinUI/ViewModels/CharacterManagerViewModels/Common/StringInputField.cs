using CommunityToolkit.Mvvm.ComponentModel;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

public sealed partial class StringInputField(string value) : InputField<string>(value)
{
    [ObservableProperty] private string _placeHolderText = string.Empty;
}