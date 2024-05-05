using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Helpers;
using Microsoft.UI.Xaml.Media.Animation;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;

public partial class CharacterGalleryViewModel
{
    private bool CanNavigateToModObject(SelectableModdableObjectVm? selectableModdableObject)
    {
        return selectableModdableObject is not null && !IsNavigating && !IsBusy && !selectableModdableObject.IsSelected;
    }

    [RelayCommand(CanExecute = nameof(CanNavigateToModObject))]
    private Task NavigateToModObject(SelectableModdableObjectVm selectableModdableObject)
    {
        IsNavigating = true;

        ModdableObjectVms.ForEach(m => m.IsSelected = false);
        selectableModdableObject.IsSelected = true;


        _navigationService.NavigateTo(typeof(CharacterGalleryViewModel).FullName!,
            selectableModdableObject.ModdableObject, transitionInfo: new SuppressNavigationTransitionInfo());

        IsNavigating = false;
        return Task.CompletedTask;
    }
}