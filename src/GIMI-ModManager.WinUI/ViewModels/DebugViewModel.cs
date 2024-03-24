using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class DebugViewModel(
    ISkinManagerService skinManagerService,
    ModPresetService modPresetService,
    ILocalSettingsService localSettingsService) : ObservableRecipient, INavigationAware
{
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly ModPresetService _modPresetService = modPresetService;
    private readonly ILocalSettingsService _localSettingsService = localSettingsService;

    [ObservableProperty] private string _title = string.Empty;

    [ObservableProperty] private PresetVm? _selectedPreset = null;

    [RelayCommand]
    private async Task CreatePreset()
    {
        if (string.IsNullOrWhiteSpace(Title) || SelectedPreset is not null)
            return;

        await Task.Run(() => _modPresetService.CreatePresetAsync(Title)).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task SavePreset()
    {
        if (SelectedPreset == null)
            return;

        await Task.Run(() => _modPresetService.SaveCurrentModList(SelectedPreset.PresetName)).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ApplyPreset()
    {
        if (SelectedPreset == null)
            return;

        await Task.Run(() => _modPresetService.ApplyPresetAsync(SelectedPreset.PresetName)).ConfigureAwait(false);
    }


    public async void OnNavigatedTo(object parameter)
    {
        var presets = _modPresetService.GetPresets().ToArray();
        var first = presets.FirstOrDefault();
        if (first != null)
        {
            SelectedPreset = new PresetVm(first);
            Title = first.Name;
        }
    }

    public void OnNavigatedFrom()
    {
    }
}

public partial class PresetVm : ObservableObject
{
    public PresetVm(ModPreset preset)
    {
        _presetName = preset.Name;
        _modEntries = new ObservableCollection<ModEntryVm>(preset.Mods.Select(x => new ModEntryVm(x)));
    }

    public PresetVm()
    {
    }

    [ObservableProperty] private string _presetName = string.Empty;

    [ObservableProperty] private ObservableCollection<ModEntryVm> _modEntries = new();


    public class ModEntryVm(ModPresetEntry modPresetEntry)
    {
        public string Name { get; set; } = modPresetEntry.Name;
    }
}