using System.Collections.ObjectModel;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.WinUI.Models;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.System;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class ModPaneVM : ObservableRecipient
{
    private SkinModSettingsModel _backendSkinModSettings = new();

    private SkinModKeySwapModel[] _backendSkinModKeySwaps = Array.Empty<SkinModKeySwapModel>();

    private readonly ISkinManagerService _skinManagerService;
    private IMod _selectedMod = null!;
    private SkinMod _selectedSkinMod = null!;


    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SaveModSettingsCommand))]
    private SkinModSettingsModel _skinModSettings = new();

    [ObservableProperty] private bool _isReadOnlyMode = true;


    public ObservableCollection<SkinModKeySwapModel> SkinModKeySwaps { get; set; } =
        new ObservableCollection<SkinModKeySwapModel>();

    [ObservableProperty] private NewModModel _selectedModModel = null!;

    public ModPaneVM(ISkinManagerService skinManagerService)
    {
        _skinManagerService = skinManagerService;
    }

    public async Task LoadMod(IMod mod, NewModModel modModel, CancellationToken cancellationToken = default)
    {
        if (mod == _selectedMod) return;
        UnloadMod();

        _selectedMod = mod;
        _selectedSkinMod = new SkinMod(_selectedMod);
        SelectedModModel = modModel;

        var internalSettings = await _selectedSkinMod.ReadSkinModSettings(cancellationToken);
        _backendSkinModSettings = SkinModSettingsModel.FromMod(internalSettings);
        SkinModSettings = SkinModSettingsModel.FromMod(internalSettings);

        IsReadOnlyMode = false;

        SkinModSettings.PropertyChanged += (_, _) => SettingsPropertiesChanged();

        if (!_selectedSkinMod.HasMergedInI) return;

        var internalKeySwaps = await _selectedSkinMod.ReadKeySwapConfiguration(cancellationToken);
        _backendSkinModKeySwaps = _selectedSkinMod.KeySwaps.Select(SkinModKeySwapModel.FromKeySwapSettings).ToArray();

        foreach (var skinModKeySwapModel in _selectedSkinMod.KeySwaps.Select(SkinModKeySwapModel.FromKeySwapSettings))
        {
            skinModKeySwapModel.PropertyChanged += (_, _) => SettingsPropertiesChanged();
            SkinModKeySwaps.Add(skinModKeySwapModel);
        }

        SkinModKeySwaps.CollectionChanged += (_, _) => SettingsPropertiesChanged();
    }

    public void UnloadMod()
    {
        IsReadOnlyMode = true;
        SkinModSettings.PropertyChanged -= (_, _) => SettingsPropertiesChanged();
        foreach (var skinModKeySwapModel in SkinModKeySwaps)
        {
            skinModKeySwapModel.PropertyChanged -= (_, _) => SettingsPropertiesChanged();
        }

        SkinModKeySwaps.CollectionChanged -= (_, _) => SettingsPropertiesChanged();


        _selectedMod = null!;
        _selectedSkinMod = null!;
        SelectedModModel = null!;
        _backendSkinModSettings = new SkinModSettingsModel();
        SkinModSettings = new SkinModSettingsModel();
        _backendSkinModKeySwaps = Array.Empty<SkinModKeySwapModel>();
        SkinModKeySwaps.Clear();
    }

    [RelayCommand]
    private async Task SetImageUriAsync()
    {
        var filePicker = new FileOpenPicker();
        filePicker.FileTypeFilter.Add(".png");
        filePicker.FileTypeFilter.Add(".jpg");
        filePicker.FileTypeFilter.Add(".jpeg");
        filePicker.FileTypeFilter.Add(".bmp");
        filePicker.FileTypeFilter.Add(".gif");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        var file = await filePicker.PickSingleFileAsync();

        if (file == null) return;
        var imageUri = new Uri(file.Path);
        SkinModSettings.ImageUri = imageUri.ToString();
    }


    [RelayCommand]
    private async Task OpenModFolder() =>
        await Launcher.LaunchFolderAsync(
            await StorageFolder.GetFolderFromPathAsync(_selectedMod.FullPath));

    private bool ModSettingsChanged() => !_backendSkinModSettings.Equals(SkinModSettings) ||
                                         !_backendSkinModKeySwaps.SequenceEqual(SkinModKeySwaps);

    [RelayCommand(CanExecute = nameof(ModSettingsChanged))]
    private async Task SaveModSettingsAsync()
    {
        if (!_backendSkinModSettings.Equals(SkinModSettings))
        {
            await _selectedSkinMod.SaveSkinModSettings(SkinModSettings.ToModSettings());
        }

        if (!_backendSkinModKeySwaps.SequenceEqual(SkinModKeySwaps))
        {
            var keySwapSettings = SkinModKeySwaps.Select(x => x.ToKeySwapSettings()).ToArray();
            await _selectedSkinMod.SaveKeySwapConfiguration(keySwapSettings);
        }
        
    }

    private void SettingsPropertiesChanged() => SaveModSettingsCommand.NotifyCanExecuteChanged();
}