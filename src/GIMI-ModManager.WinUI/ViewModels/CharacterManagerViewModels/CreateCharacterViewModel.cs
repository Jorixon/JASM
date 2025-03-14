﻿using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.GamesService.Requests;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using static GIMI_ModManager.WinUI.Helpers.Extensions;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

public partial class CreateCharacterViewModel : ObservableObject
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGameService _gameService;
    private readonly NotificationManager _notificationManager;
    private readonly ImageHandlerService _imageHandlerService;
    private readonly INavigationService _navigationService;
    private readonly ILogger _logger;

    private readonly List<IModdableObject> _allModObjects;

    public bool IsFinished { get; private set; }

    public CreateCharacterForm Form { get; } = new();
    [ObservableProperty] private string _newKeyNameInput = string.Empty;

    [ObservableProperty] private ElementItemVM _selectedElement;
    public ObservableCollection<ElementItemVM> Elements { get; } = new();

    public CreateCharacterViewModel(ISkinManagerService skinManagerService, IGameService gameService, NotificationManager notificationManager,
        ImageHandlerService imageHandlerService, ILogger logger, INavigationService navigationService)
    {
        _skinManagerService = skinManagerService;
        _gameService = gameService;
        _notificationManager = notificationManager;
        _imageHandlerService = imageHandlerService;
        _navigationService = navigationService;
        _logger = logger.ForContext<CreateCharacterViewModel>();

        _allModObjects = _gameService.GetAllModdableObjects(GetOnly.Both);
        var elements = _gameService.GetElements();

        Form.Initialize(_allModObjects, elements);

        Elements.AddRange(elements.Select(e => new ElementItemVM(e.InternalName, e.DisplayName)));

        SelectedElement = Elements.First(e => e.InternalName.Equals("None", StringComparison.OrdinalIgnoreCase));
        Form.Element.Value = SelectedElement.InternalName;

        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SelectedElement))
                Form.Element.Value = SelectedElement.InternalName;
        };

        Form.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(CreateCharacterForm.IsValid) or nameof(CreateCharacterForm.AnyFieldDirty))
            {
                SaveCharacterCommand.NotifyCanExecuteChanged();
                ExportCharacterCommand.NotifyCanExecuteChanged();
            }
        };
    }


    [RelayCommand]
    private async Task CopyCharacterToClipboardAsync()
    {
        return;
    }

    private bool CanOpenCustomCharacterJsonFile => File.Exists(_gameService.GameServiceSettingsFilePath);

    [RelayCommand(CanExecute = nameof(CanOpenCustomCharacterJsonFile))]
    private async Task OpenCustomCharacterJsonFile()
    {
        if (!CanOpenCustomCharacterJsonFile)
            return;

        var settingsFile = await StorageFile.GetFileFromPathAsync(_gameService.GameServiceSettingsFilePath);

        await Launcher.LaunchFileAsync(settingsFile);
    }

    private bool CanSaveCharacter() => Form is { IsValid: true, AnyFieldDirty: true } && !IsFinished;

    [RelayCommand(CanExecute = nameof(CanSaveCharacter))]
    private async Task SaveCharacterAsync()
    {
        Form.ValidateAllFields();
        if (!Form.IsValid) return;

        var createCharacterRequest = NewCharacterRequest();

        ICharacter character;
        try
        {
            character = await Task.Run(() => _gameService.CreateCharacterAsync(createCharacterRequest));
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to create character");
            _notificationManager.ShowNotification("Failed to create character", e.Message, null);
            return;
        }

        try
        {
            await Task.Run(() => _skinManagerService.EnableModListAsync(character));
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to enable mod list for character");
            _notificationManager.ShowNotification("Character created, but failed to enable mod list for character", e.Message, null);
            return;
        }

        IsFinished = true;
        _navigationService.NavigateTo(typeof(CharacterManagerViewModel).FullName!, character.InternalName);
        _notificationManager.ShowNotification("Character created", $"Character '{character.DisplayName}' was created successfully", null);
    }

    #region ImageCommands

    [RelayCommand]
    private async Task PasteImageAsync()
    {
        try
        {
            var image = await _imageHandlerService.GetImageFromClipboardAsync();
            if (image is not null)
                Form.Image.Value = image;
            else
                _notificationManager.ShowNotification("Failed to paste image", "No image found in clipboard", null);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to paste image");
            _notificationManager.ShowNotification("Failed to paste image", ex.Message, null);
        }
    }

    [RelayCommand]
    private void ClearImage() => Form.Image.Value = ImageHandlerService.StaticPlaceholderImageUri;

    [RelayCommand]
    private async Task SelectImageAsync()
    {
        var image = await _imageHandlerService.PickImageAsync(copyToTmpFolder: false);
        if (image is not null && Uri.TryCreate(image.Path, UriKind.Absolute, out var imagePath))
            Form.Image.Value = imagePath;
    }

    #endregion

    #region Keys

    [RelayCommand]
    private void AddNewKey()
    {
        var newKey = NewKeyNameInput.Trim();
        if (newKey.IsNullOrEmpty())
            return;

        if (Form.Keys.Items.Contains(newKey, StringComparer.OrdinalIgnoreCase))
            return;

        Form.Keys.Items.Add(newKey);
        NewKeyNameInput = string.Empty;
    }

    [RelayCommand]
    private void RemoveKey(string key)
    {
        Form.Keys.Items.Remove(key);
    }

    #endregion

    private bool CanExportCharacter() => Form is { IsValid: true, AnyFieldDirty: true };

    [RelayCommand(CanExecute = nameof(CanExportCharacter))]
    private async Task ExportCharacterAsync()
    {
        Form.ValidateAllFields();
        if (!Form.IsValid) return;


        var createCharacterRequest = NewCharacterRequest();

        var json = "";
        ICharacter character;
        try
        {
            var exportResult = await Task.Run(() => _gameService.CreateJsonCharacterExportAsync(createCharacterRequest));
            json = exportResult.json;
            character = exportResult.character;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to create json export");
            _notificationManager.ShowNotification("Failed to create json export", e.Message, null);
            return;
        }


        var content = new ScrollViewer()
        {
            Content = new TextBlock()
            {
                Text = json,
                IsTextSelectionEnabled = true,
                Margin = new Thickness(4)
            }
        };


        var dialogHeight = App.MainWindow.Height * 0.5;
        var dialogWidth = App.MainWindow.Width * 0.7;
        var contentWrapper = new StackPanel()
        {
            MinHeight = dialogHeight,
            MinWidth = dialogWidth,
            Children =
            {
                content
            }
        };


        var characterModelDialog = new ContentDialog
        {
            Title = "Character Model Json Export",
            Content = contentWrapper,
            PrimaryButtonText = "Copy to clipboard and Close",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Resources =
            {
                ["ContentDialogMaxWidth"] = 8000,
                ["ContentDialogMaxHeight"] = 4000
            }
        };

        var result = await characterModelDialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        DataPackage package = new();
        package.SetText(json);
        Clipboard.SetContent(package);

        _notificationManager.ShowNotification("Character json copied to clipboard", "", null);

        if (createCharacterRequest.Image is null || !File.Exists(createCharacterRequest.Image.LocalPath))
            return;

        await Task.Run(async () =>
        {
            var imageFolder = App.GetUniqueTmpFolder();

            var imageFilePath = createCharacterRequest.Image.LocalPath;

            var tmpImageFilePath = Path.Combine(imageFolder.FullName, character.InternalName + Path.GetExtension(imageFilePath));

            File.Copy(imageFilePath, tmpImageFilePath, true);

            await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(imageFolder.FullName));
        }).ConfigureAwait(false);
    }

    private CreateCharacterRequest NewCharacterRequest()
    {
        var internalName = new InternalName(Form.InternalName.Value);
        var displayName = Form.DisplayName.Value.Trim();
        var modFilesName = Form.ModFilesName.Value.Trim().ToLowerInvariant();
        var keys = Form.Keys.Items.Select(k => k.Trim().ToLowerInvariant()).ToArray();
        var releaseDate = Form.ReleaseDate.Value.Date;
        var isMultiMod = Form.IsMultiMod.Value;

        var createCharacterRequest = new CreateCharacterRequest()
        {
            InternalName = internalName,
            DisplayName = displayName.IsNullOrEmpty() ? Form.InternalName.Value.Trim() : displayName,
            ModFilesName = modFilesName,
            Image = Form.Image.Value == ImageHandlerService.StaticPlaceholderImageUri ? null : Form.Image.Value,
            Rarity = Form.Rarity.Value,
            Element = Form.Element.Value,
            Class = null,
            Keys = keys,
            ReleaseDate = releaseDate,
            IsMultiMod = isMultiMod
        };

        return createCharacterRequest;
    }

    public class ElementItemVM(string internalName, string displayText)
    {
        public string InternalName { get; } = internalName;
        public string DisplayText { get; } = displayText;

        public override string ToString() => DisplayText;
    }
}