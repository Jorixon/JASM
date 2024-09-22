using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services.ModPresetService;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;

public partial class ModGridVM(
    ISkinManagerService skinManagerService,
    CharacterSkinService characterSkinService,
    NotificationManager notificationService,
    ModPresetService presetService)
    : ObservableRecipient, IRecipient<ModChangedMessage>
{
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly CharacterSkinService _characterSkinService = characterSkinService;
    private readonly NotificationManager _notificationService = notificationService;
    private readonly ModPresetService _presetService = presetService;

    private CancellationToken _navigationCt = default;
    private ICharacterModList _characterModList = null!;
    private ModDetailsPageContext _context = null!;

    private ModGridSortingMethod _currentSortingMethod = new(ModRowSorters.IsEnabledSorters);


    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy = true;

    public bool IsNotBusy => !IsBusy;

    private List<CharacterSkinEntry> _modsBackend = new();
    public IReadOnlyList<CharacterSkinEntry> ModsBackend => _modsBackend.AsReadOnly();
    private readonly List<ModRowVM> _gridModsBackend = new();
    public ObservableCollection<ModRowVM> GridMods { get; } = new();
    public ObservableCollection<ModRowVM> SelectedMods { get; } = new();

    public bool IsSingleModSelected => SelectedMods.Count == 1;

    public bool ModdableObjectHasAnyMods => _modsBackend.Count != 0;

    public async Task InitializeAsync(ModDetailsPageContext context, CancellationToken navigationCt = default)
    {
        _navigationCt = navigationCt;
        _context = context;
        _characterModList = _skinManagerService.GetCharacterModList(_context.ShownModObject);
        await ReloadAllModsAsync();
        IsBusy = false;
    }


    public async Task ReloadAllModsAsync()
    {
        GridMods.Clear();
        _gridModsBackend.Clear();
        RefreshResult refreshResult = new RefreshResult();

        await Task.Run(async () =>
        {
            refreshResult = await _skinManagerService
                .RefreshModsAsync(_context.ShownModObject.InternalName)
                .ConfigureAwait(false);

            if (_context.IsCharacter && _context.Skins.Count > 1)
            {
                _modsBackend = await _characterSkinService
                    .GetCharacterSkinEntriesForSkinAsync(_context.SelectedSkin, useSettingsCache: true,
                        cancellationToken: _navigationCt)
                    .ToListAsync().ConfigureAwait(false);
            }
            else
            {
                _modsBackend = _characterModList.Mods.ToList();
            }

            var modToPresetMapping = await _presetService
                .FindPresetsForModsAsync(_modsBackend.Select(m => m.Id))
                .ConfigureAwait(false);

            foreach (var x in _modsBackend)
            {
                _navigationCt.ThrowIfCancellationRequested();

                IEnumerable<string> presetNames = [];

                if (modToPresetMapping.TryGetValue(x.Id, out var modPresets))
                    presetNames = modPresets.Select(m => m.Name);

                var modVm = await CreateModRowVM(x, presetNames, _navigationCt).ConfigureAwait(false);
                _gridModsBackend.Add(modVm);
            }

            if (refreshResult.ModsDuplicate.Any())
            {
                var message = $"Duplicate mods were detected in {_context.ModObjectDisplayName}'s mod folder.\n";

                message = refreshResult.ModsDuplicate.Aggregate(message,
                    (current, duplicateMod) =>
                        current +
                        $"Mod: '{duplicateMod.ExistingFolderName}' was renamed to '{duplicateMod.RenamedFolderName}' to avoid conflicts.\n");

                _notificationService.ShowNotification("Duplicate Mods Detected",
                    message,
                    TimeSpan.FromSeconds(10));
            }

            // TODO: TESTING
            var orderedMods = _currentSortingMethod.Sort(_gridModsBackend, true).ToList();
            _gridModsBackend.Clear();
            _gridModsBackend.AddRange(orderedMods);
        }, _navigationCt);


        GridMods.AddRange(_gridModsBackend);
    }

    private async Task<ModRowVM> CreateModRowVM(CharacterSkinEntry characterSkinEntry, IEnumerable<string> presetNames,
        CancellationToken cancellationToken = default)
    {
        var skinModSettings = await characterSkinEntry.Mod.Settings.TryReadSettingsAsync(true, cancellationToken)
            .ConfigureAwait(false);

        return new ModRowVM(characterSkinEntry, skinModSettings, presetNames);
    }

    public ModRowVM[] SearchFilterMods(string searchText)
    {
        var modsToShow = _gridModsBackend
            .Where(x => x.SearchableText.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
            .ToArray();

        var modsToHide = _gridModsBackend.Except(modsToShow).ToArray();

        foreach (var mod in modsToShow)
        {
            if (GridMods.Contains(mod))
                continue;

            GridMods.Add(mod);
        }

        foreach (var modToHide in modsToHide)
            GridMods.Remove(modToHide);

        return GridMods.ToArray();
    }

    public void ResetModView()
    {
        var selectedMod = SelectedMods.FirstOrDefault();
        GridMods.Clear();
        GridMods.AddRange(_gridModsBackend);

        if (selectedMod is not null)
            SetSelectedMod(selectedMod.Id);
    }

    public void Receive(ModChangedMessage message)
    {
        // TODO: IMplement
    }

    public void SetSelectedMod(Guid modId, bool ignoreFilters = false)
    {
        if (ignoreFilters)
            throw new NotImplementedException();

        var mod = GridMods.FirstOrDefault(x => x.Id == modId);
        if (mod is null)
            return;

        var index = GridMods.IndexOf(mod);
        SelectModEvent?.Invoke(this, new SelectModRowEventArgs(index));
    }

    public void ClearSelection() => SelectModEvent?.Invoke(this, new SelectModRowEventArgs(-1));


    // Only to be used by code behind
    // Contrary to my attempt at MVVM, this is the only way to get the selected mods from the view
    // DataGrid is quite limited in this regard, so it is in charge of the selection and the view model just listens
    public void SelectionChanged_EventHandler(ICollection<ModRowVM> selectedMods, ICollection<ModRowVM> removedMods)
    {
        var anyChanges = false;
        if (selectedMods.Any())
        {
            foreach (var mod in selectedMods)
            {
                if (!SelectedMods.Contains(mod))
                {
                    SelectedMods.Add(mod);
                    anyChanges = true;
                }
            }
        }

        if (removedMods.Any())
        {
            foreach (var mod in removedMods)
            {
                if (SelectedMods.Contains(mod))
                {
                    SelectedMods.Remove(mod);
                    anyChanges = true;
                }
            }
        }

        if (anyChanges)
        {
            OnPropertyChanged(nameof(IsSingleModSelected));
            OnModsSelected?.Invoke(this, new ModRowSelectedEventArgs(SelectedMods));
        }
    }

    // Used by parent view model
    public event EventHandler<ModRowSelectedEventArgs>? OnModsSelected;

    // Set single selected from code
    public event EventHandler<SelectModRowEventArgs>? SelectModEvent;

    public class ModRowSelectedEventArgs(IEnumerable<ModRowVM> mods) : EventArgs
    {
        public ICollection<ModRowVM> Mods { get; } = mods.ToArray();
    }

    public class SelectModRowEventArgs(int index) : EventArgs
    {
        public int Index { get; } = index;
    }
}

public sealed class ModGridSortingMethod : SortingMethod<ModRowVM>
{
    public ModGridSortingMethod(Sorter<ModRowVM> sortingMethodType, ModRowVM? firstItem = null,
        ICollection<ModRowVM>? lastItems = null) : base(sortingMethodType, firstItem, lastItems)
    {
    }
}

public sealed class ModRowSorters : Sorter<ModRowVM>
{
    private ModRowSorters(string sortingMethodType, SortFunc firstSortFunc, AdditionalSortFunc? secondSortFunc = null,
        AdditionalSortFunc? thirdSortFunc = null) : base(sortingMethodType, firstSortFunc, secondSortFunc,
        thirdSortFunc)
    {
    }

    public static readonly string NoneName = "None";

    public static ModRowSorters NoneSorter { get; } = new(NoneName,
        (mod, isDescending) => mod.OrderBy(x => x.Id)
    );


    public static readonly string IsEnabledName = nameof(ModRowVM.IsEnabled);

    public static ModRowSorters IsEnabledSorters { get; } = new(IsEnabledName,
        (mod, isDescending) =>
            isDescending
                ? mod.OrderByDescending(x => x.IsEnabled).ThenByDescending(x => x.DateAdded)
                : mod.OrderBy(x => x.IsEnabled).ThenByDescending(x => x.DateAdded)
    );


    public static readonly string DateAddedName = nameof(ModRowVM.DateAdded);

    public static ModRowSorters DatedAddedSorters { get; } = new(DateAddedName,
        (mod, isDescending) =>
            isDescending
                ? mod.OrderByDescending(x => (x.DateAdded))
                : mod.OrderBy(x => (x.DateAdded)
                ));
}
