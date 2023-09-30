using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities.Genshin;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Models.CustomControlTemplates;
using GIMI_ModManager.WinUI.Models.ViewModels;
using GIMI_ModManager.WinUI.Services;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class MoveModsFlyoutVM : ObservableRecipient
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGenshinService _genshinService;
    private readonly ILogger _logger = App.GetService<ILogger>().ForContext<MoveModsFlyoutVM>();

    private GenshinCharacter _shownCharacter = null!;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(DeleteModsCommand), nameof(MoveModsCommand))]
    private GenshinCharacter? _selectedCharacter = null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteModsCommand), nameof(MoveModsCommand),
        nameof(OverrideModCharacterSkinCommand))]
    private ISubSkin? _selectedCharacterSkin = null;

    private SkinVM? _backendSelectedCharacterSkin = null;

    [ObservableProperty] private bool _isMoveModsFlyoutOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteModsCommand), nameof(OverrideModCharacterSkinCommand))]
    private int _selectedModsCount;

    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private bool _selectedModHasCharacterSkinOverride;

    [ObservableProperty] private string _selectedModCharacterSkinOverrideDisplayName = string.Empty;

    public event EventHandler? CloseFlyoutEvent;
    public ObservableCollection<GenshinCharacter> SuggestedCharacters { get; init; } = new();
    private List<NewModModel> SelectedMods { get; init; } = new();

    public ObservableCollection<SelectCharacterTemplate> SelectableCharacterSkins { get; init; } = new();

    public MoveModsFlyoutVM(IGenshinService genshinService, ISkinManagerService skinManagerService)
    {
        _genshinService = genshinService;
        _skinManagerService = skinManagerService;
    }

    public void SetShownCharacter(GenshinCharacter selectedCharacter)
    {
        if (_shownCharacter is not null)
            throw new InvalidOperationException("Selected character is already set");
        _shownCharacter = selectedCharacter;
        var skinVms = _shownCharacter.InGameSkins.Select(SkinVM.FromSkin);

        foreach (var skinVm in skinVms) SelectableCharacterSkins.Add(new SelectCharacterTemplate(skinVm));
    }

    public void SetActiveSkin(SkinVM skinVm)
    {
        foreach (var selectableCharacterSkin in SelectableCharacterSkins)
            if (selectableCharacterSkin.DisplayName == skinVm.DisplayName)
            {
                selectableCharacterSkin.IsSelected = true;
                _backendSelectedCharacterSkin = skinVm;
                SelectedCharacterSkin =
                    _shownCharacter.InGameSkins.FirstOrDefault(skin =>
                        skin.Name.Equals(skinVm.Name, StringComparison.CurrentCultureIgnoreCase));
            }
            else
            {
                selectableCharacterSkin.IsSelected = false;
            }
    }


    [RelayCommand]
    private void SetSelectedMods(IEnumerable<NewModModel> modModel)
    {
        SelectedMods.Clear();
        SelectedMods.AddRange(modModel);
        SelectedModsCount = SelectedMods.Count;
        if (SelectedModsCount == 1)
        {
            var selectedMod = SelectedMods.First();
            SelectedModHasCharacterSkinOverride = !string.IsNullOrWhiteSpace(selectedMod.CharacterSkinOverride);
            SelectedModCharacterSkinOverrideDisplayName = selectedMod.CharacterSkinOverride;
        }
        else
        {
            SelectedModHasCharacterSkinOverride = false;
            SelectedModCharacterSkinOverrideDisplayName = string.Empty;
        }
    }

    private bool CanMoveModsCommandExecute()
    {
        return SelectedCharacter is not null && SelectedModsCount > 0
                                             && (SelectedCharacterSkin is null ||
                                                 SelectedCharacterSkin.DisplayName.Equals(
                                                     _backendSelectedCharacterSkin?.DisplayName,
                                                     StringComparison.CurrentCultureIgnoreCase));
    }

    [RelayCommand(CanExecute = nameof(CanMoveModsCommandExecute))]
    private async Task MoveModsAsync()
    {
        var sourceModList = _skinManagerService.GetCharacterModList(_shownCharacter);
        var destinationModList = _skinManagerService.GetCharacterModList(SelectedCharacter!);
        var notificationManager = App.GetService<NotificationManager>();

        var selectedCharacterName =
            SelectedCharacter!.DisplayName; // Just in case it is nulled before the operation is done
        var selectedModsCount = SelectedModsCount; // Just in case it is reset before the operation is done

        try
        {
            await Task.Run(async () =>
                await _skinManagerService.TransferMods(sourceModList, destinationModList,
                    SelectedMods.Select(modEntry => modEntry.Id)));
        }
        catch (InvalidOperationException e)
        {
            _logger.Error(e, "Error moving mods");
            notificationManager
                .ShowNotification("Invalid Operation Exception",
                    $"Cannot move mods\n{e.Message}, see logs for details.", TimeSpan.FromSeconds(10));
            return;
        }

        notificationManager.ShowNotification("Mods Moved",
            $"Successfully moved {selectedModsCount} mods to {selectedCharacterName}",
            TimeSpan.FromSeconds(5));

        ModsMoved?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ModsMoved;

    private bool CanDeleteModsCommandExecute()
    {
        return SelectedCharacter is null && SelectedModsCount > 0 && SelectedCharacterSkin is null;
    }

    [RelayCommand(CanExecute = nameof(CanDeleteModsCommandExecute))]
    private async Task DeleteModsAsync()
    {
        var modList = _skinManagerService.GetCharacterModList(_shownCharacter);
        var modEntryIds = new List<Guid>(SelectedMods.Select(modEntry => modEntry.Id));
        var modEntryNames = new List<string>(SelectedMods.Select(modEntry => modEntry.Name));


        var shownCharacterName =
            _shownCharacter.DisplayName; // Just in case it is nulled before the operation is done
        var selectedModsCount = SelectedModsCount; // Just in case it is reset before the operation is done
        var modsDeleted = 0;

        var notificationManager = App.GetService<NotificationManager>();
        var windowManager = App.GetService<IWindowManagerService>();

        var MoveToRecycleBinCheckBox = new CheckBox()
        {
            Content = "Move to Recycle Bin?",
            IsChecked = true
        };
        var mods = new ListView()
        {
            ItemsSource = modEntryNames,
            SelectionMode = ListViewSelectionMode.None
        };

        var scrollViewer = new ScrollViewer()
        {
            Content = mods,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 400
        };
        var stackPanel = new StackPanel()
        {
            Children =
            {
                MoveToRecycleBinCheckBox,
                scrollViewer
            }
        };
        var dialog = new ContentDialog()
        {
            Title = $"Delete These {selectedModsCount} Mods?",
            Content = stackPanel,
            PrimaryButtonText = "Delete",
            SecondaryButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        CloseFlyout();
        var result = await windowManager.ShowDialogAsync(dialog);

        if (result != ContentDialogResult.Primary)
            return;
        var recycleMods = MoveToRecycleBinCheckBox.IsChecked == true;
        try
        {
            await Task.Run(() =>
            {
                foreach (var modEntryId in modEntryIds)
                {
                    modList.DeleteModBySkinEntryId(modEntryId, recycleMods);
                    modsDeleted++;
                }
            });
        }
        catch (InvalidOperationException e)
        {
            _logger.Error(e, "Error deleting mods");
            notificationManager
                .ShowNotification("Invalid Operation Exception",
                    $"Mods Deleted: {modsDeleted}. Some mods may not have been deleted, See Logs.\n{e.Message}",
                    TimeSpan.FromSeconds(10));
            if (modsDeleted > 0)
                ModsDeleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        notificationManager.ShowNotification("Mods Moved",
            $"Successfully deleted {modsDeleted} mods in {shownCharacterName} Mods Folder",
            TimeSpan.FromSeconds(5));

        ModsDeleted?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ModsDeleted;

    private bool CanOverrideModCharacterSkin()
    {
        return SelectedCharacter is null && SelectedCharacterSkin is not null && SelectedModsCount > 0
               && !SelectedCharacterSkin.DisplayName.Equals(_backendSelectedCharacterSkin?.DisplayName,
                   StringComparison.CurrentCultureIgnoreCase);
    }

    [RelayCommand(CanExecute = nameof(CanOverrideModCharacterSkin))]
    private async Task OverrideModCharacterSkin()
    {
        if (SelectedCharacterSkin == null) return;

        var characterSkinToSet = _shownCharacter.InGameSkins.FirstOrDefault(charSkin =>
            charSkin.Name.Equals(SelectedCharacterSkin.Name, StringComparison.CurrentCultureIgnoreCase));

        if (characterSkinToSet == null)
            return;


        foreach (var modModel in SelectedMods)
        {
            var skinMod = _skinManagerService.GetCharacterModList(_shownCharacter).Mods
                .First(mod => mod.Id == modModel.Id).Mod;

            var skinModSettings = modModel.WithModSettings(await skinMod.ReadSkinModSettings());

            skinModSettings.CharacterSkinOverride = characterSkinToSet.Name;

            try
            {
                await skinMod.SaveSkinModSettings(skinModSettings.ToModSettings());
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error saving skin mod settings");
                var notificationManager = App.GetService<NotificationManager>();
                notificationManager.ShowNotification("Error Saving Skin Mod Settings",
                    $"Error saving skin mod settings for {skinMod.Name}\n{e.Message}",
                    TimeSpan.FromSeconds(10));
            }
        }

        ModCharactersSkinOverriden?.Invoke(this, EventArgs.Empty);
        CloseFlyoutCommand.Execute(null);
    }

    public event EventHandler? ModCharactersSkinOverriden;


    [RelayCommand]
    private void CloseFlyout()
    {
        IsMoveModsFlyoutOpen = false;
        CloseFlyoutEvent?.Invoke(this, EventArgs.Empty);
    }

    private readonly GenshinCharacter
        _noCharacterFound = new() { Id = -999999, DisplayName = "No Characters Found..." };

    [RelayCommand]
    private async Task TextChanged(string searchString)
    {
        if (string.IsNullOrWhiteSpace(searchString))
        {
            SuggestedCharacters.Clear();
            return;
        }

        if (SelectedCharacter is not null)
            return;

        SuggestedCharacters.Clear();
        var searchResultKeyValue =
            await Task.Run(() =>
                _genshinService.GetCharacters(searchString, minScore: 100).OrderByDescending(kv => kv.Value));
        var eligibleCharacters = searchResultKeyValue.Select(kv => kv.Key).Where(ch => ch != _shownCharacter).Take(5);


        foreach (var eligibleCharacter in eligibleCharacters)
            SuggestedCharacters.Add(eligibleCharacter);


        if (SuggestedCharacters.Count == 0)
            SuggestedCharacters.Add(_noCharacterFound);
    }

    [RelayCommand]
    private void ResetState()
    {
        SelectedCharacter = null;
        SelectedCharacterSkin = null;
        SuggestedCharacters.Clear();
        SearchText = string.Empty;
    }

    public bool SelectCharacter(GenshinCharacter? character)
    {
        if (character == _noCharacterFound) return false;
        if (character is null)
        {
            if (SuggestedCharacters.Any(ch => ch != _noCharacterFound))
                character = SuggestedCharacters.First(ch => ch != _noCharacterFound);
            else
                return false;
        }

        SuggestedCharacters.Clear();
        SelectedCharacter = character;
        SearchText = character.DisplayName;
        return true;
    }


    [RelayCommand]
    private void SelectNewCharacterSkin(SelectCharacterTemplate? characterTemplate)
    {
        if (characterTemplate == null) return;

        var characterSkinToSet = _shownCharacter.InGameSkins.FirstOrDefault(charSkin =>
            charSkin.DisplayName.Equals(characterTemplate.DisplayName, StringComparison.CurrentCultureIgnoreCase));

        if (characterSkinToSet == null)
            return;

        SelectedCharacterSkin = characterSkinToSet;
        foreach (var selectableCharacterSkin in SelectableCharacterSkins) selectableCharacterSkin.IsSelected = false;
        characterTemplate.IsSelected = true;
    }
}