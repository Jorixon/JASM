using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels.Validation;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

public sealed partial class EditCharacterForm : Form
{
    public EditCharacterForm()
    {
    }

    public void Initialize(ICharacter character, ICollection<IModdableObject> allModdableObjects)
    {
        allModdableObjects = allModdableObjects.Contains(character)
            ? allModdableObjects.Where(mo => !mo.Equals(character)).ToArray()
            : allModdableObjects;

        InternalName.ValidationRules.AddInternalNameValidators(allModdableObjects);
        InternalName.ReInitializeInput(character.InternalName);

        DisplayName.ValidationRules.AddDisplayNameValidators(allModdableObjects);
        DisplayName.ReInitializeInput(character.DisplayName);

        Image.ValidationRules.AddImageValidators();
        Image.ReInitializeInput(character.ImageUri ?? ImageHandlerService.StaticPlaceholderImageUri);

        Keys.ValidationRules.AddKeysValidators(allModdableObjects.OfType<ICharacter>().ToList());
        Keys.ReInitializeInput(character.Keys);

        IsMultiMod.ReInitializeInput(character.IsMultiMod);
        IsInitialized = true;
    }

    public InputField<Uri> Image { get; } = new(ImageHandlerService.StaticPlaceholderImageUri);
    public StringInputField InternalName { get; } = new(string.Empty);
    public StringInputField DisplayName { get; } = new(string.Empty);
    public ListInputField<string> Keys { get; } = new();

    public InputField<bool> IsMultiMod { get; } = new(false);
}