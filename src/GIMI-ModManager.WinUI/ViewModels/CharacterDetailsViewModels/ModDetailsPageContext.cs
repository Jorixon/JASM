using System.Diagnostics.CodeAnalysis;
using GIMI_ModManager.Core.GamesService.Interfaces;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;

public sealed record ModDetailsPageContext
{
    public ModDetailsPageContext(IModdableObject shownModObject, ICharacterSkin? characterSkin)
    {
        ShownModObject = shownModObject;
        if (shownModObject is ICharacter character)
            Character = character;

        SelectedSkin = characterSkin;
    }

    public string ModObjectDisplayName => IsCharacter && !SelectedSkin.IsDefault ? SelectedSkin.DisplayName : ShownModObject.DisplayName;
    public IModdableObject ShownModObject { get; }
    public ICharacter? Character { get; }

    public ICollection<ICharacterSkin>? Skins => Character?.Skins;
    public ICharacterSkin? SelectedSkin { get; }


    [MemberNotNullWhen(true, nameof(Character), nameof(SelectedSkin), nameof(Skins))]
    public bool IsCharacter => ShownModObject is ICharacter && Character != null && SelectedSkin != null;
}