using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

public sealed partial class EditCharacterForm : Form
{
    public EditCharacterForm()
    {
    }

    public void Initialize(ICharacter character)
    {
        // TODO: Add validation
#if RELEASE
throw new NotImplementedException();
#endif

        InternalName.ReInitializeInput(character.InternalName);
        DisplayName.ReInitializeInput(character.DisplayName);
        Image.ReInitializeInput(character.ImageUri ?? ImageHandlerService.StaticPlaceholderImageUri);
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