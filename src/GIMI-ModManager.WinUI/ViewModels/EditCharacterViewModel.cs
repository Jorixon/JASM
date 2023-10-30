using System.Collections.ObjectModel;
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
using Newtonsoft.Json;
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
    [ObservableProperty] private string _modFolderString = "";
    [ObservableProperty] private int _modsCount;
    [ObservableProperty] private string _keyToAddInput = string.Empty;

    [ObservableProperty] private CharacterStatus _characterStatus = new();

    public ObservableCollection<ValidationErrors> ValidationErrors { get; } = new();


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


        var character = _gameService.GetCharacterByIdentifier(internalName, true);

        if (character is null)
        {
            _logger.Error($"Invalid character identifier, {internalName}");
            character = _gameService.GetCharacters().First();
        }

        _character = character;
        CharacterVm = CharacterVM.FromCharacter(_character);

        if (!_gameService.GetDisabledCharacters().Contains(character))
        {
            CharacterStatus.SetEnabled(true);
            var modList = _skinManagerService.GetCharacterModList(_character);
            ModFolderUri = new Uri(modList.AbsModsFolderPath);
            ModFolderString = ModFolderUri.LocalPath;
            ModsCount = modList.Mods.Count;
        }
        else
        {
            CharacterStatus.SetEnabled(false);
            ModFolderUri = new Uri(_skinManagerService.ActiveModsFolderPath);
            ModFolderString = ModFolderUri.LocalPath;
            ModsCount = 0;
        }


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
        var image = await _imageHandlerService.PickImageAsync(false);

        if (image is null)
            return;

        CharacterVm.ImageUri = new Uri(image.Path);
        CheckForChanges();
    }

    private bool CanDisableCharacter() => CharacterStatus.IsEnabled && !AnyChanges();

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

        await _gameService.DisableCharacterAsync(_character);
        await _skinManagerService.DisableModListAsync(_character, deleteFolderCheckBox.IsChecked ?? false);
        ResetState();
    }

    private bool CanEnableCharacter() => CharacterStatus.IsDisabled && !AnyChanges();

    [RelayCommand(CanExecute = nameof(CanEnableCharacter))]
    private async Task EnableCharacter()
    {
        var dialogContent = new TextBlock()
        {
            Text =
                "Are you sure you want to enable this character? " +
                "This will be executed immediately on pressing yes",
            TextWrapping = TextWrapping.WrapWholeWords,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var enableDialog = new ContentDialog
        {
            Title = "Enable Character",
            Content = dialogContent,
            PrimaryButtonText = "Yes, enable this character",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        var result = await enableDialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        _logger.Information($"Enabling character {_character.InternalName}");

        await _gameService.EnableCharacterAsync(_character);
        await _skinManagerService.EnableModListAsync(_character);
        ResetState();
    }

    private bool CanSaveChanges()
    {
        ValidationErrors.Clear();

        if (!AnyChanges())
            return false;

        var errors = new List<ValidationErrors>();

        var characters = _gameService.GetCharacters();
        if (!characters.Remove(_character))
        {
            _logger.Error($"Character {_character.InternalName} not found in game service");
            return false;
        }

        CharacterVm.DisplayName = CharacterVm.DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(CharacterVm.DisplayName))
        {
            errors.Add(new ValidationErrors
            {
                InputField = "Field: DisplayName",
                ErrorMessage = "Display name cannot be empty"
            });
        }


        // Check for duplicate display names
        if (characters.FirstOrDefault(c =>
                c.DisplayName.Equals(CharacterVm.DisplayName, StringComparison.OrdinalIgnoreCase)) is
            { } duplicateDisplayNameCharacter)
        {
            errors.Add(new ValidationErrors
            {
                InputField = "Field: DisplayName",
                ErrorMessage =
                    $"Display name already in use by Id: {duplicateDisplayNameCharacter.InternalName} | DisplayName: {duplicateDisplayNameCharacter.DisplayName}"
            });
        }

        // Check for duplicate keys
        // TODO: Implement key editing
        //if (characters.FirstOrDefault(c => c.Keys.Any(key => CharacterVm.Keys.Contains(key))) is
        //    { } duplicateKeyCharacter)
        //{
        //    errors.Add(new ValidationErrors
        //    {
        //        InputField = "Field: Keys",
        //        ErrorMessage =
        //            $"Key already in use by Id: {duplicateKeyCharacter.InternalName} | DisplayName: {duplicateKeyCharacter.DisplayName}"
        //    });
        //}

        if (errors.Count > 0)
        {
            errors.ForEach(err => ValidationErrors.Add(err));
            return false;
        }


        return true;
    }

    [RelayCommand(CanExecute = nameof(CanSaveChanges))]
    private void DummySave()
    {
    }

    [RelayCommand(CanExecute = nameof(CanSaveChanges))]
    private async Task SaveChangesAsync()
    {
        _logger.Debug($"Saving changes to character {_character.InternalName}");

        if (!AnyChanges())
            return;

        if (_character.DisplayName != CharacterVm.DisplayName)
            await _gameService.SetCharacterDisplayNameAsync(_character, CharacterVm.DisplayName);

        if (CharacterVm.ImageUri.LocalPath != _imageHandlerService.PlaceholderImagePath &&
            _character.ImageUri != CharacterVm.ImageUri)
            await _gameService.SetCharacterImageAsync(_character, CharacterVm.ImageUri);

        ResetState();
    }

    private bool CanRevertChanges() => AnyChanges();

    [RelayCommand(CanExecute = nameof(CanRevertChanges))]
    private void RevertChanges()
    {
        _logger.Debug($"Reverting draft changes to character {_character.InternalName}");
        ResetState();
    }

    private bool CanResetCharacterToDefault() => true;

    [RelayCommand(CanExecute = nameof(CanResetCharacterToDefault))]
    private async Task ResetCharacterToDefaultAsync()
    {
        _logger.Information($"Resetting character {_character.InternalName} to default");
        await _gameService.ResetOverrideForCharacterAsync(_character);
        ResetState();
    }

    private void ResetState()
    {
        CharacterVm.PropertyChanged -= CheckForChanges;
        OnNavigatedTo(_character.InternalName.Id);
        CheckForChanges();
    }

    private bool AnyChanges()
    {
        // TODO: Implement key editing
        if (CharacterVm.Keys.Any(key => !_character.Keys.Contains(key)))
            return false; // Remove this once key editing has been implemented

        if (CharacterVm.DisplayName != _character.DisplayName)
            return true;

        if (CharacterVm.ImageUri.LocalPath != _imageHandlerService.PlaceholderImagePath &&
            CharacterVm.ImageUri != _character.ImageUri)
            return true;

        //if (CharacterVm.Keys.Count != _character.Keys.Count)
        //    return true;

        //if (CharacterVm.Keys.Any(key => !_character.Keys.Contains(key)))
        //    return true;


        return false;
    }

    private void CheckForChanges(object? sender = null, PropertyChangedEventArgs? propertyChangedEventArgs = null)
    {
        DummySaveCommand.NotifyCanExecuteChanged();
        DisableCharacterCommand.NotifyCanExecuteChanged();
        EnableCharacterCommand.NotifyCanExecuteChanged();
        SaveChangesCommand.NotifyCanExecuteChanged();
        RevertChangesCommand.NotifyCanExecuteChanged();
    }


    [RelayCommand]
    private async Task ShowCharacterModelAsync()
    {
        var json = JsonConvert.SerializeObject(_character, Formatting.Indented);

        var content = new ScrollViewer()
        {
            Content = new TextBlock()
            {
                Text = json,
                TextWrapping = TextWrapping.WrapWholeWords,
                IsTextSelectionEnabled = true,
                Margin = new Thickness(4)
            }
        };

        var characterModelDialog = new ContentDialog
        {
            Title = "Character Model",
            Content = content,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            FullSizeDesired = true
        };

        await characterModelDialog.ShowAsync();
    }
}

public class ValidationErrors
{
    public string InputField { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public record ModModelProperty(string Key, string Value);

public class CharacterStatus : ObservableObject
{
    private bool _isEnabled;

    public bool IsEnabled => _isEnabled;

    private bool _isDisabled;

    public bool IsDisabled => _isDisabled;


    public void SetEnabled(bool enabled)
    {
        SetProperty(ref _isEnabled, enabled, nameof(IsEnabled));
        SetProperty(ref _isDisabled, !enabled, nameof(IsDisabled));
    }
}