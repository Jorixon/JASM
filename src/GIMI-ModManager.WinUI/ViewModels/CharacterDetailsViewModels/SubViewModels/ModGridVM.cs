using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;

public partial class ModGridVM(ISkinManagerService skinManagerService, CharacterSkinService characterSkinService, NotificationManager notificationService)
    : ObservableRecipient, IRecipient<ModChangedMessage>
{
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly CharacterSkinService _characterSkinService = characterSkinService;
    private readonly NotificationManager _notificationService = notificationService;

    private CancellationToken _navigationCt = default;
    private ICharacterModList _characterModList = null!;
    private ModDetailsPageContext _context = null!;


    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy = true;

    public bool IsNotBusy => !IsBusy;

    private List<CharacterSkinEntry> _modBackend = new();
    public ObservableCollection<ModRowVM> GridMods = new();
    public ObservableCollection<ModRowVM> SelectedMods = new();

    public bool ModdableObjectHasAnyMods => _modBackend.Count != 0;

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
        var modRows = new List<ModRowVM>();
        RefreshResult refreshResult = new RefreshResult();

        await Task.Run(async () =>
        {
            refreshResult = await _skinManagerService
                .RefreshModsAsync(_context.ShownModObject.InternalName)
                .ConfigureAwait(false);

            if (_context.IsCharacter && _context.Skins.Count > 1)
            {
                _modBackend = await _characterSkinService
                    .GetCharacterSkinEntriesForSkinAsync(_context.SelectedSkin, useSettingsCache: true,
                        cancellationToken: _navigationCt)
                    .ToListAsync().ConfigureAwait(false);
            }
            else
            {
                _modBackend = _characterModList.Mods.ToList();
            }


            foreach (var x in _modBackend)
            {
                _navigationCt.ThrowIfCancellationRequested();
                var modVm = await CreateModRowVM(x, _navigationCt).ConfigureAwait(false);
                modRows.Add(modVm);
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
        }, _navigationCt);


        GridMods.AddRange(modRows);
    }

    private async Task<ModRowVM> CreateModRowVM(CharacterSkinEntry characterSkinEntry,
        CancellationToken cancellationToken = default)
    {
        var skinModSettings = await characterSkinEntry.Mod.Settings.TryReadSettingsAsync(true, cancellationToken)
            .ConfigureAwait(false);
        return new ModRowVM(characterSkinEntry, skinModSettings);
    }

    public void Receive(ModChangedMessage message)
    {
        // TODO: IMplement
    }

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
                }
            }
        }

        if (removedMods.Any())
        {
            foreach (var mod in removedMods)
            {
                if (SelectedMods.Contains(mod))
                    SelectedMods.Remove(mod);
            }
        }

        //SelectedModsCount = SelectedMods.Count;
        //OnModsSelected?.Invoke(this, new ModSelectedEventArgs(SelectedMods));
    }
}

public partial class ModRowVM : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public bool IsEnabled { get; init; }
    public string DisplayName { get; init; }

    public string FolderName { get; init; }

    public ModRowVM(CharacterSkinEntry characterSkinEntry, ModSettings? modSettings)
    {
        IsEnabled = characterSkinEntry.IsEnabled;
        DisplayName = modSettings?.CustomName ?? characterSkinEntry.Mod.GetDisplayName();
        FolderName = characterSkinEntry.Mod.Name;
    }
}