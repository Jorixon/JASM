using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

public partial class CreateCharacterViewModel : ObservableObject
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGameService _gameService;

    [ObservableProperty] private NewCharacterForm _form = new();

    public CreateCharacterViewModel(ISkinManagerService skinManagerService, IGameService gameService)
    {
        _skinManagerService = skinManagerService;
        _gameService = gameService;

        var allModObjects = _gameService.GetAllModdableObjects(GetOnly.Both);

        var mapping = allModObjects.ToDictionary(i => i.InternalName.Id, i => i);

        Form.Initialize(mapping);
    }


    [RelayCommand]
    private async Task CopyCharacterToClipboardAsync()
    {
        return;
    }

    [RelayCommand]
    private async Task OpenCustomCharacterJsonFile()
    {
        return;
    }
}

public partial class NewCharacterForm : ObservableObject
{
    [ObservableProperty] private bool _isInitialized;

    public bool IsValid => IsInitialized && Fields.All(f => f.ValidationResults.Count == 0);

    public bool AnyFieldDirty { get; private set; }

    public List<BaseInputFieldViewModel> Fields { get; } = [];

    public StringInputFieldViewModel InternalName { get; } = new(string.Empty);
    public StringInputFieldViewModel DisplayName { get; } = new(string.Empty);

    public StringInputFieldViewModel ModFilesName { get; } = new(string.Empty);

    public InputFieldViewModel<DateTimeOffset> ReleaseDate { get; } = new(DateTime.Now);

    public InputFieldViewModel<Uri> Image { get; } = new(ImageHandlerService.StaticPlaceholderImageUri);

    public InputFieldViewModel<int> Rarity { get; } = new(5);


    public void Initialize(Dictionary<string, IModdableObject> allModdableObjects)
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
            context => string.IsNullOrWhiteSpace(context.Value.Trim()) ? new ValidationResult { Message = "Display name cannot be empty" } : null
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
        AnyFieldDirty = true;
        field.Validate(this);


        if (field.FieldName == nameof(InternalName) && DisplayName.Value.IsNullOrEmpty())
        {
            var internalName = InternalName.Value.Trim();
            if (internalName.Length > 1)
                DisplayName.PlaceHolderText = internalName[0].ToString().ToUpper() + internalName.Substring(1);
            else
                DisplayName.PlaceHolderText = string.Empty;
        }
    }
}

public partial class StringInputFieldViewModel(string value) : InputFieldViewModel<string>(value)
{
    [ObservableProperty] private string placeHolderText = "";
}

public partial class InputFieldViewModel<T> : BaseInputFieldViewModel
{
    public T DefaultValue { get; }

    [ObservableProperty] private bool _isDirty;

    [ObservableProperty] private T _value;

    public InputFieldViewModel(T value)
    {
        _value = value;
        DefaultValue = value;

        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(Value) or nameof(DefaultValue))
                _isDirty = EqualityComparer<T>.Default.Equals(Value, DefaultValue);
        };
    }

    public ObservableCollection<ValidationResult> ValidationResults { get; } = new();
    public List<Func<ValidationContext<T>, ValidationResult?>> ValidationRules { get; } = new();

    public override void Validate(NewCharacterForm form)
    {
        ValidationResults.Clear();

        foreach (var rule in ValidationRules)
        {
            if (rule(new ValidationContext<T>(form, this, Value)) is { } result)
            {
                ValidationResults.Add(result);
            }
        }
    }

    public override void Reset(NewCharacterForm form)
    {
        Value = DefaultValue;
        Validate(form);
    }
}

public abstract class BaseInputFieldViewModel : ObservableObject
{
    public string FieldName { get; set; } = string.Empty;

    public ObservableCollection<ValidationResult> ValidationResults { get; } = new();

    public bool IsValid => ValidationResults.Count == 0;

    public abstract void Validate(NewCharacterForm form);

    public abstract void Reset(NewCharacterForm form);
}

public readonly struct ValidationContext<T>(NewCharacterForm form, BaseInputFieldViewModel field, T value)
{
    public NewCharacterForm Form { get; } = form;

    public BaseInputFieldViewModel Field { get; } = field;

    public T Value { get; } = value;
}

public class ValidationResult
{
    public string Message { get; set; }

    private enum ValidationType
    {
        Information,
        Warning,
        Error
    }
}