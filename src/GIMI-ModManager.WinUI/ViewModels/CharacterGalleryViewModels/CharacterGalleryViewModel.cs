using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;

public partial class CharacterGalleryViewModel(
    IGameService gameService,
    INavigationService navigationService,
    ISkinManagerService skinManagerService,
    ElevatorService elevatorService)
    : ObservableRecipient, INavigationAware
{
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly ElevatorService _elevatorService = elevatorService;
    private readonly INavigationService _navigationService = navigationService;
    private readonly IGameService _gameService = gameService;
    private ICategory? _category;
    private IModdableObject? _moddableObject;
    private ICharacterModList? _modList;

    private readonly List<ModModel> _backendMods = new();
    public ObservableCollection<ModGridItemVm> Mods { get; } = new();

    public string Name => _moddableObject?.DisplayName ?? "Loading...";
    public Uri ImagePath => _moddableObject?.ImageUri ?? ModModel.PlaceholderImagePath;

    [ObservableProperty] private int _gridItemWidth = 500;

    [ObservableProperty] private int _gridItemHeight = 300;

    [ObservableProperty] private bool _isBusy;


    [MemberNotNullWhen(false, nameof(_category), nameof(_moddableObject), nameof(_modList))]
    private bool IsNavigating()
    {
        return _isNavigating;
    }

    private bool _isNavigating = true;

    public async void OnNavigatedTo(object parameter)
    {
        var internalName = new InternalName("_");


        if (parameter is CharacterGridItemModel characterGridItemModel)
        {
            internalName = characterGridItemModel.Character.InternalName;
        }
        else if (parameter is INameable iInternalName)
        {
            internalName = iInternalName.InternalName;
        }
        else if (parameter is string internalNameString)
        {
            internalName = new InternalName(internalNameString);
        }
        else if (parameter is InternalName internalName1)
        {
            internalName = internalName1;
        }


        var moddableObject = _gameService.GetModdableObjectByIdentifier(internalName);

        if (moddableObject is null)
        {
            ErrorNavigateBack();
            return;
        }

        _category = moddableObject.ModCategory;
        _moddableObject = moddableObject;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ImagePath));


        _modList = _skinManagerService.GetCharacterModList(_moddableObject);

        foreach (var characterSkinEntry in _modList.Mods)
        {
            var modModel = ModModel.FromMod(characterSkinEntry);

            var modSettings = await characterSkinEntry.Mod.Settings.TryReadSettingsAsync(false);

            if (modSettings is not null)
                modModel.WithModSettings(modSettings);

            if (characterSkinEntry.Mod.KeySwaps is not null)
            {
                var keySwaps = await characterSkinEntry.Mod.KeySwaps.ReadKeySwapConfiguration();

                modModel.SetKeySwaps(keySwaps);
            }

            _backendMods.Add(modModel);
        }

        foreach (var mod in _backendMods)
        {
            Mods.Add(new ModGridItemVm(mod, ToggleModCommand));
        }


        _isNavigating = false;
    }


    public void OnNavigatedFrom()
    {
    }


    private void ErrorNavigateBack()
    {
        Task.Run(async () =>
        {
            await Task.Delay(500);
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (_navigationService.CanGoBack)
                    _navigationService.GoBack();
                else
                    _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!);
            });
        });
    }
}