using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class ModListVM : ObservableRecipient
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly ModNotificationManager _modNotificationManager;
    public readonly ObservableCollection<ModModel> BackendMods = new();

    public ObservableCollection<ModModel> SelectedMods { get; } = new();
    [ObservableProperty] private InfoBarSeverity _severity = InfoBarSeverity.Warning;

    [ObservableProperty] private bool _isInfoBarOpen;

    [ObservableProperty] private string _infoBarMessage = string.Empty;

    [ObservableProperty] private int _selectedModsCount;

    public ObservableCollection<ModModel> Mods { get; } = new();

    public bool DisableInfoBar { get; set; } = false;

    public ModListVM(ISkinManagerService skinManagerService, ModNotificationManager modNotificationManager)
    {
        _skinManagerService = skinManagerService;
        _modNotificationManager = modNotificationManager;
        Mods.CollectionChanged += Mods_CollectionChanged;
    }


    private void Mods_CollectionChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (ModModel item in e.NewItems)
            {
                item.PropertyChanged += (o, args) =>
                {
                    if (args.PropertyName != nameof(ModModel.IsEnabled)) return;

                    if (Mods.Count(x => x.IsEnabled) > 1)
                    {
                        SetInfoBarMessage("More than one skin mod enabled", InfoBarSeverity.Warning);
                    }
                    else
                    {
                        ResetInfoBar();
                    }
                };
            }
        }
    }

    public void SetBackendMods(IEnumerable<ModModel> mods)
    {
        BackendMods.Clear();
        foreach (var mod in mods)
        {
            BackendMods.Add(mod);
        }
    }

    public void ReplaceMods(IEnumerable<ModModel> mods)
    {
        Mods.Clear();
        foreach (var mod in mods)
        {
            Mods.Add(mod);
        }
    }


    public class SortMethod
    {
        public SortMethod(string propertyName, bool isDescending = false)
        {
            PropertyName = propertyName;
            IsDescending = isDescending;
        }

        public bool IsDescending { get; }
        public string PropertyName { get; }
    }

    public void ResetContent(SortMethod? sortMethod = null)
    {
        Mods.Clear();
        var isEnabledComparer = new ModEnabledComparer();

        if (sortMethod is not null)
        {
            isEnabledComparer.IsDescending = sortMethod.IsDescending;

            void AddMods(IEnumerable<ModModel> mods)
            {
                foreach (var mod in mods)
                {
                    Mods.Add(mod);
                }
            }

            switch (sortMethod.PropertyName)
            {
                case nameof(ModModel.IsEnabled):
                    AddMods(sortMethod.IsDescending
                        ? BackendMods.OrderByDescending(modModel => modModel, isEnabledComparer)
                        : BackendMods.OrderBy(modModel => modModel, isEnabledComparer));

                    break;

                case nameof(ModModel.Name):
                    AddMods(sortMethod.IsDescending
                        ? BackendMods.OrderByDescending(modModel => modModel.Name)
                        : BackendMods.OrderBy(modModel => modModel.Name));

                    break;

                case nameof(ModModel.FolderName):
                    AddMods(sortMethod.IsDescending
                        ? BackendMods.OrderByDescending(modModel => modModel.FolderName)
                        : BackendMods.OrderBy(modModel => modModel.FolderName));
                    break;

                default:
                    Debug.Assert(false, "Unknown sort method");
                    AddMods(BackendMods.OrderBy(modModel => modModel.Name));
                    break;
            }
        }
        else
        {
            foreach (var mod in BackendMods.OrderBy(newModModel => newModModel.Name))
            {
                Mods.Add(mod);
            }
        }


        if (Mods.Count(x => x.IsEnabled) > 1)
            SetInfoBarMessage("More than one skin enabled", InfoBarSeverity.Warning);
        else
            ResetInfoBar();
    }

    public void SelectionChanged(ICollection<ModModel> selectedMods, ICollection<ModModel> removedMods)
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
        OnModsSelected?.Invoke(this, new ModSelectedEventArgs(SelectedMods));
    }

    public event EventHandler<ModSelectedEventArgs>? OnModsSelected;

    public class ModSelectedEventArgs : EventArgs
    {
        public ModSelectedEventArgs(IEnumerable<ModModel> mods)
        {
            Mods = mods.ToArray();
        }

        public ICollection<ModModel> Mods { get; }
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

public sealed class ModEnabledComparer : IComparer<ModModel>
{
    public bool IsDescending;


    public int Compare(ModModel? x, ModModel? y)
    {
        if (x is null || y is null) return 0;
        if (x.IsEnabled == y.IsEnabled)
        {
            var nameComparison = x.Name.CompareTo(y.Name);
            if (IsDescending && nameComparison != 0)
                return -nameComparison;

            return nameComparison;
        }

        return x.IsEnabled.CompareTo(y.IsEnabled);
    }
}