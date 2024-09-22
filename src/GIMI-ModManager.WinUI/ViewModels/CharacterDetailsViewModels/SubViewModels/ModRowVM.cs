using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;

public partial class ModRowVM : ObservableObject
{
    public Guid Id { get; init; }
    [ObservableProperty] private bool _isSelected;
    public bool IsEnabled { get; init; }
    public string DisplayName { get; init; }

    public string FolderName { get; init; }

    public string AbsFolderPath { get; }

    public DateTime DateAdded { get; init; }

    public string DateAddedFormated { get; }

    public string Author { get; }

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
        InPresets = string.Join('|', presetNames);


        SearchableText = $"{DisplayName}{FolderName}{Author}{InPresets.Replace("|", "")}{DateAdded:D}";
    }

    public string SearchableText { get; }
}