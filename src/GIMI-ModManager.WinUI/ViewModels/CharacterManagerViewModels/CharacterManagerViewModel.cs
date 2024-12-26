using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class CharacterManagerViewModel : ObservableRecipient, INavigationAware
{
    private readonly IGameService _gameService;
    private readonly ImageHandlerService _imageHandlerService;

    [ObservableProperty] private ICharacter? _selectedCharacter;

    private List<ICharacter> _characters = new();

    private static InternalName? _lastSelectedCharacter;

    public ObservableCollection<CharacterSearchResult> Suggestions { get; } = new();

    public event EventHandler<SetSelectionArgs>? SetSelection;

    public CharacterManagerViewModel(IGameService gameService,
        ImageHandlerService imageHandlerService)
    {
        _gameService = gameService;
        _imageHandlerService = imageHandlerService;
    }


    private void SetCharacter(ICharacter character)
    {
        SelectedCharacter = character;

        SetSelection?.Invoke(this, new SetSelectionArgs(character));
        _lastSelectedCharacter = character.InternalName;
    }

    private void ResetCharacter()
    {
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
    private void OpenCreateCharacterForm()
    {
        ResetCharacter();
        Suggestions.Clear();
        _lastSelectedCharacter = null;
        SetSelection?.Invoke(this, new SetSelectionArgs(true));
    }

    public void OnNavigatedTo(object parameter)
    {
        _characters = _gameService.GetCharacters().Concat(_gameService.GetDisabledCharacters()).ToList();

        ICharacter? character = null;
        var internalName = (parameter as InternalName)?.Id ?? parameter as string ?? (parameter as ICharacter)?.InternalName.Id;
        if (!internalName.IsNullOrEmpty())
        {
            character = _characters.FirstOrDefault(c => c.InternalNameEquals(internalName));
            if (character is not null)
            {
                SetCharacter(character);
                return;
            }
        }


        if (_lastSelectedCharacter is null) return;
        character = _characters.FirstOrDefault(c => c.InternalNameEquals(_lastSelectedCharacter));
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


        var suitableCharacters = _gameService.QueryCharacters(query, minScore: 100, includeDisabledCharacters: true)
            .OrderByDescending(kv => kv.Value)
            .ToArray();

        if (suitableCharacters.Length == 0)
        {
            Suggestions.Add(new CharacterSearchResult
            {
                Name = "No results found",
                ImagePath = _imageHandlerService.PlaceholderImagePath
            });
            return;
        }

        foreach (var character in suitableCharacters.Select(kv => kv.Key))
            Suggestions.Add(CharacterSearchResult.FromCharacter(character, _imageHandlerService.PlaceholderImagePath));
    }


    public class SetSelectionArgs : EventArgs
    {
        public INameable? Character { get; }

        public bool NewCharacter { get; }

        public SetSelectionArgs(INameable? character)
        {
            Character = character;
        }

        public SetSelectionArgs(bool newCharacter)
        {
            NewCharacter = newCharacter;
        }
    }
}

public class CharacterSearchResult
{
    public string InternalName { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;

    public static CharacterSearchResult FromCharacter(ICharacter character, string placeHolderImage)
    {
        return new CharacterSearchResult
        {
            InternalName = character.InternalName,
            Name = character.DisplayName,
            ImagePath = character.ImageUri?.ToString() ?? placeHolderImage
        };
    }

    public override string ToString()
    {
        return Name;
    }
}