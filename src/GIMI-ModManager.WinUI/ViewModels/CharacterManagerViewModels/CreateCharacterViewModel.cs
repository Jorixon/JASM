using System.Collections.ObjectModel;
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
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.Notifications;
using Serilog;
using static GIMI_ModManager.WinUI.Helpers.Extensions;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

public partial class CreateCharacterViewModel : ObservableObject
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGameService _gameService;
    private readonly NotificationManager _notificationManager;
    private readonly ImageHandlerService _imageHandlerService;
    private readonly ILogger _logger;

    private readonly List<IModdableObject> _allModObjects;

    public bool IsFinished { get; private set; }

    public NewCharacterForm Form { get; } = new();
    [ObservableProperty] private string _newKeyNameInput = string.Empty;

    [ObservableProperty] private ElementItemVM _selectedElement;
    public ObservableCollection<ElementItemVM> Elements { get; } = new();

    public CreateCharacterViewModel(ISkinManagerService skinManagerService, IGameService gameService, NotificationManager notificationManager,
        ImageHandlerService imageHandlerService, ILogger logger)
    {
        _skinManagerService = skinManagerService;
        _gameService = gameService;
        _notificationManager = notificationManager;
        _imageHandlerService = imageHandlerService;
        _logger = logger.ForContext<CreateCharacterViewModel>();

        _allModObjects = _gameService.GetAllModdableObjects(GetOnly.Both);

        var mapping = _allModObjects.ToDictionary(i => i.InternalName.Id, i => i);
        var elementsMapping = _gameService.GetElements().ToDictionary(e => e.InternalName.Id, e => e);
        Form.Initialize(mapping, elementsMapping);

        var elements = _gameService.GetElements();
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
            if (args.PropertyName is nameof(NewCharacterForm.IsValid) or nameof(NewCharacterForm.AnyFieldDirty))
                SaveCharacterCommand.NotifyCanExecuteChanged();
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
        var internalName = new InternalName(Form.InternalName.Value);
        var displayName = Form.DisplayName.Value.Trim();
        var modFilesName = Form.ModFilesName.Value.Trim().ToLowerInvariant();
        var keys = Form.Keys.Items.Select(k => k.Trim().ToLowerInvariant()).ToArray();
        var releaseDate = Form.ReleaseDate.Value.Date;
        var isMultiMod = Form.IsMultiMod.Value;

        var createCharacterRequest = new CreateCharacterRequest()
        {
            InternalName = internalName,
            DisplayName = displayName.IsNullOrEmpty() ? internalName : displayName,
            ModFilesName = modFilesName,
            Image = Form.Image.Value,
            Rarity = Form.Rarity.Value,
            Element = Form.Element.Value,
            Class = null,
            Keys = keys,
            ReleaseDate = releaseDate,
            IsMultiMod = isMultiMod
        };

        ICharacter character;
        try
        {
            character = await Task.Run(() => _gameService.CreateCharacterAsync(createCharacterRequest));

            if (character is null)
                throw new Exception("Character was not created, null, was returned! Unknown error");
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
        var image = await _imageHandlerService.PickImageAsync();
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

        if (_allModObjects.OfType<ICharacter>().SelectMany(c => c.Keys).Any(key => key.Equals(newKey, StringComparison.OrdinalIgnoreCase)))
            return;

        if (Form.Keys.Items.Contains(newKey, StringComparer.OrdinalIgnoreCase))
            return;

        Form.Keys.Items.Add(newKey);
    }

    [RelayCommand]
    private void RemoveKey(string key)
    {
        Form.Keys.Items.Remove(key);
    }

    #endregion


    public class ElementItemVM(string internalName, string displayText)
    {
        public string InternalName { get; } = internalName;
        public string DisplayText { get; } = displayText;

        public override string ToString() => DisplayText;
    }
}

public partial class NewCharacterForm : ObservableObject
{
    [ObservableProperty] private bool _isInitialized;

    public bool IsValid => IsInitialized && Fields.All(f => f.IsValid);

    [ObservableProperty] private bool _anyFieldDirty;

    public List<BaseInputFieldViewModel> Fields { get; } = [];

    public StringInputFieldViewModel InternalName { get; } = new(string.Empty);
    public StringInputFieldViewModel DisplayName { get; } = new(string.Empty);

    public StringInputFieldViewModel ModFilesName { get; } = new(string.Empty);

    public InputFieldViewModel<DateTimeOffset> ReleaseDate { get; } = new(DateTime.Now);

    public InputFieldViewModel<Uri> Image { get; } = new(ImageHandlerService.StaticPlaceholderImageUri);

    public InputFieldViewModel<int> Rarity { get; } = new(5);

    public ListInputFieldViewModel<string> Keys { get; } = new();

    public InputFieldViewModel<string> Element { get; } = new("None");

    public InputFieldViewModel<bool> IsMultiMod { get; } = new(false);


    public void Initialize(Dictionary<string, IModdableObject> allModdableObjects, Dictionary<string, IGameElement> elements)
    {
        InternalName.ValidationRules.AddRange([
            context => string.IsNullOrWhiteSpace(context.Value.Trim()) ? new ValidationResult { Message = "Internal name cannot be empty" } : null,
            context =>
            {
                if (!allModdableObjects.TryGetValue(context.Value.Trim().ToLowerInvariant(), out var existingModdableObject))
                    return null;

                return new ValidationResult { Message = $"Internal name '{context.Value}' is already in use by '{existingModdableObject.DisplayName}'" };
            }
        ]);

        ModFilesName.ValidationRules.AddRange([
            context => allModdableObjects.Values.Any(m =>
                m.ModFilesName != string.Empty && m.ModFilesName.Equals(context.Value.Trim(), StringComparison.OrdinalIgnoreCase))
                ? new ValidationResult() { Message = "Mod files name already in use" }
                : null
        ]);

        DisplayName.ValidationRules.AddRange([
            context => allModdableObjects.Values.Any(m =>
                m.DisplayName.Trim().Equals(context.Value.Trim(), StringComparison.OrdinalIgnoreCase))
                ? new ValidationResult()
                    { Message = "Another mod object already uses this display name, this may cause oddities with search", Type = ValidationType.Warning }
                : null
        ]);


        Image.ValidationRules.AddRange([
            context => context.Value == null! ? new ValidationResult { Message = "Image cannot be empty" } : null,
            context => !context.Value.IsFile || !File.Exists(context.Value.LocalPath)
                ? new ValidationResult() { Message = "Image must be a valid existing image" }
                : null,
            context =>
            {
                if (!context.Value.IsFile)
                    return null;
                var fileExtension = Path.GetExtension(context.Value.LocalPath);


                var isSupportedExtension = Constants.SupportedImageExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);

                return isSupportedExtension
                    ? null
                    : new ValidationResult
                    {
                        Message =
                            $"Image must be one of the following types: {string.Join(", ", Constants.SupportedImageExtensions)}. The extension {fileExtension} is not supported."
                    };
            }
        ]);

        Rarity.ValidationRules.AddRange([
            context => context.Value < 1 ? new ValidationResult { Message = "Rarity must be greater than 0" } : null,
            context => context.Value > 10 ? new ValidationResult { Message = "Rarity must be less than 11" } : null
        ]);

        Element.ValidationRules.AddRange([
            context => elements.Values.Any(e => e.InternalNameEquals(context.Value))
                ? null
                : new ValidationResult
                    { Message = $"Element {context.Value} does not exist. Valid values {string.Join(',', elements.Values.Select(e => e.InternalName))}" }
        ]);

        IsInitialized = true;
    }

    public NewCharacterForm()
    {
        var fields = GetType()
            .GetProperties().Where(p => p.PropertyType.IsAssignableTo(typeof(BaseInputFieldViewModel)))
            .Select(p => new
            {
                Property = (BaseInputFieldViewModel)p.GetValue(this)!,
                PropertyName = p.Name
            });

        foreach (var field in fields)
        {
            field.Property.FieldName = field.PropertyName;
            field.Property.PropertyChanged += (sender, _) => OnValueChanged((BaseInputFieldViewModel)sender!);
            Fields.Add(field.Property);
        }
    }

    public void OnValueChanged(BaseInputFieldViewModel field)
    {
        if (!IsInitialized) return;
        var oldValidValue = IsValid;

        AnyFieldDirty = Fields.Any(f => f.IsDirty);
        field.Validate(this);


        if (field.FieldName == nameof(InternalName) && DisplayName.Value.IsNullOrEmpty())
        {
            var internalName = InternalName.Value.Trim();
            if (internalName.Length > 1)
                DisplayName.PlaceHolderText = internalName[0].ToString().ToUpper() + internalName.Substring(1);
            else
                DisplayName.PlaceHolderText = string.Empty;
        }

        if (oldValidValue != IsValid)
            OnPropertyChanged(nameof(IsValid));
    }
}

public partial class StringInputFieldViewModel(string value) : InputFieldViewModel<string>(value)
{
    [ObservableProperty] private string _placeHolderText = string.Empty;
}

public partial class ListInputFieldViewModel<TItem> : BaseInputFieldViewModel
{
    public ListInputFieldViewModel(IEnumerable<TItem>? values = null)
    {
        DefaultValue = (values ?? []).ToList();
        Items.AddRange(DefaultValue);
        Items.CollectionChanged += (_, _) => IsDirty = !Items.SequenceEqual(DefaultValue);
    }

    public List<TItem> DefaultValue { get; }
    public ObservableCollection<TItem> Items { get; } = new();

    public List<Func<ValidationContext<ReadOnlyCollection<TItem>>, ValidationResult?>> ValidationRules { get; } = new();


    public override void Validate(NewCharacterForm form)
    {
        ValidationResults.Clear();

        foreach (var rule in ValidationRules)
        {
            if (rule(new ValidationContext<ReadOnlyCollection<TItem>>(form, this, Items.AsReadOnly())) is { } result)
            {
                AddValidationResult(result);
            }
        }
    }

    public override void Reset(NewCharacterForm form)
    {
        Items.Clear();
        Items.AddRange(DefaultValue);
        Validate(form);
        IsDirty = false;
    }
}

public partial class InputFieldViewModel<T> : BaseInputFieldViewModel
{
    public T DefaultValue { get; }

    [ObservableProperty] private T _value;

    public InputFieldViewModel(T value)
    {
        _value = value;
        DefaultValue = value;


        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(Value) or nameof(DefaultValue))
                IsDirty = !EqualityComparer<T>.Default.Equals(Value, DefaultValue);
        };
    }

    public List<Func<ValidationContext<T>, ValidationResult?>> ValidationRules { get; } = new();

    public override void Validate(NewCharacterForm form)
    {
        ValidationResults.Clear();

        foreach (var rule in ValidationRules)
        {
            if (rule(new ValidationContext<T>(form, this, Value)) is { } result)
            {
                AddValidationResult(result);
            }
        }
    }

    public override void Reset(NewCharacterForm form)
    {
        Value = DefaultValue;
        Validate(form);
        IsDirty = false;
    }
}

public abstract partial class BaseInputFieldViewModel : ObservableObject
{
    public string FieldName { get; set; } = string.Empty;

    [ObservableProperty] private bool _isDirty;

    public ObservableCollection<ValidationResult> ValidationResults { get; } = new();

    public IEnumerable<ValidationResult> Errors => ValidationResults.Where(r => r.Type == ValidationType.Error);

    public IEnumerable<ValidationResult> Warnings => ValidationResults.Where(r => r.Type == ValidationType.Warning);

    public IEnumerable<ValidationResult> Information => ValidationResults.Where(r => r.Type == ValidationType.Information);

    public bool IsValid => !Errors.Any();

    public abstract void Validate(NewCharacterForm form);

    public abstract void Reset(NewCharacterForm form);

    protected void AddValidationResult(ValidationResult result) => ValidationResults.Add(result);
}

public sealed class NoOpFieldViewModel : BaseInputFieldViewModel
{
    public override void Validate(NewCharacterForm form)
    {
    }

    public override void Reset(NewCharacterForm form)
    {
    }
}

public readonly struct ValidationContext<T>(NewCharacterForm form, BaseInputFieldViewModel field, T value)
{
    public NewCharacterForm Form { get; } = form;

    public BaseInputFieldViewModel Field { get; } = field;

    public T Value { get; } = value;
}

public class ValidationResult
{
    public required string Message { get; init; }

    public ValidationType Type { get; init; } = ValidationType.Error;
}

public enum ValidationType
{
    Information,
    Warning,
    Error
}