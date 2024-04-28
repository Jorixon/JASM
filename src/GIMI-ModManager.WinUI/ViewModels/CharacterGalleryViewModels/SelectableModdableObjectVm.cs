using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;

public partial class SelectableModdableObjectVm : ObservableObject
{
    private IModdableObject _modObject;

    [ObservableProperty] private bool _isSelected;

    public INameable ModdableObject => _modObject;

    public string Name => _modObject.DisplayName;

    public Uri ImagePath => _modObject.ImageUri ?? ImageHandlerService.StaticPlaceholderImageUri;

    public SelectableModdableObjectVm(IModdableObject modObject, IAsyncRelayCommand navigateToModPageCommand)
    {
        _modObject = modObject;
        NavigateToModPageCommand = navigateToModPageCommand;
    }


    public IAsyncRelayCommand NavigateToModPageCommand { get; }
}