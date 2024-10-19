using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Dispatching;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;

public partial class ContextMenuVM(ISkinManagerService skinManagerService, IGameService gameService, NotificationManager notificationManager, ILogger logger)
    : ObservableRecipient
{
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly IGameService _gameService = gameService;
    private readonly NotificationManager _notificationManager = notificationManager;
    private readonly ILogger _logger = logger.ForContext<ContextMenuVM>();

    private DispatcherQueue _dispatcherQueue = null!;
    private CancellationToken _navigationCt = default;
    private ICharacterModList _modList = null!;
    private ModDetailsPageContext _context = null!;
    private BusySetter _busySetter = null!;

    private List<Guid> _selectedMods = [];

    [ObservableProperty] private int _selectedModsCount;
    [ObservableProperty] private bool _isCharacter;

    public ObservableCollection<SuggestedModObject> SuggestedModdableObjects { get; } = new();

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(MoveModsCommand))]
    private SuggestedModObject? _selectedSuggestedModObject;

    [ObservableProperty] private string _moveModsSearchText = string.Empty;


    public Task InitializeAsync(ModDetailsPageContext context, BusySetter busySetter, CancellationToken navigationCt)

    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _navigationCt = navigationCt;
        ChangeSkin(context);
        _busySetter = busySetter;

        _modList = _skinManagerService.GetCharacterModList(context.ShownModObject);

        return Task.CompletedTask;
    }

    public void ChangeSkin(ModDetailsPageContext context)
    {
        _context = context;
        IsCharacter = context.IsCharacter;
    }

    public bool CanOpenContextMenu => SelectedModsCount > 0 && _busySetter.IsNotHardBusy;

    public void SetSelectedMods(IEnumerable<Guid> selectedMods)
    {
        _selectedMods = selectedMods.ToList();
        SelectedModsCount = _selectedMods.Count;
        MoveModsCommand.NotifyCanExecuteChanged();
    }

    private List<CharacterSkinEntry> ResolveSelectedMods() => _modList.Mods.Where(m => _selectedMods.Contains(m.Id)).ToList();


    #region Commands

    private bool CanMoveModsCommandExecute() => SelectedModsCount > 0
                                                && !_busySetter.IsWorking
                                                && SelectedSuggestedModObject is not null;

    [RelayCommand(CanExecute = nameof(CanMoveModsCommandExecute))]
    private async Task MoveModsAsync()
    {
        using var busy = _busySetter.StartHardBusy();
        var destinationModList = _skinManagerService.GetCharacterModListOrDefault(SelectedSuggestedModObject?.InternalName ?? "");


        if (destinationModList is null)
        {
            _logger.Warning("Destination mod list not found");
            _notificationManager.ShowNotification("Destination Mod List Not Found",
                "Destination mod list not found", TimeSpan.FromSeconds(5));
            return;
        }

        var selectedMods = ResolveSelectedMods();

        try
        {
            await Task.Run(() => _skinManagerService.TransferMods(_modList, destinationModList,
                selectedMods.Select(modEntry => modEntry.Id)));
        }
        catch (InvalidOperationException e)
        {
            _logger.Error(e, "Error moving mods");
            _notificationManager
                .ShowNotification("Invalid Operation Exception",
                    $"Cannot move mods\n{e.Message}, see logs for details.", TimeSpan.FromSeconds(10));
            return;
        }

        _notificationManager.ShowNotification($"{SelectedModsCount} Mods Moved",
            $"Successfully moved {string.Join(",", selectedMods.Select(m => m.Mod.GetDisplayName()))} mods to {destinationModList.Character.DisplayName}",
            TimeSpan.FromSeconds(5));

        ModsMoved?.Invoke(this, EventArgs.Empty);
    }

    #endregion


    #region Events

    public event EventHandler? ModsMoved;

    #endregion


    #region EventHandlers

    public void OnFlyoutClosing()
    {
        SuggestedModdableObjects.Clear();
        SelectedSuggestedModObject = null;
        MoveModsSearchText = string.Empty;
    }

    public void SearchTextChanged(string senderText)
    {
        SuggestedModdableObjects.Clear();
        SelectedSuggestedModObject = null;
        if (string.IsNullOrWhiteSpace(senderText))
        {
            return;
        }

        var result = _gameService.QueryModdableObjects(senderText, minScore: 120)
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(x => new SuggestedModObject(x.Key));

        SuggestedModdableObjects.AddRange(result);
    }

    public void OnSuggestionChosen(SuggestedModObject? modObject)
    {
        if (modObject is null)
            return;

        SelectedSuggestedModObject = modObject;
        SuggestedModdableObjects.Clear();
    }

    #endregion
}

public sealed class SuggestedModObject(IModdableObject moddableObject)
{
    public InternalName InternalName { get; } = moddableObject.InternalName;
    public string DisplayName { get; } = moddableObject.DisplayName;

    public override string ToString() => DisplayName;
}