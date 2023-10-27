using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using Microsoft.UI.Xaml;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class CharacterManagerViewModel : ObservableRecipient, INavigationAware
{
    private readonly IGameService _gameService;
    private readonly ISkinManagerService _skinManagerService;


    [ObservableProperty] private bool _isCharacterDisabled;

    [ObservableProperty] private bool _isEditMode;

    [ObservableProperty] private string _characterName = string.Empty;

    [ObservableProperty] private string _imagePath = string.Empty;

    [ObservableProperty] private string _characterFolderPath = string.Empty;

    [ObservableProperty] private int _modsCount;

    [ObservableProperty] private ObservableCollection<string> _characterKeys = new();

    [ObservableProperty] private ICharacter? _selectedCharacter;
    [ObservableProperty] private Visibility _characterSelectionVisibility = Visibility.Collapsed;

    private List<ICharacter> _characters = new();

    private static InternalName? _lastSelectedCharacter;

    public ObservableCollection<CharacterSearchResult> Suggestions { get; } = new();

    public event EventHandler<SetSelectionArgs>? SetSelection;

    public CharacterManagerViewModel(IGameService gameService, ISkinManagerService skinManagerService)
    {
        _gameService = gameService;
        _skinManagerService = skinManagerService;
    }


    private void SetCharacter(ICharacter character)
    {
        SelectedCharacter = character;
        CharacterName = character.DisplayName;
        ImagePath = character.ImageUri?.ToString() ?? " ";

        var characterModList = _skinManagerService.GetCharacterModList(character);
        CharacterFolderPath = characterModList.AbsModsFolderPath;
        ModsCount = characterModList.Mods.Count;
        SelectedCharacter.Keys.ForEach(key => CharacterKeys.Add(key));
        CharacterSelectionVisibility = Visibility.Visible;

        SetSelection?.Invoke(this, new SetSelectionArgs(character));
        _lastSelectedCharacter = character.InternalName;
    }

    private void ResetCharacter()
    {
        CharacterSelectionVisibility = Visibility.Collapsed;
        SelectedCharacter = null;
        CharacterName = string.Empty;
        ImagePath = string.Empty;
        CharacterFolderPath = string.Empty;
        ModsCount = 0;
        CharacterKeys.Clear();
        SetSelection?.Invoke(this, new SetSelectionArgs(null));
        _lastSelectedCharacter = null;
    }

    [RelayCommand]
    private void SelectCharacter(CharacterSearchResult? selectedCharacter)
    {
        if (selectedCharacter is null)
        {
            return;
        }

        var character = _characters.FirstOrDefault(c => c.InternalNameEquals(selectedCharacter.InternalName));

        if (character is null)
        {
            OnSearchTextChanged("");
            return;
        }

        ResetCharacter();
        SetCharacter(character);
        Suggestions.Clear();
    }

    [RelayCommand]
    private void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
        if (!IsEditMode)
        {
            var selectedCharacter = SelectedCharacter;
            ResetCharacter();
            SetCharacter(selectedCharacter);
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedCharacter is null)
            return;

        if (!CharacterName.IsNullOrEmpty() && !SelectedCharacter.DisplayName.Equals(CharacterName))
        {
            await _gameService.SetCharacterDisplayNameAsync(SelectedCharacter, CharacterName);
        }

        ToggleEditMode();
    }

    public void OnNavigatedTo(object parameter)
    {
        _characters = _gameService.GetCharacters().Concat(_gameService.GetDisabledCharacters()).ToList();

        if (_lastSelectedCharacter is null) return;
        var character = _characters.FirstOrDefault(c => c.InternalNameEquals(_lastSelectedCharacter));
        if (character is null) return;
        SetCharacter(character);
    }

    public void OnNavigatedFrom()
    {
    }


    public void OnSearchTextChanged(string query)
    {
        Suggestions.Clear();
        if (string.IsNullOrWhiteSpace(query))
        {
            ResetCharacter();
            return;
        }


        var suitableCharacters = _gameService.GetCharacters(query, minScore: 100).OrderByDescending(kv => kv.Value)
            .Take(5).ToArray();

        if (suitableCharacters.Length == 0)
        {
            Suggestions.Add(new CharacterSearchResult
            {
                Name = "No results found"
            });
            return;
        }

        foreach (var character in suitableCharacters.Select(kv => kv.Key))
            Suggestions.Add(CharacterSearchResult.FromCharacter(character));
    }


    public class SetSelectionArgs : EventArgs
    {
        public INameable? Character { get; }

        public SetSelectionArgs(INameable? character)
        {
            Character = character;
        }
    }
}

public class CharacterSearchResult
{
    public string InternalName { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;

    public static CharacterSearchResult FromCharacter(ICharacter character)
    {
        return new CharacterSearchResult
        {
            InternalName = character.InternalName,
            Name = character.DisplayName,
            ImagePath = character.ImageUri?.ToString() ?? " "
        };
    }

    public override string ToString()
    {
        return Name;
    }
}