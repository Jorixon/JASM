﻿using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Models.CustomControlTemplates;
using GIMI_ModManager.WinUI.Models.Settings;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.ModHandling;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;

public partial class CharacterGalleryViewModel : ObservableRecipient, INavigationAware
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly CharacterSkinService _characterSkinService;
    private readonly ElevatorService _elevatorService;
    private readonly INavigationService _navigationService;
    private readonly IGameService _gameService;
    private readonly ILogger _logger;


    private ICategory? _category;
    private IModdableObject? _moddableObject;
    private ICharacterModList? _modList;
    private ICharacterSkin? _selectedSkin;

    public event EventHandler? Initialized;

    private readonly List<ModGridItemVm> _backendMods = new();
    public ObservableCollection<ModGridItemVm> Mods { get; } = new();
    public ObservableCollection<SelectCharacterTemplate> CharacterSkins { get; } = new();

    public ObservableCollection<SelectableModdableObjectVm> ModdableObjectVms { get; } = new();

    [ObservableProperty] private string _selectedSortingMethod;
    [ObservableProperty] private bool _sortByDescending;

    public bool MultipleCharacterSkins => CharacterSkins.Count > 1;

    public string ModdableObjectName
    {
        get
        {
            if (_selectedSkin is not null &&
                !_selectedSkin.InternalName.Id.Contains("default", StringComparison.OrdinalIgnoreCase))
                return _selectedSkin.DisplayName;

            return _moddableObject?.DisplayName ?? "Loading...";
        }
    }

    public Uri ModdableObjectImagePath =>
        _selectedSkin?.ImageUri ?? _moddableObject?.ImageUri ?? ModModel.PlaceholderImagePath;

    [ObservableProperty] private int _gridItemWidth;

    [ObservableProperty] private int _gridItemHeight;

    [ObservableProperty] private bool _isSingleSelection;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleViewCommand), nameof(ToggleModCommand),
        nameof(ToggleSingleSelectionCommand), nameof(SetHeightWidthCommand), nameof(OpenModFolderCommand),
        nameof(OpenModUrlCommand), nameof(NavigateToModObjectCommand), nameof(ToggleNavPaneCommand),
        nameof(DeleteModCommand))]
    private bool _isBusy;

    private bool _isNavigating;
    [ObservableProperty] private string _isInitializingMods = "true";


    [ObservableProperty] private string _searchText = string.Empty;


    [MemberNotNullWhen(false, nameof(_category), nameof(_moddableObject), nameof(_modList))]
    public bool IsNavigating
    {
        get => _isNavigating || _category is null || _moddableObject is null || _modList is null;
        set
        {
            if (value == _isNavigating) return;
            _isNavigating = value;
            OnPropertyChanged(string.Empty);
        }
    }

    private Task<CharacterGallerySettings> ReadCharacterGallerySettings() =>
        _localSettingsService.ReadOrCreateSettingAsync<CharacterGallerySettings>(CharacterGallerySettings.Key);

    private Task SaveCharacterGallerySettings(CharacterGallerySettings settings) =>
        _localSettingsService.SaveSettingAsync(CharacterGallerySettings.Key, settings);

    public CharacterGalleryViewModel(IGameService gameService,
        INavigationService navigationService,
        ISkinManagerService skinManagerService,
        ILocalSettingsService localSettingsService,
        CharacterSkinService characterSkinService,
        ElevatorService elevatorService, ILogger logger)
    {
        _skinManagerService = skinManagerService;
        _localSettingsService = localSettingsService;
        _characterSkinService = characterSkinService;
        _elevatorService = elevatorService;
        _logger = logger.ForContext<CharacterGalleryViewModel>();
        _navigationService = navigationService;
        _gameService = gameService;

        var settings = _localSettingsService.ReadSetting<CharacterGallerySettings>(CharacterGallerySettings.Key) ??
                       new CharacterGallerySettings();
        _gridItemHeight = settings.ItemHeight;
        _gridItemWidth = settings.ItemDesiredWidth;
        _isSingleSelection = settings.IsSingleSelection;
        _selectedSortingMethod = settings.SortingMethod ?? "Name";
        _sortByDescending = settings.SortByDescending;
        IsNavPaneVisible = settings.IsNavPaneOpen;
    }

    public async void OnNavigatedTo(object parameter)
    {
        var internalName = new InternalName("_");


        if (parameter is CharacterGridItemModel characterGridItemModel)
        {
            internalName = characterGridItemModel.Character.InternalName;
        }
        else if (parameter is INameable iInternalName)
        {
            internalName = iInternalName.InternalName;
        }
        else if (parameter is string internalNameString)
        {
            internalName = new InternalName(internalNameString);
        }
        else if (parameter is InternalName internalName1)
        {
            internalName = internalName1;
        }


        var moddableObject = _gameService.GetModdableObjectByIdentifier(internalName);

        if (moddableObject is null)
        {
            ErrorNavigateBack();
            return;
        }

        _category = moddableObject.ModCategory;
        _moddableObject = moddableObject;
        if (_moddableObject is ICharacter character)
        {
            _selectedSkin = character.Skins.First();
            foreach (var characterSkin in character.Skins)
            {
                CharacterSkins.Add(new SelectCharacterTemplate(characterSkin));
            }

            CharacterSkins.First().IsSelected = true;
        }

        OnPropertyChanged(nameof(ModdableObjectName));
        OnPropertyChanged(nameof(ModdableObjectImagePath));
        OnPropertyChanged(nameof(MultipleCharacterSkins));


        List<SelectableModdableObjectVm> moddableObjectVms = [];
        await Task.Run(() =>
        {
            var moddableObjects = _gameService.GetModdableObjects(_category).AsEnumerable();

            // Check for date support
            if (typeof(IDateSupport).IsAssignableFrom(_category.ModdableObjectType))
            {
                moddableObjects = moddableObjects.OrderByDescending(m => ((IDateSupport)m).ReleaseDate);
            }

            foreach (var modObject in moddableObjects)
            {
                var vm = new SelectableModdableObjectVm(modObject, NavigateToModObjectCommand);

                if (modObject.InternalNameEquals(_moddableObject))
                    vm.IsSelected = true;

                var modList = _skinManagerService.GetCharacterModList(modObject);
                if (!vm.IsSelected && modList.Mods.Count == 0)
                    continue;

                moddableObjectVms.Add(vm);
            }

            _modList = _skinManagerService.GetCharacterModList(_moddableObject);
        });

        moddableObjectVms.ForEach(ModdableObjectVms.Add);


        await ReloadModsAsync();

        IsNavigating = false;
        IsInitializingMods = "false";
        App.MainWindow.DispatcherQueue.TryEnqueue(() => Initialized?.Invoke(this, EventArgs.Empty));
    }

    private async Task ReloadModsAsync(CancellationToken cancellationToken = default)
    {
        if (_modList is null || _moddableObject is null)
            return;

        var mods = new List<CharacterSkinEntry>();
        await Task.Run(async () =>
        {
            if (_moddableObject is ICharacter { Skins.Count: > 1 } character)
            {
                var selectedSkin = _selectedSkin!;

                var skinEntries = _characterSkinService.GetCharacterSkinEntriesForSkinAsync(selectedSkin);

                await foreach (var skinEntry in skinEntries.ConfigureAwait(false))
                {
                    mods.Add(skinEntry);
                }
            }
            else
            {
                mods.AddRange(_modList.Mods);
            }

            _backendMods.Clear();

            foreach (var skinEntry in mods)
            {
                var modVm = await MapSkinEntryToModGridItemVm(skinEntry, cancellationToken);

                _backendMods.Add(modVm);
            }
        }, cancellationToken);


        ResetContent();
    }

    public async void OnSortComboBoxSelectionChanged(string sortingMethod)
    {
        SelectedSortingMethod = sortingMethod;
        ResetContent();

        var settings = await ReadCharacterGallerySettings();
        settings.SortingMethod = sortingMethod;
        await SaveCharacterGallerySettings(settings).ConfigureAwait(false);
    }

    public async void OnSortToggleButtonChanged(bool sortByDescending)
    {
        SortByDescending = sortByDescending;
        ResetContent();

        var settings = await ReadCharacterGallerySettings();
        settings.SortByDescending = sortByDescending;
        await SaveCharacterGallerySettings(settings).ConfigureAwait(false);
    }

    public void OnSearchBoxTextChanged(string? searchText)
    {
        SearchText = searchText.IsNullOrEmpty() ? string.Empty : searchText;
        ResetContent();
    }

    private void ResetContent()
    {
        if (_modList is null || _moddableObject is null)
            return;

        var gridItemVms = SearchText.IsNullOrEmpty()
            ? _backendMods.ToList()
            : _backendMods.Where(m
                => m.FolderName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   m.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   m.Author.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();


        switch (SelectedSortingMethod)
        {
            case "Name":
                if (SortByDescending)
                    gridItemVms.Sort((a, b) => string.Compare(b.Name, a.Name, StringComparison.Ordinal));
                else
                    gridItemVms.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                break;
            case "DateAdded":
                if (SortByDescending)
                    gridItemVms.Sort((a, b) =>
                    {
                        var dateComparison = b.DateAdded.CompareTo(a.DateAdded);
                        return dateComparison != 0 ? dateComparison : string.Compare(b.Name, a.Name, StringComparison.Ordinal);
                    });
                else
                    gridItemVms.Sort((a, b) =>
                    {
                        var dateComparison = a.DateAdded.CompareTo(b.DateAdded);
                        return dateComparison != 0 ? dateComparison : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                    });
                break;
            case "FolderName":
                if (SortByDescending)
                    gridItemVms.Sort((a, b) => string.Compare(b.FolderName, a.FolderName, StringComparison.Ordinal));
                else
                    gridItemVms.Sort((a, b) => string.Compare(a.FolderName, b.FolderName, StringComparison.Ordinal));
                break;
            default:
                gridItemVms.Sort((a, b) =>
                {
                    var dateComparison = b.DateAdded.CompareTo(a.DateAdded);
                    return dateComparison != 0 ? dateComparison : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                });
                break;
        }

        var enabledMods = gridItemVms.Where(m => m.IsEnabled);

        foreach (var mod in enabledMods.Reverse())
        {
            gridItemVms.Remove(mod);
            gridItemVms.Insert(0, mod);
        }

        var currentMods = Mods.ToArray();

        foreach (var mod in currentMods)
        {
            if (gridItemVms.Contains(mod))
                continue;

            Mods.Remove(mod);
        }

        foreach (var mod in gridItemVms)
        {
            if (currentMods.Contains(mod))
                continue;

            Mods.Add(mod);
        }

        Debug.Assert(Mods.Count == gridItemVms.Count);

        foreach (var mod in gridItemVms)
        {
            var newIndex = gridItemVms.IndexOf(mod);
            var oldIndex = Mods.IndexOf(mod);


            if (newIndex == Mods.IndexOf(mod)) continue;
            if (oldIndex < 0 || oldIndex >= Mods.Count || newIndex < 0 || newIndex >= Mods.Count)
                throw new ArgumentOutOfRangeException();

            Mods.RemoveAt(oldIndex);
            Mods.Insert(newIndex, mod);
        }
    }

    private async Task UpdateGridItemAsync(CharacterSkinEntry skinEntry)
    {
        var modVm = await MapSkinEntryToModGridItemVm(skinEntry);

        var existingModVm = _backendMods.FirstOrDefault(m => m.Id == modVm.Id);

        if (existingModVm is not null)
            Update(existingModVm, modVm);
    }

    private static void Update(ModGridItemVm oldItem, ModGridItemVm newItem)
    {
        oldItem.FolderPath = newItem.FolderPath;
        oldItem.FolderName = newItem.FolderName;
        oldItem.IsEnabled = newItem.IsEnabled;
    }


    private async Task<ModGridItemVm> MapSkinEntryToModGridItemVm(CharacterSkinEntry skinEntry,
        CancellationToken cancellationToken = default)
    {
        var modModel = ModModel.FromMod(skinEntry);

        var modSettings = await skinEntry.Mod.Settings.TryReadSettingsAsync(false, cancellationToken)
            .ConfigureAwait(false);

        if (modSettings is not null)
            modModel.WithModSettings(modSettings);

        if (skinEntry.Mod.KeySwaps is not null)
        {
            var keySwaps = await skinEntry.Mod.KeySwaps.ReadKeySwapConfiguration(cancellationToken)
                .ConfigureAwait(false);

            modModel.SetKeySwaps(keySwaps);
        }

        return new ModGridItemVm(modModel, ToggleModCommand, OpenModFolderCommand, OpenModUrlCommand, DeleteModCommand);
    }


    public void OnNavigatedFrom()
    {
    }


    private void ErrorNavigateBack()
    {
        Task.Run(async () =>
        {
            await Task.Delay(500);
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (_navigationService.CanGoBack)
                    _navigationService.GoBack();
                else
                    _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!);
            });
        });
    }
}