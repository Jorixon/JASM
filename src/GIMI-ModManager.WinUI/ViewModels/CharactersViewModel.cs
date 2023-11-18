using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
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


    private IReadOnlyList<ICharacter> _characters = new List<ICharacter>();

    private IReadOnlyList<CharacterGridItemModel> _backendCharacters = new List<CharacterGridItemModel>();
    public ObservableCollection<CharacterGridItemModel> SuggestionsBox { get; } = new();

    public ObservableCollection<CharacterGridItemModel> Characters { get; } = new();

    private string _searchText = string.Empty;

    private Dictionary<FilterType, GridFilter> _filters = new();


    public ObservableCollection<SortingMethodType> SortingMethods { get; } =
        new() { SortingMethodType.Alphabetical, SortingMethodType.ReleaseDate, SortingMethodType.Rarity };

    private SortingMethod _sortingMethod = null!;
    [ObservableProperty] private SortingMethodType _selectedSortingMethod = SortingMethodType.Alphabetical;
    [ObservableProperty] private bool _sortByDescending;

    [ObservableProperty] private bool _canCheckForUpdates = false;

    [ObservableProperty] private Uri? _gameBananaLink;

    private CharacterGridItemModel[] _lastCharacters = Array.Empty<CharacterGridItemModel>();

    private bool isNavigating = true;

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
        DockPanelVM.FilterElementSelected += FilterElementSelected;
        DockPanelVM.Initialize();
        StartGameIcon = _gameService.GameIcon;
        ShortGameName = "Start " + _gameService.GameShortName;
        GameBananaLink = _gameService.GameBananaUrl;

        CanCheckForUpdates = _modUpdateAvailableChecker.IsReady;
        _modUpdateAvailableChecker.OnUpdateCheckerEvent += (_, _) =>
        {
            App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                CanCheckForUpdates = _modUpdateAvailableChecker.IsReady;
            });
        };
    }

    private void FilterElementSelected(object? sender, FilterElementSelectedArgs e)
    {
        if (e.InternalElementNames.Length == 0)
        {
            _filters.Remove(FilterType.Element);
            ResetContent();
            return;
        }

        _filters[FilterType.Element] = new GridFilter(character =>
            e.InternalElementNames.Contains(character.Character.Element.InternalName));
        ResetContent();
    }

    private readonly CharacterGridItemModel _noCharacterFound =
        new(new Character("None", "No Characters Found..."));

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

        var suitableItems = _gameService.QueryCharacters(text, minScore: 100).OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(x => new CharacterGridItemModel(x.Key))
            .ToList();


        if (!suitableItems.Any())
        {
            SuggestionsBox.Add(_noCharacterFound);
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
        if (character == _noCharacterFound || character is null)
            return false;


        _navigationService.SetListDataItemForNextConnectedAnimation(character);
        _navigationService.NavigateTo(typeof(CharacterDetailsViewModel).FullName!, character);
        return true;
    }

    private void ResetContent()
    {
        if (isNavigating) return;

        var filteredCharacters = FilterCharacters(_backendCharacters);
        var sortedCharacters = _sortingMethod.Sort(filteredCharacters).ToList();

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

    public async void OnNavigatedTo(object parameter)
    {
        await Task.Run(async () =>
        {
            var characters = _gameService.GetCharacters().ToList();
            var others =
                characters.FirstOrDefault(ch => ch.InternalNameEquals(_gameService.OtherCharacterInternalName));
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

            characters = new List<ICharacter>(_characters);

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

            var distinctReleaseDates = _backendCharacters
                .Where(ch => ch.Character.ReleaseDate != default)
                .DistinctBy(ch => ch.Character.ReleaseDate)
                .Count();

            if (distinctReleaseDates == 1)
                SortingMethods.Remove(SortingMethodType.ReleaseDate);


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


                if (modList.Mods.Count(modEntry => modEntry.IsEnabled) > 2)
                {
                    charactersWithMultipleActiveSkins.Add(modList.Character.InternalName);
                }
                else if (modList.Character is ICharacter character)
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

            var lastCharacters = new List<CharacterGridItemModel>
            {
                FindCharacterByInternalName(_gameService.GlidersCharacterInternalName)!,
                FindCharacterByInternalName(_gameService.WeaponsCharacterInternalName)!
            };

            _lastCharacters = lastCharacters.ToArray();

            _sortingMethod = new SortingMethod(SortingMethodType.Alphabetical,
                FindCharacterByInternalName(_gameService.OtherCharacterInternalName), _lastCharacters);
        });

        // ShowOnlyModsCharacters
        var settings =
            await _localSettingsService
                .ReadOrCreateSettingAsync<CharacterOverviewSettings>(CharacterOverviewSettings.Key);
        if (settings.ShowOnlyCharactersWithMods)
        {
            ShowOnlyCharactersWithMods = true;
            _filters[FilterType.HasMods] = new GridFilter(characterGridItem =>
                _skinManagerService.GetCharacterModList(characterGridItem.Character).Mods.Any());
        }

        SortByDescending = settings.SortByDescending;

        _sortingMethod = new SortingMethod(settings.SortingMethod,
            FindCharacterByInternalName(_gameService.OtherCharacterInternalName), _lastCharacters, SortByDescending);
        SelectedSortingMethod = settings.SortingMethod;

        isNavigating = false;
        ResetContent();
    }

    private async Task RefreshNotificationsAsync()
    {
        foreach (var character in _characters)
        {
            var characterGridItemModel = FindCharacterByInternalName(character.InternalName);
            if (characterGridItemModel is null) continue;
            var notifications =
                await _modNotificationManager.GetNotificationsForInternalNameAsync(character.InternalName);

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

    private async Task<CharacterOverviewSettings> ReadCharacterSettings()
    {
        return await _localSettingsService.ReadSettingAsync<CharacterOverviewSettings>(CharacterOverviewSettings.Key) ??
               new CharacterOverviewSettings();
    }

    private async Task SaveCharacterSettings(CharacterOverviewSettings settings)
    {
        await _localSettingsService.SaveSettingAsync(CharacterOverviewSettings.Key, settings);
    }


    [RelayCommand]
    private async Task Start3DmigotoAsync()
    {
        _logger.Debug("Starting 3Dmigoto");
        ThreeDMigtoProcessManager.CheckStatus();

        if (ThreeDMigtoProcessManager.ProcessStatus == ProcessStatus.NotInitialized)
        {
            var processPath = await ThreeDMigtoProcessManager.PickProcessPathAsync(App.MainWindow);
            if (processPath is null) return;
            await ThreeDMigtoProcessManager.SetPath(Path.GetFileName(processPath), processPath);
        }

        if (ThreeDMigtoProcessManager.ProcessStatus == ProcessStatus.NotRunning)
        {
            ThreeDMigtoProcessManager.StartProcess();
            if (ThreeDMigtoProcessManager.ErrorMessage is not null)
                NotificationManager.ShowNotification($"Failed to start {ThreeDMigtoProcessManager.ProcessName}",
                    ThreeDMigtoProcessManager.ErrorMessage,
                    TimeSpan.FromSeconds(5));
        }
    }

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

    [RelayCommand]
    private async Task StartGenshinAsync()
    {
        _logger.Debug("Starting Genshin Impact");
        GenshinProcessManager.CheckStatus();
        if (GenshinProcessManager.ProcessStatus == ProcessStatus.NotInitialized)
        {
            var processPath = await GenshinProcessManager.PickProcessPathAsync(App.MainWindow);
            if (processPath is null) return;
            await GenshinProcessManager.SetPath(Path.GetFileName(processPath), processPath);
        }

        if (GenshinProcessManager.ProcessStatus == ProcessStatus.NotRunning)
        {
            GenshinProcessManager.StartProcess();
            if (GenshinProcessManager.ErrorMessage is not null)
                NotificationManager.ShowNotification($"Failed to start {GenshinProcessManager.ProcessName}",
                    GenshinProcessManager.ErrorMessage,
                    TimeSpan.FromSeconds(5));
        }
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

        var errored = false;
        try
        {
            IsAddingMod = true;
            var extractResults = await _modDragAndDropService.AddStorageItemFoldersAsync(modList, storageItems);

            foreach (var extractResult in extractResults)
            {
                var notfiy = new ModNotification()
                {
                    CharacterInternalName = modList.Character.InternalName,
                    AttentionType = AttentionType.Added,
                    ModFolderName = new DirectoryInfo(extractResult.ExtractedFolderPath).Name,
                    Message = "Mod added from character overview"
                };
                await _modNotificationManager.AddModNotification(notfiy);

                characterGridItemModel.Notification = true;
                characterGridItemModel.NotificationType = notfiy.AttentionType;
            }
        }
        catch (Exception e)
        {
            errored = true;
            _logger.Error(e, "Error adding mod");
            NotificationManager.ShowNotification("Error adding mod", e.Message, TimeSpan.FromSeconds(10));
        }
        finally
        {
            IsAddingMod = false;
        }

        if (!errored)
            NotificationManager.ShowNotification("Mod added",
                $"Added {storageItems.Count} mod to {characterGridItemModel.Character.InternalName}",
                TimeSpan.FromSeconds(2));
    }


    public Task ModDroppedOnAutoDetect(IReadOnlyList<IStorageItem> storageItems)
    {
        var modNameToCharacter = new Dictionary<IStorageItem, ICharacter>();
        var othersCharacter = _gameService.GetCharacters()
            .First(x => x.InternalName == _gameService.OtherCharacterInternalName);

        foreach (var storageItem in storageItems)
        {
            var modName = Path.GetFileNameWithoutExtension(storageItem.Name);
            var result = _gameService.QueryCharacters(modName, minScore: 100);

            var character = result.FirstOrDefault().Key;
            if (character is not null)
            {
                _logger.Debug("Mod {ModName} was detected as {Character}", modName,
                    character.InternalName);
                modNameToCharacter.Add(storageItem, character);
            }
            else
            {
                _logger.Debug("Mod {ModName} was not detected as any character", modName);
                modNameToCharacter.Add(storageItem, othersCharacter);
            }
        }

        return Task.CompletedTask;
    }

    private CharacterGridItemModel? FindCharacterByInternalName(string internalName)
    {
        return _backendCharacters.FirstOrDefault(x =>
            x.Character.InternalNameEquals(internalName));
    }

    private CharacterGridItemModel? FindCharacter(ICharacter character)
    {
        return FindCharacterByInternalName(character.InternalName);
    }


    [RelayCommand]
    private async Task SortBy(IEnumerable<SortingMethodType> methodTypes)
    {
        if (isNavigating) return;
        var sortingMethodType = methodTypes.First();

        _sortingMethod = new SortingMethod(sortingMethodType,
            FindCharacterByInternalName(_gameService.OtherCharacterInternalName),
            _lastCharacters, isDescending: SortByDescending);
        ResetContent();

        var settings = await ReadCharacterSettings();
        settings.SortingMethod = sortingMethodType;
        await SaveCharacterSettings(settings).ConfigureAwait(false);
    }


    [RelayCommand]
    private async Task InvertSorting()
    {
        _sortingMethod.IsDescending = SortByDescending;
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
    NotSet,
    Element,
    WeaponType,
    Search,
    HasMods
}

public sealed class SortingMethod
{
    public SortingMethodType SortingMethodType { get; }
    public bool IsDescending { get; set; }

    private readonly CharacterGridItemModel[] _lastCharacters;
    private readonly CharacterGridItemModel? _firstCharacter;

    public SortingMethod(SortingMethodType sortingMethodType, CharacterGridItemModel? firstCharacter = null,
        ICollection<CharacterGridItemModel>? lastCharacters = null, bool isDescending = false)
    {
        SortingMethodType = sortingMethodType;
        IsDescending = isDescending;
        _lastCharacters = lastCharacters?.ToArray() ?? Array.Empty<CharacterGridItemModel>();
        _firstCharacter = firstCharacter;
    }

    private static IEnumerable<CharacterGridItemModel> Sort<T, T1, T2>(
        IEnumerable<CharacterGridItemModel> characters,
        Func<CharacterGridItemModel, T> sortFirstBy, bool firstByDescending = false,
        Func<CharacterGridItemModel, T1>? sortSecondBy = null, bool secondByDescending = false,
        Func<CharacterGridItemModel, T2>? sortThirdBy = null, bool thirdByDescending = false
    )
    {
        var charactersList = firstByDescending
            ? characters.OrderByDescending(sortFirstBy)
            : characters.OrderBy(sortFirstBy);

        if (sortSecondBy is not null)
        {
            charactersList = secondByDescending
                ? charactersList.ThenByDescending(sortSecondBy)
                : charactersList.ThenBy(sortSecondBy);
        }

        if (sortThirdBy is not null)
        {
            charactersList = thirdByDescending
                ? charactersList.ThenByDescending(sortThirdBy)
                : charactersList.ThenBy(sortThirdBy);
        }

        return charactersList;
    }

    public IEnumerable<CharacterGridItemModel> Sort(IEnumerable<CharacterGridItemModel> characters)
    {
        IEnumerable<CharacterGridItemModel> sortedCharacters = null!;
        switch (SortingMethodType)
        {
            case SortingMethodType.Alphabetical:
                sortedCharacters = Sort<string, string, string>(characters, x => x.Character.DisplayName, IsDescending);
                break;
            case SortingMethodType.ReleaseDate:
                sortedCharacters = Sort<DateTime, string, string>(characters, x => x.Character.ReleaseDate,
                    !IsDescending,
                    sortSecondBy: x => x.Character.DisplayName);
                break;
            case SortingMethodType.Rarity:
                sortedCharacters = Sort(characters, x => x.Character.Rarity, !IsDescending,
                    sortSecondBy: x => x.Character.ReleaseDate, !IsDescending,
                    sortThirdBy: x => x.Character.DisplayName);

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(SortingMethodType));
        }

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
}

public enum SortingMethodType
{
    Alphabetical,
    ReleaseDate,
    Rarity
}