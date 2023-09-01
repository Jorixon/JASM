using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class MoveModsFlyoutVM : ObservableRecipient
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGenshinService _genshinService;

    private GenshinCharacter _shownCharacter = null!;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(DeleteModsCommand), nameof(MoveModsCommand))]
    private GenshinCharacter? _selectedCharacter = null;

    [ObservableProperty] private bool _isMoveModsFlyoutOpen;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(DeleteModsCommand))]
    private int _selectedModsCount;

    [ObservableProperty] private string _searchText = string.Empty;


    public ObservableCollection<GenshinCharacter> SuggestedCharacters { get; init; } = new();
    private List<NewModModel> SelectedMods { get; init; } = new();

    public MoveModsFlyoutVM(IGenshinService genshinService, ISkinManagerService skinManagerService)
    {
        _genshinService = genshinService;
        _skinManagerService = skinManagerService;
    }

    public void SetShownCharacter(GenshinCharacter selectedCharacter)
    {
        if (_shownCharacter is not null) throw new InvalidOperationException("Selected character is already set");
        _shownCharacter = selectedCharacter;
    }


    [RelayCommand]
    private void SetSelectedMods(IEnumerable<NewModModel> modModel)
    {
        SelectedMods.Clear();
        SelectedMods.AddRange(modModel);
        SelectedModsCount = SelectedMods.Count;
    }

    private bool CanMoveModsCommandExecute() => SelectedCharacter is not null && SelectedModsCount > 0;

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
            await Task.Run(() =>
                _skinManagerService.TransferMods(sourceModList, destinationModList,
                    SelectedMods.Select(modEntry => modEntry.Id)));
        }
        catch (InvalidOperationException e)
        {
            notificationManager
                .ShowNotification("Invalid Operation Exception",
                    $"Cannot move mods\n{e.Message}", TimeSpan.FromSeconds(10));
            return;
        }

        notificationManager.ShowNotification("Mods Moved",
            $"Successfully moved {selectedModsCount} mods to {selectedCharacterName}",
            TimeSpan.FromSeconds(5));

        ModsMoved?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ModsMoved;

    private bool CanDeleteModsCommandExecute() => SelectedCharacter is null && SelectedModsCount > 0;

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
            SelectionMode = ListViewSelectionMode.None,
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

        var result = await windowManager.ShowDialogAsync(new ContentDialog()
        {
            Title = $"Delete These {selectedModsCount} Mods?",
            Content = stackPanel,
            PrimaryButtonText = "Delete?",
            SecondaryButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Secondary
        });

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


    [RelayCommand]
    private void CloseFlyout()
    {
        IsMoveModsFlyoutOpen = false;
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
        var eligibleCharacters =
            await Task.Run(() =>
                _genshinService.GetCharacters(searchString, fuzzRatio: 40).OrderByDescending(kv => kv.Value));

        foreach (var eligibleCharacter in eligibleCharacters)
            SuggestedCharacters.Add(eligibleCharacter.Key);


        if (SuggestedCharacters.Count == 0)
            SuggestedCharacters.Add(_noCharacterFound);
    }

    [RelayCommand]
    private void ResetState()
    {
        SelectedCharacter = null;
        SuggestedCharacters.Clear();
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void SelectCharacter(GenshinCharacter character)
    {
        if (character == _noCharacterFound) return;
        SuggestedCharacters.Clear();
        SelectedCharacter = character;
        SearchText = character.DisplayName;
    }
}