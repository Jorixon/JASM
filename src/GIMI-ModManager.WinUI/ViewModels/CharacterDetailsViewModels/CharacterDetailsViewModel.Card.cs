using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.WinUI.Models.CustomControlTemplates;
using GIMI_ModManager.WinUI.Models.Settings;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;
using GIMI_ModManager.WinUI.Views;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;

public partial class CharacterDetailsViewModel
{
    // Character Pane

    public IModdableObject ShownModObject { get; private set; } = null!;
    public ICharacter? Character { get; private set; }
    [ObservableProperty] private ICharacterSkin? _selectedSkin;

    [ObservableProperty] private Uri _shownModImageUri = ImageHandlerService.StaticPlaceholderImageUri;

    [MemberNotNullWhen(true, nameof(Character), nameof(SelectedSkin))]
    public bool IsCharacter => ShownModObject is ICharacter && Character != null && SelectedSkin != null;

    [ObservableProperty] private bool _isModObjectLoaded;

    [ObservableProperty] private string _trackedModsCount = "~";

    public bool CanChangeInGameSkins => !IsHardBusy && IsCharacter && Character.Skins.Count > 1;
    public ObservableCollection<SelectCharacterTemplate> SelectableInGameSkins { get; } = new();


    private void InitCharacterCard(object parameter)
    {
        var internalName = ParseNavigationArg(parameter);

        var moddableObject = _gameService.GetModdableObjectByIdentifier(internalName);

        if (moddableObject == null)
        {
            ErrorNavigateBack();
            return;
        }

        ShownModObject = moddableObject;
        ShownModImageUri = moddableObject.ImageUri ?? ImageHandlerService.StaticPlaceholderImageUri;


        if (ShownModObject is ICharacter character)
        {
            Character = character;
            var skins = character.Skins.ToArray();
            SelectedSkin = skins.First();
            ShownModImageUri = SelectedSkin.ImageUri ?? ImageHandlerService.StaticPlaceholderImageUri;

            foreach (var characterInGameSkin in skins)
            {
                var skinImage = characterInGameSkin.ImageUri ?? ImageHandlerService.StaticPlaceholderImageUri;
                SelectableInGameSkins.Add(
                    new SelectCharacterTemplate(characterInGameSkin.DisplayName, characterInGameSkin.InternalName,
                        skinImage.ToString())
                );
            }

            SelectedSkin = skins.First(skinVm => skinVm.IsDefault);

            if (SelectedSkin.ImageUri is not null)
                ShownModImageUri = SelectedSkin.ImageUri;

            //MoveModsFlyoutVM.SetActiveSkin(SelectedInGameSkin);
        }

        var modList = _skinManagerService.GetCharacterModListOrDefault(moddableObject.InternalName);

        if (modList is null)
        {
            ErrorNavigateBack();
            return;
        }

        _modList = modList;
        IsModObjectLoaded = true;
        OnModObjectLoaded?.Invoke(this, EventArgs.Empty);
    }


    [RelayCommand]
    private async Task SelectSkinAsync(SelectCharacterTemplate? characterTemplate)
    {
        if (characterTemplate is null || IsHardBusy)
            return;

        if (ShownModObject is not ICharacter character || SelectedSkin is null)
            return;


        if (SelectedSkin.InternalName.Equals(characterTemplate.InternalName))
            return;

        await CommandWrapperAsync(true, async () =>
        {
            var characterSkin = character.Skins.FirstOrDefault(skin =>
                skin.InternalName.Equals(characterTemplate.InternalName));


            if (characterSkin is null)
            {
                _logger.Error("Could not find character skin {SkinName} for character {CharacterName}",
                    characterTemplate.DisplayName, ShownModObject.DisplayName);
                _notificationService.ShowNotification("Error while switching character skin.", "",
                    TimeSpan.FromSeconds(5));
                return;
            }

            SelectedSkin = characterSkin;
            ShownModImageUri = SelectedSkin.ImageUri ?? ImageHandlerService.StaticPlaceholderImageUri;

            //MoveModsFlyoutVM.SetActiveSkin(characterSkin);


            foreach (var selectableInGameSkin in SelectableInGameSkins)
                selectableInGameSkin.IsSelected = selectableInGameSkin.InternalName.Equals(
                    characterTemplate.InternalName,
                    StringComparison.OrdinalIgnoreCase);

            ContextMenuVM.ChangeSkin(CreateContext());
            await ModGridVM.OnChangeSkinAsync(CreateContext());
            AutoSelectFirstMod();
        }).ConfigureAwait(false);
    }

    [RelayCommand]
    private void GoToModsOverview()
    {
        _navigationService.NavigateTo(typeof(ModsOverviewVM).FullName!, ShownModObject.InternalName);
    }

    [RelayCommand]
    private void GoToCharacterEditScreen()
    {
        _navigationService.NavigateTo(typeof(CharacterManagerViewModel).FullName!, ShownModObject.InternalName);
    }


    [RelayCommand]
    private async Task GoToGalleryScreen()
    {
        var settings = await _localSettingsService.ReadOrCreateSettingAsync<CharacterDetailsSettings>(
            CharacterDetailsSettings.Key);

        settings.GalleryView = true;

        await _localSettingsService.SaveSettingAsync(CharacterDetailsSettings.Key, settings);

        _navigationService.NavigateTo(typeof(CharacterGalleryViewModel).FullName!, ShownModObject.InternalName);
        _navigationService.ClearBackStack(1);
    }

    [RelayCommand]
    private void GoBackToGrid()
    {
        var gridLastStack = _navigationService.GetBackStackItems().LastOrDefault();

        if (gridLastStack is not null && gridLastStack.SourcePageType == typeof(CharactersPage))
        {
            _navigationService.GoBack();
            return;
        }

        _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!, ShownModObject.ModCategory);
    }

    private void UpdateTrackedMods() => TrackedModsCount = ModGridVM.TrackedMods.ToString();

    private ModDetailsPageContext CreateContext() => new(ShownModObject, SelectedSkin);
}