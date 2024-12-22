using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.GamesService.Requests;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class EditCharacterViewModel : ObservableRecipient, INavigationAware
{
    private readonly IGameService _gameService;
    private readonly ISkinManagerService _skinManagerService;
    private readonly ILogger _logger;
    private readonly NotificationManager _notificationManager;
    private readonly ImageHandlerService _imageHandlerService;
    private readonly INavigationService _navigationService;

    private ICharacter _character = null!;


    [ObservableProperty] private Uri _modFolderUri = null!;
    [ObservableProperty] private string _modFolderString = "";
    [ObservableProperty] private int _modsCount;
    [ObservableProperty] private string _keyToAddInput = string.Empty;

    public CharacterStatus CharacterStatus { get; } = new();

    public EditCharacterForm Form { get; } = new();


    public EditCharacterViewModel(IGameService gameService, ILogger logger, ISkinManagerService skinManagerService,
        ImageHandlerService imageHandlerService, NotificationManager notificationManager, INavigationService navigationService)
    {
        _gameService = gameService;
        _logger = logger.ForContext<EditCharacterViewModel>();
        _skinManagerService = skinManagerService;
        _imageHandlerService = imageHandlerService;
        _notificationManager = notificationManager;
        _navigationService = navigationService;
        Form.PropertyChanged += NotifyAllCommands;
    }


    public void OnNavigatedTo(object parameter)
    {
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
        CharacterStatus.SetIsCustomCharacter(_character.IsCustomModObject);

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

        Form.Initialize(character);
        NotifyAllCommands();
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private void AddKey()
    {
        KeyToAddInput = KeyToAddInput.Trim().ToLowerInvariant();
        var existingKeys = Form.Keys.Items.ToList();

        if (string.IsNullOrWhiteSpace(KeyToAddInput))
            return;

        if (existingKeys.Contains(KeyToAddInput, StringComparer.OrdinalIgnoreCase))
            return;

        Form.Keys.Items.Add(KeyToAddInput);
        KeyToAddInput = string.Empty;
        NotifyAllCommands();
    }

    [RelayCommand]
    private void RemoveKey(string key) => Form.Keys.Items.Remove(key);

    [RelayCommand]
    private async Task PickImage()
    {
        var image = await _imageHandlerService.PickImageAsync(copyToTmpFolder: false);

        if (image is null || !File.Exists(image.Path))
            return;

        Form.Image.Value = new Uri(image.Path);
    }

    private bool CanDisableCharacter() => !_character.IsCustomModObject && CharacterStatus.IsEnabled && !AnyChanges();

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
        await Task.Run(() => _skinManagerService.DisableModListAsync(_character, deleteFolderCheckBox.IsChecked ?? false));
        ResetState();
    }


    private bool CanDeleteCustomCharacter() => _character.IsCustomModObject && CharacterStatus.IsEnabled && !Form.AnyFieldDirty;

    [RelayCommand(CanExecute = nameof(CanDeleteCustomCharacter))]
    private async Task DeleteCustomCharacterAsync()
    {
        var deleteFolderCheckBox = new CheckBox()
        {
            Content = "Delete Custom Character folder and its contents/Mods?\n" +
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
                        "Are you sure you want to delete this custom character? " +
                        "This will remove the character, and JASM will no longer recognize the character. " +
                        "This will be executed immediately on pressing yes",
                    TextWrapping = TextWrapping.WrapWholeWords,
                    Margin = new Thickness(0, 0, 0, 8)
                },
                deleteFolderCheckBox
            }
        };


        var disableDialog = new ContentDialog
        {
            Title = "Delete Custom Character",
            Content = dialogContent,
            PrimaryButtonText = "Yes, delete this custom character",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        var result = await disableDialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;


        _logger.Information($"Deleting custom character {_character.InternalName}");
        await _gameService.DeleteCustomCharacterAsync(_character.InternalName);
        await Task.Run(() => _skinManagerService.DisableModListAsync(_character, deleteFolderCheckBox.IsChecked ?? false));
        ResetState();
    }

    private bool CanEnableCharacter() => !_character.IsCustomModObject && CharacterStatus.IsDisabled && !AnyChanges();

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

    [RelayCommand]
    private void GoToCharacter()
    {
        _navigationService.NavigateToCharacterDetails(_character.InternalName);
    }

    private bool CanSaveChanges() => Form.AnyFieldDirty && Form.IsValid;

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

        try
        {
            await InternalSaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to save changes to character");
            _notificationManager.ShowNotification("Failed to save changes to character", e.Message, null);
            return;
        }


        ResetState();

        return;

        async Task InternalSaveChangesAsync()
        {
            if (_character.IsCustomModObject)
            {
                var editRequest = new EditCustomCharacterRequest();

                if (Form.DisplayName.IsDirty)
                {
                    Debug.Assert(Form.DisplayName.IsValid);
                    editRequest.DisplayName = NewValue<string>.Set(Form.DisplayName.Value);
                }

                // TODO: Add more properties
#if RELEASE
           wdawd
#endif

                await _gameService.EditCustomCharacterAsync(_character.InternalName, editRequest);
            }
            else
            {
                if (Form.DisplayName.IsDirty)
                {
                    Debug.Assert(Form.DisplayName.IsValid);
                    await _gameService.SetCharacterDisplayNameAsync(_character, Form.DisplayName.Value.Trim());
                }

                if (Form.Image.IsDirty && Form.Image.Value.LocalPath != _imageHandlerService.PlaceholderImagePath)
                {
                    Debug.Assert(Form.Image.IsValid);
                    await _gameService.SetCharacterImageAsync(_character, Form.Image.Value);
                }
            }
        }
    }


    private bool CanRevertChanges() => AnyChanges();

    [RelayCommand(CanExecute = nameof(CanRevertChanges))]
    private void RevertChanges()
    {
        _logger.Debug($"Reverting draft changes to character {_character.InternalName}");
        ResetState();
    }

    private bool CanResetCharacterToDefault() => !_character.IsCustomModObject;

    [RelayCommand(CanExecute = nameof(CanResetCharacterToDefault))]
    private async Task ResetCharacterToDefaultAsync()
    {
        _logger.Information($"Resetting character {_character.InternalName} to default");
        await _gameService.ResetOverrideForCharacterAsync(_character);
        ResetState();
    }

    private void ResetState()
    {
        OnNavigatedTo(_character.InternalName.Id);
        NotifyAllCommands();
    }

    private bool AnyChanges() => Form.AnyFieldDirty;

    private void NotifyAllCommands(object? sender = null, PropertyChangedEventArgs? propertyChangedEventArgs = null)
    {
        DummySaveCommand.NotifyCanExecuteChanged();
        DisableCharacterCommand.NotifyCanExecuteChanged();
        EnableCharacterCommand.NotifyCanExecuteChanged();
        SaveChangesCommand.NotifyCanExecuteChanged();
        RevertChangesCommand.NotifyCanExecuteChanged();
        ResetCharacterToDefaultCommand.NotifyCanExecuteChanged();
        DeleteCustomCharacterCommand.NotifyCanExecuteChanged();
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

public class CharacterStatus : ObservableObject
{
    private bool _isCustomCharacter;
    public bool IsCustomCharacter => _isCustomCharacter;
    private bool _isEnabled;

    public bool IsEnabled => _isEnabled;

    private bool _isDisabled;

    public bool IsDisabled => _isDisabled;

    public bool IsEnabledAndNotCustomCharacter => IsEnabled && !IsCustomCharacter;

    public bool IsDisabledAndNotCustomCharacter => IsDisabled && !IsCustomCharacter;


    public void SetIsCustomCharacter(bool isCustomCharacter)
    {
        SetProperty(ref _isCustomCharacter, isCustomCharacter, nameof(IsCustomCharacter));
        OnPropertyChanged(nameof(IsDisabledAndNotCustomCharacter));
        OnPropertyChanged(nameof(IsEnabledAndNotCustomCharacter));
    }

    public void SetEnabled(bool enabled)
    {
        SetProperty(ref _isEnabled, enabled, nameof(IsEnabled));
        SetProperty(ref _isDisabled, !enabled, nameof(IsDisabled));
        OnPropertyChanged(nameof(IsDisabledAndNotCustomCharacter));
        OnPropertyChanged(nameof(IsEnabledAndNotCustomCharacter));
    }
}