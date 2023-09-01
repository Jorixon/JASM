using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.WinUI.Models;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class ModListVM : ObservableRecipient
{
    private readonly ISkinManagerService _skinManagerService;
    public readonly ObservableCollection<NewModModel> BackendMods = new();

    public ObservableCollection<NewModModel> SelectedMods { get; } = new();
    [ObservableProperty] private InfoBarSeverity _severity = InfoBarSeverity.Warning;

    [ObservableProperty] private bool _isInfoBarOpen;

    [ObservableProperty] private string _infoBarMessage = string.Empty;

    [ObservableProperty] private int _selectedModsCount;

    public ObservableCollection<NewModModel> Mods { get; } = new();

    public bool DisableInfoBar { get; set; } = false;

    public ModListVM(ISkinManagerService skinManagerService)
    {
        _skinManagerService = skinManagerService;
        Mods.CollectionChanged += Mods_CollectionChanged;
    }

    private void Mods_CollectionChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (NewModModel item in e.NewItems)
            {
                item.PropertyChanged += (o, args) =>
                {
                    if (args.PropertyName != nameof(NewModModel.IsEnabled)) return;

                    if (Mods.Count(x => x.IsEnabled) > 1)
                    {
                        SetInfoBarMessage("More than one skin enabled", InfoBarSeverity.Warning);
                    }
                    else
                    {
                        ResetInfoBar();
                    }
                };
            }
        }
    }

    public void SetBackendMods(IEnumerable<NewModModel> mods)
    {
        BackendMods.Clear();
        foreach (var mod in mods)
        {
            BackendMods.Add(mod);
        }
    }

    public void ReplaceMods(IEnumerable<NewModModel> mods)
    {
        Mods.Clear();
        foreach (var mod in mods)
        {
            Mods.Add(mod);
        }
    }

    public void ResetContent()
    {
        Mods.Clear();
        foreach (var mod in BackendMods)
        {
            Mods.Add(mod);
        }

        if (Mods.Count(x => x.IsEnabled) > 1)
            SetInfoBarMessage("More than one skin enabled", InfoBarSeverity.Warning);
        else
            ResetInfoBar();
    }

    public void SelectionChanged(ICollection<NewModModel> selectedMods, ICollection<NewModModel> removedMods)
    {
        if (selectedMods.Any())
        {
            foreach (var mod in selectedMods)
            {
                if (!SelectedMods.Contains(mod))
                    SelectedMods.Add(mod);
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

        SelectedModsCount = SelectedMods.Count;
    }

    public void SetInfoBarMessage(string message, InfoBarSeverity severity, bool openInfoBar = true)
    {
        if (DisableInfoBar) return;
        InfoBarMessage = message;
        Severity = severity;
        IsInfoBarOpen = openInfoBar;
    }

    public void ResetInfoBar()
    {
        InfoBarMessage = string.Empty;
        Severity = InfoBarSeverity.Informational;
        IsInfoBarOpen = false;
    }
}