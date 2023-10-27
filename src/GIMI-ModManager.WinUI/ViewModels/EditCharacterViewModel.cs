using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models.ViewModels;
using GIMI_ModManager.WinUI.Services;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class EditCharacterViewModel : ObservableRecipient, INavigationAware
{
    private readonly IGameService _gameService;
    private readonly ISkinManagerService _skinManagerService;
    private ICharacter _character = null!;
    private readonly ILogger _logger;

    private readonly ImageHandlerService _imageHandlerService;

    [ObservableProperty] private CharacterVM _characterVm = null!;
    [ObservableProperty] private Uri _modFolderUri = null!;
    [ObservableProperty] private string _modFolderString = null!;
    [ObservableProperty] private int _modsCount = 0;

    [ObservableProperty] private string _keyToAddInput = null!;


    public EditCharacterViewModel(IGameService gameService, ILogger logger, ISkinManagerService skinManagerService,
        ImageHandlerService imageHandlerService)
    {
        _gameService = gameService;
        _logger = logger;
        _skinManagerService = skinManagerService;
        _imageHandlerService = imageHandlerService;
    }


    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not string internalName)
        {
            _logger.Error($"Invalid parameter type, {parameter}");
            internalName = _gameService.GetCharacters().First().InternalName;
        }


        var character = _gameService.GetCharacterByIdentifier(internalName);

        if (character is null)
        {
            _logger.Error($"Invalid character identifier, {internalName}");
            character = _gameService.GetCharacters().First();
        }

        _character = character;
        CharacterVm = CharacterVM.FromCharacter(_character);
        var modList = _skinManagerService.GetCharacterModList(_character);
        ModFolderUri = new Uri(modList.AbsModsFolderPath);
        ModFolderString = ModFolderUri.LocalPath;
        ModsCount = modList.Mods.Count;
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private void AddKey()
    {
        KeyToAddInput = KeyToAddInput.Trim();

        if (string.IsNullOrWhiteSpace(KeyToAddInput))
            return;

        if (CharacterVm.Keys.Any(key => key.Equals(KeyToAddInput, StringComparison.CurrentCultureIgnoreCase)))
            return;

        CharacterVm.Keys.Add(KeyToAddInput.ToLower());
        KeyToAddInput = string.Empty;
    }

    [RelayCommand]
    private void RemoveKey(string key)
    {
        CharacterVm.Keys.Remove(key);
    }

    [RelayCommand]
    private async Task PickImage()
    {
        var image = await _imageHandlerService.PickImageAsync();

        if (image is null)
            return;

        CharacterVm.ImageUri = new Uri(image.Path);
    }
}