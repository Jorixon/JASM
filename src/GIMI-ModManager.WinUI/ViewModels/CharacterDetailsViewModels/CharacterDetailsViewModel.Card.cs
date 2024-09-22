using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;

public partial class CharacterDetailsViewModel
{
    // Character Pane

    public IModdableObject ShownModObject { get; private set; } = null!;
    public ICharacter? Character { get; private set; }
    public ICharacterSkin? SelectedSkin { get; private set; }

    [ObservableProperty] private Uri _shownModImageUri = ImageHandlerService.StaticPlaceholderImageUri;

    [MemberNotNullWhen(true, nameof(Character), nameof(SelectedSkin))]
    public bool IsCharacter => ShownModObject is ICharacter && Character != null && SelectedSkin != null;

    [ObservableProperty] private ICharacterSkin? _selectedInGameSkin;

    [ObservableProperty] private bool _isModObjectLoaded;

    [RelayCommand]
    private void SelectSkin()
    {
    }
}