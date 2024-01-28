using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkitWrapper;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Models.Settings;
using GIMI_ModManager.WinUI.Models.ViewModels;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class CharactersViewModel : ObservableRecipient, INavigationAware
{
    private readonly IGameService _gameService;
    private readonly ILogger _logger;
    private readonly INavigationService _navigationService;
    private readonly ISkinManagerService _skinManagerService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ModDragAndDropService _modDragAndDropService;
    private readonly ModNotificationManager _modNotificationManager;
    private readonly ModCrawlerService _modCrawlerService;
    private readonly ModSettingsService _modSettingsService;
    private readonly ModUpdateAvailableChecker _modUpdateAvailableChecker;

    public readonly GenshinProcessManager GenshinProcessManager;
    public readonly ThreeDMigtoProcessManager ThreeDMigtoProcessManager;

    public readonly string StartGameIcon;
    public readonly string ShortGameName;
    public NotificationManager NotificationManager { get; }
    public ElevatorService ElevatorService { get; }

    public OverviewDockPanelVM DockPanelVM { get; }


    private IReadOnlyList<IModdableObject> _characters = new List<IModdableObject>();

    private IReadOnlyList<CharacterGridItemModel> _backendCharacters = new List<CharacterGridItemModel>();
    public ObservableCollection<CharacterGridItemModel> SuggestionsBox { get; } = new();

    public ObservableCollection<CharacterGridItemModel> Characters { get; } = new();

    private string _searchText = string.Empty;

    private readonly Dictionary<FilterType, GridFilter> _filters = new();


    public ObservableCollection<SortingMethod> SortingMethods { get; } =
        new() { };

    [ObservableProperty] private SortingMethod _selectedSortingMethod;
    [ObservableProperty] private bool _sortByDescending;

    [ObservableProperty] private bool _canCheckForUpdates = false;

    [ObservableProperty] private Uri? _gameBananaLink;

    [ObservableProperty] private string _categoryPageTitle = string.Empty;
    [ObservableProperty] private string _modToggleText = string.Empty;
    [ObservableProperty] private string _searchBoxPlaceHolder = string.Empty;


    private bool _isNavigating = true;

    public CharactersViewModel(IGameService gameService, ILogger logger, INavigationService navigationService,
        ISkinManagerService skinManagerService, ILocalSettingsService localSettingsService,
        NotificationManager notificationManager, ElevatorService elevatorService,
        GenshinProcessManager genshinProcessManager, ThreeDMigtoProcessManager threeDMigtoProcessManager,
        ModDragAndDropService modDragAndDropService, ModNotificationManager modNotificationManager,
        ModCrawlerService modCrawlerService, ModSettingsService modSettingsService,
        ModUpdateAvailableChecker modUpdateAvailableChecker)
    {
        _gameService = gameService;
        _logger = logger.ForContext<CharactersViewModel>();
        _navigationService = navigationService;
        _skinManagerService = skinManagerService;
        _localSettingsService = localSettingsService;
        NotificationManager = notificationManager;
        ElevatorService = elevatorService;
        GenshinProcessManager = genshinProcessManager;
        ThreeDMigtoProcessManager = threeDMigtoProcessManager;
        _modDragAndDropService = modDragAndDropService;
        _modNotificationManager = modNotificationManager;
        _modCrawlerService = modCrawlerService;
        _modSettingsService = modSettingsService;
        _modUpdateAvailableChecker = modUpdateAvailableChecker;

        ElevatorService.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ElevatorService.ElevatorStatus))
                RefreshModsInGameCommand.NotifyCanExecuteChanged();
        };

        _modNotificationManager.OnModNotification += (_, _) =>
            App.MainWindow.DispatcherQueue.EnqueueAsync(RefreshNotificationsAsync);

        DockPanelVM = new OverviewDockPanelVM();
        StartGameIcon = _gameService.GameIcon;
        ShortGameName = "Start " + _gameService.GameShortName;
        GameBananaLink = _gameService.GameBananaUrl;

        CanCheckForUpdates = _modUpdateAvailableChecker.IsReady;
        _modUpdateAvailableChecker.OnUpdateCheckerEvent += (_, _) =>
        {
            App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                CanCheckForUpdates = _modUpdateAvailableChecker.IsReady;
                return Task.CompletedTask;
            });
        };
    }

    private void FilterElementSelected(object? sender, FilterElementSelectedArgs e)
    {
        if (_category.ModCategory != ModCategory.Character)
            return;

        if (e.InternalElementNames.Length == 0)
        {
            _filters.Remove(FilterType.Element);
            ResetContent();
            return;
        }

        _filters[FilterType.Element] = new GridFilter(character =>
            e.InternalElementNames.Contains(((ICharacter)character.Character).Element.InternalName));
        ResetContent();
    }

    private CharacterGridItemModel NoCharacterFound =>
        new(new Character("None", $"No {_category.DisplayNamePlural} Found..."));

    public void AutoSuggestBox_TextChanged(string text)
    {
        _searchText = text;
        SuggestionsBox.Clear();

        if (string.IsNullOrWhiteSpace(_searchText))
        {
            SuggestionsBox.Clear();
            _filters.Remove(FilterType.Search);
            ResetContent();
            return;
        }

        var suitableItems = _gameService.QueryModdableObjects(text, category: _category, minScore: 100)
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(x => new CharacterGridItemModel(x.Key))
            .ToList();


        if (!suitableItems.Any())
        {
            SuggestionsBox.Add(NoCharacterFound);
            _filters.Remove(FilterType.Search);
            ResetContent();
            return;
        }

        suitableItems.ForEach(suggestion => SuggestionsBox.Add(suggestion));

        _filters[FilterType.Search] = new GridFilter(character => SuggestionsBox.Contains(character));
        ResetContent();
    }


    public bool SuggestionBox_Chosen(CharacterGridItemModel? character)
    {
        if (character == NoCharacterFound || character is null)
            return false;


        _navigationService.SetListDataItemForNextConnectedAnimation(character);
        _navigationService.NavigateTo(typeof(CharacterDetailsViewModel).FullName!, character);
        return true;
    }

    private void ResetContent()
    {
        if (_isNavigating) return;

        var filteredCharacters = FilterCharacters(_backendCharacters);
        var sortedCharacters = SelectedSortingMethod.Sort(filteredCharacters, SortByDescending).ToList();

        var charactersToRemove = Characters.Except(sortedCharacters).ToArray();

        if (Characters.Count == 0)
        {
            foreach (var characterGridItemModel in sortedCharacters)
            {
                Characters.Add(characterGridItemModel);
            }

            return;
        }

        var missingCharacters = sortedCharacters.Except(Characters);

        foreach (var characterGridItemModel in missingCharacters)
        {
            Characters.Add(characterGridItemModel);
        }

        foreach (var characterGridItemModel in sortedCharacters)
        {
            var newIndex = sortedCharacters.IndexOf(characterGridItemModel);
            var oldIndex = Characters.IndexOf(characterGridItemModel);
            //Check if character is already at the right index

            if (newIndex == Characters.IndexOf(characterGridItemModel)) continue;

            if (oldIndex < 0 || oldIndex >= Characters.Count || newIndex < 0 || newIndex >= Characters.Count)
                throw new ArgumentOutOfRangeException();

            Characters.RemoveAt(oldIndex);
            Characters.Insert(newIndex, characterGridItemModel);
        }


        foreach (var characterGridItemModel in charactersToRemove)
        {
            Characters.Remove(characterGridItemModel);
        }


        Debug.Assert(Characters.Distinct().Count() == Characters.Count,
            $"Characters.Distinct().Count(): {Characters.Distinct().Count()} != Characters.Count: {Characters.Count}\n\t" +
            $"Duplicate characters found in character overview");
    }

    private IEnumerable<CharacterGridItemModel> FilterCharacters(
        IReadOnlyList<CharacterGridItemModel> characters)
    {
        if (!_filters.Any())
        {
            foreach (var characterGridItemModel in characters)
            {
                yield return characterGridItemModel;
            }
        }

        var modsFoundForFilter = new Dictionary<FilterType, IEnumerable<CharacterGridItemModel>>();


        foreach (var filter in _filters)
        {
            modsFoundForFilter.Add(filter.Key, filter.Value.Filter(characters));
        }


        IEnumerable<CharacterGridItemModel>? intersectedMods = null;

        foreach (var kvp in modsFoundForFilter)
        {
            intersectedMods = intersectedMods == null
                ? kvp.Value
                : intersectedMods.Intersect(kvp.Value);
        }


        foreach (var characterGridItemModel in intersectedMods ?? Array.Empty<CharacterGridItemModel>())
        {
            yield return characterGridItemModel;
        }
    }

    private ICategory _category = null!;

    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is not ICategory category)
        {
            _logger.Error("Invalid parameter type {ParameterType}", parameter?.GetType().FullName);
            category = _gameService.GetCategories().First();
        }


        _category = category;
        CategoryPageTitle = $"{category.DisplayName} Overview";
        ModToggleText = $"Show only {category.DisplayNamePlural} with Mods";
        SearchBoxPlaceHolder = $"Search {category.DisplayNamePlural}...";


        var characters = _gameService.GetModdableObjects(_category);

        var firstType = characters.FirstOrDefault()?.GetType();
        if (characters.Any(ch => ch.GetType() != firstType))
            throw new InvalidOperationException("Characters must be of the same type");

        var others =
            characters.FirstOrDefault(ch =>
                ch.InternalName.Id.Contains("Others", StringComparison.OrdinalIgnoreCase));
        if (others is not null) // Add to front
        {
            characters.Remove(others);
            characters.Insert(0, others);
        }

        var gliders =
            characters.FirstOrDefault(ch => ch.InternalNameEquals(_gameService.GlidersCharacterInternalName));
        if (gliders is not null) // Add to end
        {
            characters.Remove(gliders);
            characters.Add(gliders);
        }

        var weapons =
            characters.FirstOrDefault(ch => ch.InternalNameEquals(_gameService.WeaponsCharacterInternalName));
        if (weapons is not null) // Add to end
        {
            characters.Remove(weapons);
            characters.Add(weapons);
        }


        _characters = characters;

        characters = new List<IModdableObject>(_characters);

        var pinnedCharactersOptions = await ReadCharacterSettings();

        var backendCharacters = new List<CharacterGridItemModel>();
        foreach (var pinedCharacterId in pinnedCharactersOptions.PinedCharacters)
        {
            var character = characters.FirstOrDefault(x => x.InternalNameEquals(pinedCharacterId));
            if (character is not null)
            {
                backendCharacters.Add(new CharacterGridItemModel(character) { IsPinned = true });
                characters.Remove(character);
            }
        }

        foreach (var hiddenCharacterId in pinnedCharactersOptions.HiddenCharacters)
        {
            var character = characters.FirstOrDefault(x => x.InternalNameEquals(hiddenCharacterId));
            if (character is not null)
            {
                backendCharacters.Add(new CharacterGridItemModel(character) { IsHidden = true });
                characters.Remove(character);
            }
        }

        // Add rest of characters
        foreach (var character in characters)
        {
            backendCharacters.Add(new CharacterGridItemModel(character));
        }

        _backendCharacters = backendCharacters;

        foreach (var characterGridItemModel in _backendCharacters)
        {
            var modList = _skinManagerService.GetCharacterModList(characterGridItemModel.Character);
            characterGridItemModel.ModCount = modList.Mods.Count;
            characterGridItemModel.HasMods = characterGridItemModel.ModCount > 0;
        }

        InitializeSorters();

        if (typeof(ICharacter).IsAssignableFrom(firstType))
        {
            var backendCharactersList = _backendCharacters.Select(x => x.Character).Cast<ICharacter>().ToList();
            var distinctReleaseDates = backendCharactersList
                .Where(ch => ch.ReleaseDate != default)
                .DistinctBy(ch => ch.ReleaseDate)
                .Count();

            if (distinctReleaseDates == 1 &&
                SortingMethods.FirstOrDefault(x => x.SortingMethodType == Sorter.ReleaseDateSortName) is
                    { } releaseDateSortingMethod)
            {
                SortingMethods.Remove(releaseDateSortingMethod);
            }

            DockPanelVM.Initialize();
            DockPanelVM.FilterElementSelected += FilterElementSelected;
        }


        // Add notifications
        await RefreshNotificationsAsync();

        // Character Ids where more than 1 skin is enabled
        var charactersWithMultipleMods = _skinManagerService.CharacterModLists
            .Where(x => x.Mods.Count(mod => mod.IsEnabled) > 1);

        var charactersWithMultipleActiveSkins = new List<string>();
        foreach (var modList in charactersWithMultipleMods)
        {
            if (_gameService.IsMultiMod(modList.Character))
                continue;

            if (modList.Character is ICharacter character)
            {
                var addWarning = false;
                var subSkinsFound = new List<ICharacterSkin>();
                foreach (var characterSkinEntry in modList.Mods)
                {
                    if (!characterSkinEntry.IsEnabled) continue;

                    var subSkin = _modCrawlerService.GetFirstSubSkinRecursive(characterSkinEntry.Mod.FullPath);
                    var modSettingsResult = await _modSettingsService.GetSettingsAsync(characterSkinEntry.Id);


                    var mod = ModModel.FromMod(characterSkinEntry);


                    if (modSettingsResult.IsT0)
                        mod.WithModSettings(modSettingsResult.AsT0);

                    if (!mod.CharacterSkinOverride.IsNullOrEmpty())
                        subSkin = _gameService.GetCharacterByIdentifier(character.InternalName)?.Skins
                            .FirstOrDefault(x => SkinVM.FromSkin(x).InternalNameEquals(mod.CharacterSkinOverride));

                    if (subSkin is null)
                        continue;


                    if (subSkinsFound.All(foundSubSkin =>
                            !subSkin.InternalNameEquals(foundSubSkin)))
                    {
                        subSkinsFound.Add(subSkin);
                        continue;
                    }


                    addWarning = true;
                    break;
                }

                if (addWarning || subSkinsFound.Count > 1 && character.Skins.Count == 1)
                    charactersWithMultipleActiveSkins.Add(modList.Character.InternalName);
            }
            else if (modList.Mods.Count(modEntry => modEntry.IsEnabled) >= 2)
            {
                charactersWithMultipleActiveSkins.Add(modList.Character.InternalName);
            }
        }


        foreach (var characterGridItemModel in _backendCharacters.Where(x =>
                     charactersWithMultipleActiveSkins.Contains(x.Character.InternalName)))
        {
            if (_gameService.IsMultiMod(characterGridItemModel.Character))
                continue;

            characterGridItemModel.Warning = true;
        }


        if (pinnedCharactersOptions.ShowOnlyCharactersWithMods)
        {
            _filters[FilterType.HasMods] = new GridFilter(characterGridItem =>
                _skinManagerService.GetCharacterModList(characterGridItem.Character).Mods.Any());
        }


        // ShowOnlyModsCharacters
        var settings =
            await _localSettingsService
                .ReadOrCreateSettingAsync<CharacterOverviewSettings>(CharacterOverviewSettings.GetKey(_category));
        if (settings.ShowOnlyCharactersWithMods)
        {
            ShowOnlyCharactersWithMods = true;
            _filters[FilterType.HasMods] = new GridFilter(characterGridItem =>
                _skinManagerService.GetCharacterModList(characterGridItem.Character).Mods.Any());
        }

        SortByDescending = settings.SortByDescending;

        var sorter = SortingMethods.FirstOrDefault(x => x.SortingMethodType == settings.SortingMethod);

        SelectedSortingMethod = sorter ?? SortingMethods.First();


        _isNavigating = false;
        ResetContent();
    }

    private async Task RefreshNotificationsAsync()
    {
        foreach (var character in _characters)
        {
            var characterGridItemModel = FindCharacterByInternalName(character.InternalName);
            if (characterGridItemModel is null) continue;

            var characterMods = _skinManagerService.GetCharacterModList(character).Mods;

            var notifications = new List<ModNotification>();
            foreach (var characterSkinEntry in characterMods)
            {
                var modNotification = await _modNotificationManager.GetNotificationsForModAsync(characterSkinEntry.Id);
                notifications.AddRange(modNotification);
            }

            if (!notifications.Any())
            {
                characterGridItemModel.Notification = false;
                characterGridItemModel.NotificationType = AttentionType.None;
            }

            foreach (var modNotification in notifications)
            {
                if (modNotification.AttentionType == AttentionType.Added ||
                    modNotification.AttentionType == AttentionType.UpdateAvailable)
                {
                    characterGridItemModel.Notification = true;
                    characterGridItemModel.NotificationType = modNotification.AttentionType;
                }
            }
        }
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private void CharacterClicked(CharacterGridItemModel characterModel)
    {
        _navigationService.SetListDataItemForNextConnectedAnimation(characterModel);
        _navigationService.NavigateTo(typeof(CharacterDetailsViewModel).FullName!, characterModel);
    }

    [ObservableProperty] private bool _showOnlyCharactersWithMods = false;

    [RelayCommand]
    private async Task ShowCharactersWithModsAsync()
    {
        if (ShowOnlyCharactersWithMods)
        {
            ShowOnlyCharactersWithMods = false;

            _filters.Remove(FilterType.HasMods);

            ResetContent();
            var settingss = await ReadCharacterSettings();


            settingss.ShowOnlyCharactersWithMods = ShowOnlyCharactersWithMods;

            await SaveCharacterSettings(settingss);

            return;
        }

        _filters[FilterType.HasMods] = new GridFilter(characterGridItem =>
            _skinManagerService.GetCharacterModList(characterGridItem.Character.InternalName).Mods.Any());

        ShowOnlyCharactersWithMods = true;

        ResetContent();

        var settings = await ReadCharacterSettings();

        settings.ShowOnlyCharactersWithMods = ShowOnlyCharactersWithMods;

        await SaveCharacterSettings(settings).ConfigureAwait(false);
    }


    [ObservableProperty] private string _pinText = DefaultPinText;

    [ObservableProperty] private string _pinGlyph = DefaultPinGlyph;

    const string DefaultPinGlyph = "\uE718";
    const string DefaultPinText = "Pin To Top";
    const string DefaultUnpinGlyph = "\uE77A";
    const string DefaultUnpinText = "Unpin Character";

    public void OnRightClickContext(CharacterGridItemModel clickedCharacter)
    {
        ClearNotificationsCommand.CanExecute(clickedCharacter);
        if (clickedCharacter.IsPinned)
        {
            PinText = DefaultUnpinText;
            PinGlyph = DefaultUnpinGlyph;
        }
        else
        {
            PinText = DefaultPinText;
            PinGlyph = DefaultPinGlyph;
        }
    }

    [RelayCommand]
    private async Task PinCharacterAsync(CharacterGridItemModel character)
    {
        if (character.IsPinned)
        {
            character.IsPinned = false;

            ResetContent();

            var settingss = await ReadCharacterSettings();

            var pinedCharacterss = _backendCharacters.Where(ch => ch.IsPinned)
                .Select(ch => ch.Character.InternalName.Id)
                .ToArray();
            settingss.PinedCharacters = pinedCharacterss;
            await SaveCharacterSettings(settingss);
            return;
        }


        character.IsPinned = true;

        ResetContent();

        var settings = await ReadCharacterSettings();

        var pinedCharacters = _backendCharacters
            .Where(ch => ch.IsPinned)
            .Select(ch => ch.Character.InternalName.Id)
            .ToArray();

        settings.PinedCharacters = pinedCharacters;

        await SaveCharacterSettings(settings).ConfigureAwait(false);
    }


    private bool canClearNotifications(CharacterGridItemModel? character)
    {
        return character?.Notification ?? false;
    }

    [RelayCommand(CanExecute = nameof(canClearNotifications))]
    private async Task ClearNotificationsAsync(CharacterGridItemModel character)
    {
        await _modNotificationManager.ClearModNotificationsAsync(character.Character.InternalName);
        await RefreshNotificationsAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private void HideCharacter(CharacterGridItemModel character)
    {
        NotImplemented.Show("Hiding characters is not implemented yet");
    }

    private Task<CharacterOverviewSettings> ReadCharacterSettings() =>
        _localSettingsService
            .ReadOrCreateSettingAsync<CharacterOverviewSettings>(CharacterOverviewSettings.GetKey(_category));

    private Task SaveCharacterSettings(CharacterOverviewSettings settings) =>
        _localSettingsService.SaveSettingAsync(CharacterOverviewSettings.GetKey(_category), settings);


    private async Task InternalStartAsync(IProcessManager processManager)
    {
        _logger.Debug("Starting {ProcessName}", processManager.ProcessName);
        processManager.CheckStatus();

        if (processManager.ProcessStatus == ProcessStatus.NotInitialized)
        {
            string? processPath = null;
            try
            {
                processPath = await processManager.PickProcessPathAsync(App.MainWindow);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error picking process path");
                NotificationManager.ShowNotification($"Error picking process path, ErrorCode: {e.HResult}", e.Message,
                    TimeSpan.FromSeconds(10));
            }

            if (processPath is null) return;
            await processManager.SetPath(Path.GetFileName(processPath), processPath);
        }

        if (processManager.ProcessStatus == ProcessStatus.NotRunning)
        {
            processManager.StartProcess();
            if (processManager.ErrorMessage is not null)
                NotificationManager.ShowNotification($"Failed to start {processManager.ProcessName}",
                    processManager.ErrorMessage,
                    TimeSpan.FromSeconds(5));
        }
    }


    [RelayCommand]
    private async Task Start3DmigotoAsync() => await InternalStartAsync(ThreeDMigtoProcessManager);


    [RelayCommand]
    private async Task StartGenshinAsync() => await InternalStartAsync(GenshinProcessManager);

    private bool CanRefreshModsInGame()
    {
        return ElevatorService.ElevatorStatus == ElevatorStatus.Running;
    }

    [RelayCommand(CanExecute = nameof(CanRefreshModsInGame))]
    private async Task RefreshModsInGameAsync()
    {
        _logger.Debug("Refreshing Mods In Game");
        await ElevatorService.RefreshGenshinMods();
    }

    [ObservableProperty] private bool _isAddingMod = false;

    public async Task ModDroppedOnCharacterAsync(CharacterGridItemModel characterGridItemModel,
        IReadOnlyList<IStorageItem> storageItems)
    {
        if (IsAddingMod)
        {
            _logger.Warning("Already adding mod");
            return;
        }

        var modList =
            _skinManagerService.CharacterModLists.FirstOrDefault(x =>
                x.Character.InternalNameEquals(characterGridItemModel.Character));
        if (modList is null)
        {
            _logger.Warning("No mod list found for character {Character}",
                characterGridItemModel.Character.InternalName);
            return;
        }

        try
        {
            IsAddingMod = true;
            await _modDragAndDropService.AddStorageItemFoldersAsync(modList, storageItems);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error adding mod");
            NotificationManager.ShowNotification("Error adding mod", e.Message, TimeSpan.FromSeconds(10));
        }
        finally
        {
            IsAddingMod = false;
        }
    }


    private CharacterGridItemModel? FindCharacterByInternalName(string internalName)
    {
        return _backendCharacters.FirstOrDefault(x =>
            x.Character.InternalNameEquals(internalName));
    }


    [RelayCommand]
    private async Task SortBy(IEnumerable<SortingMethod> methodTypes)
    {
        if (_isNavigating) return;
        var sortingMethodType = methodTypes.First();

        ResetContent();

        var settings = await ReadCharacterSettings();
        settings.SortingMethod = sortingMethodType.SortingMethodType;
        await SaveCharacterSettings(settings).ConfigureAwait(false);
    }


    [RelayCommand]
    private async Task InvertSorting()
    {
        ResetContent();

        var settings = await ReadCharacterSettings();
        settings.SortByDescending = SortByDescending;
        await SaveCharacterSettings(settings).ConfigureAwait(false);
    }

    [RelayCommand]
    private void CheckForUpdatesForCharacter(object? characterGridItemModel)
    {
        if (characterGridItemModel is not CharacterGridItemModel character)
            return;

        var modList = _skinManagerService.GetCharacterModList(character.Character);
        if (modList is null)
        {
            _logger.Warning("No mod list found for character {Character}", character.Character.InternalName);
            return;
        }

        var check = ModCheckRequest.ForCharacter(character.Character);

        _modUpdateAvailableChecker.CheckNow(check.WithIgnoreLastChecked());
    }


    private void InitializeSorters()
    {
        var lastCharacters = new List<CharacterGridItemModel>
        {
            FindCharacterByInternalName(_gameService.GlidersCharacterInternalName)!,
            FindCharacterByInternalName(_gameService.WeaponsCharacterInternalName)!
        };

        var othersCharacter = _backendCharacters.FirstOrDefault(ch =>
            ch.Character.InternalName.Id.Contains("Others", StringComparison.OrdinalIgnoreCase));

        var alphabetical = new SortingMethod(Sorter.Alphabetical(), othersCharacter, lastCharacters);
        SortingMethods.Add(alphabetical);

        var byModCount = new SortingMethod(Sorter.ModCount(), othersCharacter, lastCharacters);
        SortingMethods.Add(byModCount);

        if (_category.ModCategory == ModCategory.Character)
        {
            SortingMethods.Add(new SortingMethod(Sorter.ReleaseDate(), othersCharacter, lastCharacters));
            SortingMethods.Add(new SortingMethod(Sorter.Rarity(), othersCharacter, lastCharacters));
        }

        if (_category.ModCategory == ModCategory.Weapons)
        {
            SortingMethods.Add(new SortingMethod(Sorter.Rarity(), othersCharacter, lastCharacters));
        }
    }
}

public sealed class GridFilter
{
    private readonly Func<CharacterGridItemModel, bool> _filter;

    public GridFilter(Func<CharacterGridItemModel, bool> filter)
    {
        _filter = filter;
    }

    public bool Filter(CharacterGridItemModel character)
    {
        return _filter(character);
    }

    public IEnumerable<CharacterGridItemModel> Filter(IEnumerable<CharacterGridItemModel> characters)
    {
        return characters.Where(Filter);
    }
}

public enum FilterType
{
    Element,
    Search,
    HasMods
}

public sealed class SortingMethod
{
    public string SortingMethodType => _sorter.SortingMethodType;

    private readonly Sorter _sorter;

    private readonly CharacterGridItemModel[] _lastCharacters;
    private readonly CharacterGridItemModel? _firstCharacter;

    public SortingMethod(Sorter sortingMethodType, CharacterGridItemModel? firstCharacter = null,
        ICollection<CharacterGridItemModel>? lastCharacters = null)
    {
        _sorter = sortingMethodType;
        _lastCharacters = lastCharacters?.ToArray() ?? Array.Empty<CharacterGridItemModel>();
        _firstCharacter = firstCharacter;
    }

    public IEnumerable<CharacterGridItemModel> Sort(IEnumerable<CharacterGridItemModel> characters, bool isDescending)
    {
        IEnumerable<CharacterGridItemModel> sortedCharacters = null!;

        sortedCharacters = _sorter.Sort(characters, isDescending).Cast<CharacterGridItemModel>();

        var returnCharactersList = sortedCharacters.ToList();

        var modifiableCharacters = new List<CharacterGridItemModel>(returnCharactersList);

        var index = 0;
        foreach (var pinnedCharacter in modifiableCharacters.Where(x => x.IsPinned))
        {
            returnCharactersList.Remove(pinnedCharacter);
            returnCharactersList.Insert(index, pinnedCharacter);
            index++;
        }

        foreach (var characterGridItemModel in modifiableCharacters.Intersect(_lastCharacters))
        {
            if (characterGridItemModel.IsPinned) continue;
            returnCharactersList.Remove(characterGridItemModel);
            returnCharactersList.Add(characterGridItemModel);
        }

        if (_firstCharacter is not null)
        {
            returnCharactersList.Remove(_firstCharacter);
            returnCharactersList.Insert(0, _firstCharacter);
        }

        return returnCharactersList;
    }

    public override string ToString()
    {
        return SortingMethodType;
    }
}

// I originally tried to do this with type support, but it turned into quite a 'generic' mess
// So I decided to go with a more 'hardcoded' approach and casting values when doing the comparison
public sealed class Sorter
{
    public string SortingMethodType { get; }
    private readonly SortFunc _firstSortFunc;

    private readonly AdditionalSortFunc? _secondSortFunc;

    private readonly AdditionalSortFunc? _thirdSortFunc;


    private delegate IOrderedEnumerable<CharacterGridItemModel> SortFunc(IEnumerable<CharacterGridItemModel> characters,
        bool isDescending);

    private delegate IOrderedEnumerable<CharacterGridItemModel> AdditionalSortFunc(
        IOrderedEnumerable<CharacterGridItemModel> characters,
        bool isDescending);

    private Sorter(string sortingMethodType, SortFunc firstSortFunc, AdditionalSortFunc? secondSortFunc = null,
        AdditionalSortFunc? thirdSortFunc = null)
    {
        SortingMethodType = sortingMethodType;
        _firstSortFunc = firstSortFunc;
        _secondSortFunc = secondSortFunc;
        _thirdSortFunc = thirdSortFunc;
    }

    public IEnumerable<CharacterGridItemModel> Sort(IEnumerable<CharacterGridItemModel> characters, bool isDescending)
    {
        var sorted = _firstSortFunc(characters, isDescending);

        if (_secondSortFunc is not null)
            sorted = _secondSortFunc(sorted, isDescending);

        if (_thirdSortFunc is not null)
            sorted = _thirdSortFunc(sorted, isDescending);

        return sorted;
    }


    public const string AlphabeticalSortName = "Alphabetical";

    // TODO: These can be a static property
    public static Sorter Alphabetical()
    {
        return new Sorter
        (
            AlphabeticalSortName,
            (characters, isDescending) =>
                isDescending
                    ? characters.OrderByDescending(x => (x.Character.DisplayName))
                    : characters.OrderBy(x => (x.Character.DisplayName)
                    ));
    }


    public const string ReleaseDateSortName = "Release Date";

    // TODO: IDateSupport interface
    public static Sorter ReleaseDate()
    {
        return new Sorter
        (
            ReleaseDateSortName,
            (characters, isDescending) =>
                !isDescending
                    ? characters.OrderByDescending(x => ((ICharacter)x.Character).ReleaseDate)
                    : characters.OrderBy(x => ((ICharacter)x.Character).ReleaseDate),
            (characters, _) =>
                characters.ThenBy(x => (x.Character.DisplayName)
                ));
    }

    public const string RaritySortName = "Rarity";

    public static Sorter Rarity()
    {
        return new Sorter
        (
            RaritySortName,
            (characters, isDescending) =>
                !isDescending
                    ? characters.OrderByDescending(x => ((IRarity)x.Character).Rarity)
                    : characters.OrderBy(x => ((IRarity)x.Character).Rarity),
            (characters, _) =>
                characters.ThenBy(x => (x.Character.DisplayName)
                ));
    }

    public const string ModCountSortName = "Mod Count";

    public static Sorter ModCount()
    {
        return new Sorter
        (
            ModCountSortName,
            (characters, isDescending) =>
                !isDescending
                    ? characters.OrderByDescending(x => (x.ModCount))
                    : characters.OrderBy(x => (x.ModCount)),
            (characters, _) =>
                characters.ThenBy(x => (x.Character.DisplayName)
                ));
    }


    //sortedCharacters = Sort(characters, x => x.Character.Rarity, !IsDescending,
    //sortSecondBy: x => x.Character.ReleaseDate, !IsDescending,
    //sortThirdBy: x => x.Character.DisplayName);
}