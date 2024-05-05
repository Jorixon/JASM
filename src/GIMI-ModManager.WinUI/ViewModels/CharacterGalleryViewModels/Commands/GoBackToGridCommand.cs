using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.WinUI.Views;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;

public partial class CharacterGalleryViewModel
{
    [RelayCommand]
    private void GoBackToGrid()
    {
        var gridLastStack = _navigationService.GetBackStackItems().LastOrDefault();

        if (gridLastStack is not null && gridLastStack.SourcePageType == typeof(CharactersPage))
        {
            _navigationService.GoBack();
            return;
        }

        _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!, _category);
    }
}