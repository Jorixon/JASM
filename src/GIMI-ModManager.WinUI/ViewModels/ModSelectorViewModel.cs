using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.ViewModels;

public sealed partial class ModSelectorViewModel(ISkinManagerService skinManagerService) : ObservableRecipient
{
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private DispatcherQueue _dispatcherQueue = null!;
    private TaskCompletionSource<SelectionResult?> _taskCompletionSource = null!;

    private List<CharacterSkinEntry> SelectableMods { get; } = new();
    private List<ModModel> _backendModModels = new();

    [ObservableProperty] private ListViewSelectionMode _selectionMode;

    public ObservableCollection<ModModel> SelectedMods { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;

    public ObservableCollection<ModModel> Mods { get; } = new();

    public EventHandler? CloseRequested;

    private CancellationToken _cancellationToken;

    public async Task InitializeAsync(InitOptions options, DispatcherQueue queue,
        TaskCompletionSource<SelectionResult?> taskCompletionSource,
        CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        _dispatcherQueue = queue;
        _taskCompletionSource = taskCompletionSource;
        SelectionMode = options.SelectionMode;

        var modModels = new List<ModModel>();
        await Task.Run(async () =>
        {
            var mods = _skinManagerService.GetAllMods(GetOptions.All);

            foreach (var mod in mods)
            {
                if (options.SelectableMods?.Contains(mod.Id) ?? true)
                {
                    SelectableMods.Add(mod);
                }
            }

            foreach (var characterSkinEntry in SelectableMods)
            {
                var modModel = ModModel.FromMod(characterSkinEntry);

                var modSettings =
                    await characterSkinEntry.Mod.Settings.TryReadSettingsAsync(cancellationToken: cancellationToken);

                if (modSettings is not null)
                    modModel.WithModSettings(modSettings);

                modModels.Add(modModel);
            }
        }, cancellationToken);

        modModels.OrderByDescending(m => m.DateAdded).ForEach(Mods.Add);
        _backendModModels = [.. modModels];

        SelectedMods.CollectionChanged += (sender, args) => SelectModsCommand.NotifyCanExecuteChanged();
    }

    private bool CanSelectMods() => SelectedMods.Count > 0;


    [RelayCommand(CanExecute = nameof(CanSelectMods))]
    private Task SelectMods()
    {
        var selectedMods = SelectedMods.Select(m => m.Id).ToList();
        _taskCompletionSource.SetResult(new SelectionResult(selectedMods));
        CloseRequested?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }


    public void SearchTextChanged(string searchText)
    {
        SearchText = searchText.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            Mods.Clear();
            _backendModModels.ForEach(m => Mods.Add(m));
            return;
        }

        Mods.Clear();

        foreach (var mod in _backendModModels)
        {
            if (mod.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                Mods.Add(mod);
                continue;
            }

            if (mod.Author.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                Mods.Add(mod);
                continue;
            }

            if (mod.CharacterSkinOverride.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                Mods.Add(mod);
                continue;
            }

            if (mod.FolderName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                Mods.Add(mod);
                continue;
            }

            if (mod.Character.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                Mods.Add(mod);
                continue;
            }
        }
    }
}

public class InitOptions
{
    public ICollection<Guid>? SelectableMods { get; set; }
    public ListViewSelectionMode SelectionMode { get; set; } = ListViewSelectionMode.Single;
}

public record SelectionResult(ICollection<Guid> ModIds);