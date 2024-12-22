using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

public partial class CreateCharacterForm : Form
{
    public StringInputField InternalName { get; } = new(string.Empty);
    public StringInputField DisplayName { get; } = new(string.Empty);

    public StringInputField ModFilesName { get; } = new(string.Empty);

    public InputField<DateTimeOffset> ReleaseDate { get; } = new(DateTime.Now);

    public InputField<Uri> Image { get; } = new(ImageHandlerService.StaticPlaceholderImageUri);

    public InputField<int> Rarity { get; } = new(5);

    public ListInputField<string> Keys { get; } = new();

    public InputField<string> Element { get; } = new("None");

    public InputField<bool> IsMultiMod { get; } = new(false);


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
                var fileExtension = Path.GetExtension((string?)context.Value.LocalPath);


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
            context => context.Value < 0 ? new ValidationResult { Message = "Rarity must be greater than -1" } : null,
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

    public CreateCharacterForm()
    {
        var fields = GetType()
            .GetProperties().Where(p => p.PropertyType.IsAssignableTo(typeof(BaseInputField)))
            .Select(p => new
            {
                Property = (BaseInputField)p.GetValue(this)!,
                PropertyName = p.Name
            });

        foreach (var field in fields)
        {
            field.Property.FieldName = field.PropertyName;
            field.Property.PropertyChanged += (sender, _) => OnValueChanged((BaseInputField)sender!);
            Fields.Add(field.Property);
        }
    }

    public override void OnValueChanged(BaseInputField field)
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