using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities.Genshin;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Models.Options;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class CharactersViewModel : ObservableRecipient, INavigationAware
{
    private readonly IGenshinService _genshinService;
    private readonly ILogger _logger;
    private readonly INavigationService _navigationService;
    private readonly ISkinManagerService _skinManagerService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ModDragAndDropService _modDragAndDropService;
    private readonly ModNotificationManager _modNotificationManager;
    private readonly ModCrawlerService _modCrawlerService;

    public readonly GenshinProcessManager GenshinProcessManager;

    public readonly ThreeDMigtoProcessManager ThreeDMigtoProcessManager;
    public NotificationManager NotificationManager { get; }
    public ElevatorService ElevatorService { get; }

    public OverviewDockPanelVM DockPanelVM { get; }


    private GenshinCharacter[] _characters = Array.Empty<GenshinCharacter>();

    private CharacterGridItemModel[] _backendCharacters = Array.Empty<CharacterGridItemModel>();
    public ObservableCollection<CharacterGridItemModel> Characters { get; } = new();

    public ObservableCollection<CharacterGridItemModel> SuggestionsBox { get; } = new();

    public ObservableCollection<CharacterGridItemModel> PinnedCharacters { get; } = new();

    public ObservableCollection<CharacterGridItemModel> HiddenCharacters { get; } = new();


    private string _searchText = string.Empty;

    public CharactersViewModel(IGenshinService genshinService, ILogger logger, INavigationService navigationService,
        ISkinManagerService skinManagerService, ILocalSettingsService localSettingsService,
        NotificationManager notificationManager, ElevatorService elevatorService,
        GenshinProcessManager genshinProcessManager, ThreeDMigtoProcessManager threeDMigtoProcessManager,
        ModDragAndDropService modDragAndDropService, ModNotificationManager modNotificationManager,
        ModCrawlerService modCrawlerService)
    {
        _genshinService = genshinService;
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

        ElevatorService.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ElevatorService.ElevatorStatus))
                RefreshModsInGameCommand.NotifyCanExecuteChanged();
        };
        DockPanelVM = new OverviewDockPanelVM();
        DockPanelVM.FilterElementSelected += FilterElementSelected;
    }

    private void FilterElementSelected(object? sender, FilterElementSelectedArgs e)
    {
        _logger.Debug("Filtering characters by element {Element}", e.Element);
    }

    private readonly CharacterGridItemModel _noCharacterFound =
        new(new GenshinCharacter { Id = -999999, DisplayName = "No Characters Found..." });

    public void AutoSuggestBox_TextChanged(string text)
    {
        _searchText = text;
        SuggestionsBox.Clear();

        if (string.IsNullOrWhiteSpace(_searchText))
        {
            SuggestionsBox.Clear();
            ResetContent();
            return;
        }

        var suitableItems = _genshinService.GetCharacters(text, minScore: 100).OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(x => new CharacterGridItemModel(x.Key))
            .ToList();


        if (!suitableItems.Any())
        {
            SuggestionsBox.Add(_noCharacterFound);
            ResetContent();
            return;
        }

        suitableItems.ForEach(suggestion => SuggestionsBox.Add(suggestion));

        ShowOnlyCharacters(suitableItems);
    }


    public bool SuggestionBox_Chosen(CharacterGridItemModel? character)
    {
        if (character == _noCharacterFound || character is null)
            return false;


        _navigationService.SetListDataItemForNextConnectedAnimation(character);
        _navigationService.NavigateTo(typeof(CharacterDetailsViewModel).FullName!, character);
        return true;
    }

    //private void ResetContent()
    //{
    //    var neitherPinnedNorHiddenCharacters = _characters.Where(x =>
    //        !PinnedCharacters.Contains(x) && !HiddenCharacters.Contains(x)).ToArray();

    //    var listLocationIndex = 0;
    //    for (var i = 0; i < PinnedCharacters.Count; i++)
    //    {
    //        var character = PinnedCharacters.ElementAtOrDefault(i);

    //        if (character is null)
    //        {
    //            Characters.Add(_characters[i]);
    //            listLocationIndex = i + 1;
    //            continue;
    //        }

    //        if (character.Id != _characters[i].Id)
    //        {
    //            Characters.Insert(i, _characters[i]);
    //        }

    //        listLocationIndex = i + 1;
    //    }

    //    var nextListLocationIndex = listLocationIndex;

    //    for (var i = listLocationIndex; i < neitherPinnedNorHiddenCharacters.Length + listLocationIndex; i++)
    //    {
    //        var index = i - listLocationIndex;
    //        var character = Characters.ElementAtOrDefault(i);

    //        if (character is null)
    //        {
    //            Characters.Add(_characters[index]);
    //            nextListLocationIndex = i + 1;
    //            continue;
    //        }

    //        if (character.Id != _characters[index].Id)
    //        {
    //            Characters.Insert(i, _characters[index]);
    //        }

    //        nextListLocationIndex = i + 1;
    //    }

    //    for (var i = nextListLocationIndex; i < HiddenCharacters.Count + nextListLocationIndex; i++)
    //    {
    //        var index = i - nextListLocationIndex;
    //        var character = HiddenCharacters.ElementAtOrDefault(i);

    //        if (character is null)
    //        {
    //            if (HiddenCharacters.Contains(_characters[index]))
    //            {
    //                continue;
    //            }

    //            Characters.Add(_characters[index]);
    //            continue;
    //        }

    //        if (character.Id != _characters[index].Id)
    //        {
    //            if (HiddenCharacters.Contains(_characters[index]))
    //            {
    //                continue;
    //            }

    //            Characters.Insert(i, _characters[index]);
    //        }
    //    }
    //}

    private void ResetContent()
    {
        var neitherPinnedNorHiddenCharacters = _characters.Where(x =>
            !PinnedCharacters.Select(pch => pch.Character).Contains(x) &&
            !HiddenCharacters.Select(pch => pch.Character).Contains(x));


        var gridIndex = 0;
        foreach (var genshinCharacter in PinnedCharacters)
        {
            InsertCharacterIntoView(genshinCharacter, gridIndex);
            gridIndex++;
        }

        foreach (var genshinCharacter in neitherPinnedNorHiddenCharacters.Select(ch => new CharacterGridItemModel(ch)))
        {
            InsertCharacterIntoView(genshinCharacter, gridIndex);
            gridIndex++;
        }

        foreach (var genshinCharacter in HiddenCharacters)
        {
            InsertCharacterIntoView(genshinCharacter, gridIndex);
            gridIndex++;
        }

        for (int i = Characters.Count; i > gridIndex; i--) Characters.RemoveAt(i - 1);


        Debug.Assert(Characters.Distinct().Count() == Characters.Count,
            $"Characters.Distinct().Count(): {Characters.Distinct().Count()} != Characters.Count: {Characters.Count}\n\t" +
            $"Duplicate characters found in character overview");
    }


    private void InsertCharacterIntoView(CharacterGridItemModel character, int gridIndex)
    {
        var characterAtGridIndex = Characters.ElementAtOrDefault(gridIndex);

        if (characterAtGridIndex?.Character.Id == character.Character.Id) return;

        if (characterAtGridIndex is null)
        {
            Characters.Add(character);
            return;
        }

        if (character.Character.Id != characterAtGridIndex.Character.Id) Characters.Insert(gridIndex, character);
    }

    private void ShowOnlyCharacters(IEnumerable<CharacterGridItemModel> charactersToShow, bool hardClear = false)
    {
        var tmpList = new List<CharacterGridItemModel>(_backendCharacters);


        var characters = tmpList.Where(charactersToShow.Contains).ToArray();

        var pinnedCharacters = characters.Intersect(PinnedCharacters).ToArray();
        characters = characters.Except(pinnedCharacters).ToArray();
        Characters.Clear();

        foreach (var genshinCharacter in pinnedCharacters) Characters.Add(genshinCharacter);

        foreach (var genshinCharacter in characters) Characters.Add(genshinCharacter);

        Debug.Assert(Characters.Distinct().Count() == Characters.Count,
            $"Characters.Distinct().Count(): {Characters.Distinct().Count()} != Characters.Count: {Characters.Count}\n\t" +
            $"Duplicate characters found in character overview");
    }

    public async void OnNavigatedTo(object parameter)
    {
        var characters = _genshinService.GetCharacters().OrderBy(g => g.DisplayName).ToList();
        var others = characters.FirstOrDefault(ch => ch.Id == _genshinService.OtherCharacterId);
        if (others is not null) // Add to front
        {
            characters.Remove(others);
            characters.Insert(0, others);
        }

        var gliders = characters.FirstOrDefault(ch => ch.Id == _genshinService.GlidersCharacterId);
        if (gliders is not null) // Add to end
        {
            characters.Remove(gliders);
            characters.Add(gliders);
        }

        var weapons = characters.FirstOrDefault(ch => ch.Id == _genshinService.WeaponsCharacterId);
        if (weapons is not null) // Add to end
        {
            characters.Remove(weapons);
            characters.Add(weapons);
        }


        DockPanelVM.Initialize();

        _characters = characters.ToArray();

        var pinnedCharactersOptions = await ReadCharacterSettings();

        foreach (var pinedCharacterId in pinnedCharactersOptions.PinedCharacters)
        {
            var character = _characters.FirstOrDefault(x => x.Id == pinedCharacterId);
            if (character is not null) PinnedCharacters.Add(new CharacterGridItemModel(character) { IsPinned = true });
        }

        foreach (var hiddenCharacterId in pinnedCharactersOptions.HiddenCharacters)
        {
            var character = _characters.FirstOrDefault(x => x.Id == hiddenCharacterId);
            if (character is not null) HiddenCharacters.Add(new CharacterGridItemModel(character) { IsHidden = true });
        }

        ResetContent();

        var allCharacters = new List<CharacterGridItemModel>();
        foreach (var genshinCharacter in _characters)
        {
            var characterGridItemModel = FindCharacterById(genshinCharacter.Id);
            if (characterGridItemModel is null) continue;
            allCharacters.Add(characterGridItemModel);
        }

        _backendCharacters = allCharacters.ToArray();

        foreach (var genshinCharacter in _characters)
        {
            var characterGridItemModel = FindCharacterById(genshinCharacter.Id);
            if (characterGridItemModel is null) continue;
            var notifications = _modNotificationManager.GetInMemoryModNotifications(characterGridItemModel.Character);
            foreach (var modNotification in notifications)
            {
                if (modNotification.AttentionType != AttentionType.Added) continue;

                characterGridItemModel.Notification = true;
                characterGridItemModel.NotificationType = modNotification.AttentionType;
            }
        }

        // Character Ids where more than 1 skin is enabled
        var charactersWithMultipleMods = _skinManagerService.CharacterModLists
            .Where(x => x.Mods.Count(mod => mod.IsEnabled) > 1);

        var charactersWithMultipleActiveSkins = new List<int>();
        foreach (var modList in charactersWithMultipleMods)
        {
            if (_genshinService.IsMultiModCharacter(modList.Character))
                continue;

            var addWarning = false;
            var subSkinsFound = new List<ISubSkin>();
            foreach (var characterSkinEntry in modList.Mods)
            {
                if (!characterSkinEntry.IsEnabled) continue;

                var subSkin = _modCrawlerService.GetFirstSubSkinRecursive(characterSkinEntry.Mod.FullPath,
                    modList.Character);
                if (subSkin is null) continue;

                if (!subSkinsFound.Contains(subSkin))
                {
                    subSkinsFound.Add(subSkin);
                    continue;
                }


                addWarning = true;
                break;
            }

            if (addWarning)
                charactersWithMultipleActiveSkins.Add(modList.Character.Id);
        }


        foreach (var characterGridItemModel in Characters.Where(x =>
                     charactersWithMultipleActiveSkins.Contains(x.Character.Id)))
        {
            if (_genshinService.IsMultiModCharacter(characterGridItemModel.Character))
                continue;

            characterGridItemModel.Warning = true;
        }


        if (!pinnedCharactersOptions.ShowOnlyCharactersWithMods) return;

        ShowOnlyCharactersWithMods = true;
        var characterIdsWithMods =
            _skinManagerService.CharacterModLists.Where(x => x.Mods.Any()).Select(x => x.Character.Id);

        var charactersWithMods = Characters.Where(x => characterIdsWithMods.Contains(x.Character.Id));

        ShowOnlyCharacters(charactersWithMods);
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
            ResetContent();
            var settingss = await ReadCharacterSettings();


            settingss.ShowOnlyCharactersWithMods = ShowOnlyCharactersWithMods;

            await SaveCharacterSettings(settingss);

            return;
        }

        var charactersWithMods =
            _skinManagerService.CharacterModLists.Where(x => x.Mods.Any())
                .Select(x => new CharacterGridItemModel(x.Character));

        ShowOnlyCharacters(charactersWithMods);

        ShowOnlyCharactersWithMods = true;

        var settings = await ReadCharacterSettings();

        settings.ShowOnlyCharactersWithMods = ShowOnlyCharactersWithMods;

        await SaveCharacterSettings(settings);
    }


    [ObservableProperty] private string _pinText = DefaultPinText;

    [ObservableProperty] private string _pinGlyph = DefaultPinGlyph;

    const string DefaultPinGlyph = "\uE718";
    const string DefaultPinText = "Pin To Top";
    const string DefaultUnpinGlyph = "\uE77A";
    const string DefaultUnpinText = "Unpin Character";

    public void OnRightClickContext(CharacterGridItemModel clickedCharacter)
    {
        if (PinnedCharacters.Contains(clickedCharacter))
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
        if (PinnedCharacters.Contains(character))
        {
            character.IsPinned = false;
            PinnedCharacters.Remove(character);

            if (!ShowOnlyCharactersWithMods)
            {
                ResetContent();
            }
            else
            {
                var charactersWithModss =
                    _skinManagerService.CharacterModLists.Where(x => x.Mods.Any())
                        .Select(x => new CharacterGridItemModel(x.Character));
                ShowOnlyCharacters(charactersWithModss, true);
            }


            var settingss = await ReadCharacterSettings();

            character.IsPinned = false;
            var pinedCharacterss = PinnedCharacters.Select(ch => ch.Character.Id).ToArray();
            settingss.PinedCharacters = pinedCharacterss;

            await SaveCharacterSettings(settingss);
            return;
        }

        character.IsPinned = true;
        PinnedCharacters.Add(character);

        if (!ShowOnlyCharactersWithMods)
        {
            ResetContent();
        }

        else
        {
            var charactersWithMods =
                _skinManagerService.CharacterModLists.Where(x => x.Mods.Any())
                    .Select(x => new CharacterGridItemModel(x.Character));
            ShowOnlyCharacters(charactersWithMods);
        }


        var settings = await ReadCharacterSettings();

        var pinedCharacters = PinnedCharacters.Select(ch => ch.Character.Id)
            .Union(settings.PinedCharacters.ToList()).ToArray();
        settings.PinedCharacters = pinedCharacters;

        await SaveCharacterSettings(settings);
    }


    [RelayCommand]
    private void HideCharacter(GenshinCharacter character)
    {
        NotImplemented.Show("Hiding characters is not implemented yet");
    }

    private async Task<CharacterOverviewOptions> ReadCharacterSettings()
    {
        return await _localSettingsService.ReadSettingAsync<CharacterOverviewOptions>(CharacterOverviewOptions.Key) ??
               new CharacterOverviewOptions();
    }

    private async Task SaveCharacterSettings(CharacterOverviewOptions settings)
    {
        await _localSettingsService.SaveSettingAsync(CharacterOverviewOptions.Key, settings);
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
                x.Character.Id == characterGridItemModel.Character.Id);
        if (modList is null)
        {
            _logger.Warning("No mod list found for character {Character}",
                characterGridItemModel.Character.DisplayName);
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
                    CharacterId = modList.Character.Id,
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
                $"Added {storageItems.Count} mod to {characterGridItemModel.Character.DisplayName}",
                TimeSpan.FromSeconds(2));
    }


    public Task ModDroppedOnAutoDetect(IReadOnlyList<IStorageItem> storageItems)
    {
        var modNameToCharacter = new Dictionary<IStorageItem, GenshinCharacter>();
        var othersCharacter = _genshinService.GetCharacters().First(x => x.Id == _genshinService.OtherCharacterId);

        foreach (var storageItem in storageItems)
        {
            var modName = Path.GetFileNameWithoutExtension(storageItem.Name);
            var result = _genshinService.GetCharacters(modName, minScore: 100);

            var character = result.FirstOrDefault().Key;
            if (character is not null)
            {
                _logger.Debug("Mod {ModName} was detected as {Character}", modName,
                    character.DisplayName);
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

    private CharacterGridItemModel? FindCharacterById(int id)
    {
        if (PinnedCharacters.Any(x => x.Character.Id == id))
            return PinnedCharacters.First(x => x.Character.Id == id);

        if (HiddenCharacters.Any(x => x.Character.Id == id))
            return HiddenCharacters.First(x => x.Character.Id == id);

        if (Characters.Any(x => x.Character.Id == id))
            return Characters.First(x => x.Character.Id == id);

        return null;
    }
}