using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models.ViewModels;
using GIMI_ModManager.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    [ObservableProperty] private int _modsCount;

    [ObservableProperty] private string _keyToAddInput = string.Empty;


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
        CharacterVm = null!;
        if (parameter is not string internalName)
        {
            _logger.Error($"Invalid parameter type, {parameter}");
            internalName = _gameService.GetCharacters().First().InternalName;
        }

        if (parameter is InternalName id)
            internalName = id;


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

        CharacterVm.PropertyChanged += CheckForChanges;
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
        CheckForChanges();
    }

    [RelayCommand]
    private void RemoveKey(string key)
    {
        CharacterVm.Keys.Remove(key);
        CheckForChanges();
    }

    [RelayCommand]
    private async Task PickImage()
    {
        var image = await _imageHandlerService.PickImageAsync();

        if (image is null)
            return;

        CharacterVm.ImageUri = new Uri(image.Path);
        CheckForChanges();
    }

    private bool CanDisableCharacter() => !AnyChanges();

    [RelayCommand(CanExecute = nameof(CanDisableCharacter))]
    private async Task DisableCharacter()
    {
        var deleteFolderCheckBox = new CheckBox()
        {
            Content = "Delete Character folder and its contents/Mods?\n" +
                      "Files will be permanently deleted!",
            IsChecked = false
        };

        var dialogContent = new StackPanel()
        {
            Children =
            {
                new TextBlock()
                {
                    Text =
                        "Are you sure you want to disable this character? " +
                        "This will not remove the character, but JASM will no longer recognize the character. " +
                        "Character can be reactivated later. " +
                        "This will be executed immediately on pressing yes",
                    TextWrapping = TextWrapping.WrapWholeWords,
                    Margin = new Thickness(0, 0, 0, 8)
                },
                deleteFolderCheckBox
            }
        };


        var disableDialog = new ContentDialog
        {
            Title = "Disable Character",
            Content = dialogContent,
            PrimaryButtonText = "Yes, disable this character",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        var result = await disableDialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        _logger.Information(
            $"Disabling character {_character.InternalName} with deleteFolder={deleteFolderCheckBox.IsChecked}");
    }

    private bool CanSaveChanges() => AnyChanges();

    [RelayCommand(CanExecute = nameof(CanSaveChanges))]
    private Task SaveChangesAsync()
    {
        _logger.Debug($"Saving changes to character {_character.InternalName}");
        return Task.CompletedTask;
    }

    private bool CanRevertChanges() => AnyChanges();

    [RelayCommand(CanExecute = nameof(CanRevertChanges))]
    private void RevertChanges()
    {
        _logger.Debug($"Reverting draft changes to character {_character.InternalName}");
        CharacterVm.PropertyChanged -= CheckForChanges;
        OnNavigatedTo(_character.InternalName.Id);
        CheckForChanges();
    }

    private bool AnyChanges()
    {
        if (CharacterVm.DisplayName != _character.DisplayName)
            return true;

        if (CharacterVm.ImageUri != _character.ImageUri)
            return true;

        if (CharacterVm.Keys.Count != _character.Keys.Count)
            return true;

        if (CharacterVm.Keys.Any(key => !_character.Keys.Contains(key)))
            return true;

        return false;
    }

    private void CheckForChanges(object? sender = null, PropertyChangedEventArgs? propertyChangedEventArgs = null)
    {
        DisableCharacterCommand.NotifyCanExecuteChanged();
        SaveChangesCommand.NotifyCanExecuteChanged();
        RevertChangesCommand.NotifyCanExecuteChanged();
    }
}