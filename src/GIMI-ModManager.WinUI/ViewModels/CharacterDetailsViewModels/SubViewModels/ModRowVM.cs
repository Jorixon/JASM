using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.WinUI.Helpers;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;

public partial class ModRowVM : ObservableObject
{
    public Guid Id { get; init; }
    [ObservableProperty] private bool _isSelected;

    [ObservableProperty] private bool _isEnabled;
    public string DisplayName { get; init; }

    [ObservableProperty] private string _folderName;

    [ObservableProperty] private string _absFolderPath;

    public DateTime DateAdded { get; init; }

    public string DateAddedFormated { get; }

    public string Author { get; }

    public string[] Presets { get; }
    public string InPresets { get; }

    public ModRowVM(CharacterSkinEntry characterSkinEntry, ModSettings? modSettings, IEnumerable<string> presetNames)
    {
        Id = characterSkinEntry.Mod.Id;
        IsEnabled = characterSkinEntry.IsEnabled;
        DisplayName = modSettings?.CustomName ?? characterSkinEntry.Mod.GetDisplayName();
        FolderName = characterSkinEntry.Mod.Name;
        AbsFolderPath = characterSkinEntry.Mod.FullPath;
        DateAdded = modSettings?.DateAdded ?? DateTime.MinValue;
        DateAddedFormated = DateAdded.ToString("d");
        Author = modSettings?.Author ?? string.Empty;
        Presets = presetNames.ToArray();
        InPresets = string.Join('|', Presets);


        SearchableText = $"{DisplayName}{FolderName}{Author}{string.Join(null, Presets)}{DateAdded:D}";
    }

    public string SearchableText { get; }

    public required IAsyncRelayCommand ToggleEnabledCommand { get; init; }
}

public sealed class ModGridSortingMethod : SortingMethod<ModRowVM>
{
    public ModGridSortingMethod(Sorter<ModRowVM> sortingMethodType, ModRowVM? firstItem = null,
        ICollection<ModRowVM>? lastItems = null) : base(sortingMethodType, firstItem, lastItems)
    {
    }

    public static readonly ModRowSorter[] AllSorters =
    [
        ModRowSorter.IsEnabledSorter, ModRowSorter.DatedAddedSorter, ModRowSorter.DisplayNameSorter,
        ModRowSorter.ModFolderSorter, ModRowSorter.AuthSorter, ModRowSorter.PresetsSorter
    ];
}

public sealed class ModRowSorter : Sorter<ModRowVM>
{
    private ModRowSorter(string sortingMethodType, SortFunc firstSortFunc, AdditionalSortFunc? secondSortFunc = null,
        AdditionalSortFunc? thirdSortFunc = null) : base(sortingMethodType, firstSortFunc, secondSortFunc,
        thirdSortFunc)
    {
    }

    public static readonly string IsEnabledName = nameof(ModRowVM.IsEnabled);

    public static ModRowSorter IsEnabledSorter { get; } = new(IsEnabledName,
        (mod, isDescending) =>
            isDescending
                ? mod.OrderByDescending(x => x.IsEnabled).ThenByDescending(x => x.DateAdded)
                    .ThenBy(x => x.DisplayName)
                : mod.OrderBy(x => x.IsEnabled).ThenByDescending(x => x.DateAdded)
                    .ThenBy(x => x.DisplayName)
    );


    public static readonly string DateAddedName = nameof(ModRowVM.DateAdded);

    public static ModRowSorter DatedAddedSorter { get; } = new(DateAddedName,
        (mod, isDescending) =>
            isDescending
                ? mod.OrderByDescending(x => (x.DateAdded)).ThenBy(x => x.DisplayName)
                : mod.OrderBy(x => (x.DateAdded)).ThenBy(x => x.DisplayName));


    public static readonly string DisplayName = nameof(ModRowVM.DisplayName);

    public static ModRowSorter DisplayNameSorter { get; } = CreateStringSorter(DisplayName, vm => vm.DisplayName);

    public static readonly string ModFolderName = nameof(ModRowVM.FolderName);

    public static ModRowSorter ModFolderSorter { get; } = CreateStringSorter(ModFolderName, vm => vm.FolderName);


    public static readonly string AuthorName = nameof(ModRowVM.Author);

    public static ModRowSorter AuthSorter { get; } = CreateStringSorter(AuthorName, vm => vm.Author);


    public static readonly string PresetsName = nameof(ModRowVM.Presets);

    public static ModRowSorter PresetsSorter { get; } = new(PresetsName,
        (mod, isDescending) =>
            isDescending
                ? mod.OrderByDescending(x => (x.Presets.Length))
                    .ThenByDescending(x => x.DateAdded)
                    .ThenBy(x => x.DisplayName)
                : mod.OrderBy(x => (x.Presets.Length))
                    .ThenByDescending(x => x.DateAdded)
                    .ThenBy(x => x.DisplayName));


    private static ModRowSorter CreateStringSorter(string name, Func<ModRowVM, string> predicate) => new(name,
        (mod, isDescending) =>
            isDescending
                ? mod.OrderByDescending(predicate).ThenByDescending(x => x.DateAdded)
                : mod.OrderBy(predicate).ThenByDescending(x => x.DateAdded));
}