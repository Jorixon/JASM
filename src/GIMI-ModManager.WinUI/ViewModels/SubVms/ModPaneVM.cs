using System.Collections.ObjectModel;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.WinUI.Models;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class ModPaneVM : ObservableRecipient
{
    private SkinModSettingsModel _backendSkinModSettings = new();

    private SkinModKeySwapModel[] _backendSkinModKeySwaps = Array.Empty<SkinModKeySwapModel>();

    private readonly ISkinManagerService _skinManagerService;
    private IMod _selectedMod = null!;
    private SkinMod _selectedSkinMod = null!;



    [ObservableProperty] private SkinModSettingsModel _skinModSettings = new();

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

        if (!_selectedSkinMod.HasMergedInI) return;

        var internalKeySwaps = await _selectedSkinMod.ReadKeySwapConfiguration(cancellationToken);
        _backendSkinModKeySwaps = _selectedSkinMod.KeySwaps.Select(SkinModKeySwapModel.FromKeySwapSettings).ToArray();

        foreach (var skinModKeySwapModel in _selectedSkinMod.KeySwaps.Select(SkinModKeySwapModel.FromKeySwapSettings))
        {
            SkinModKeySwaps.Add(skinModKeySwapModel);
        }
    }

    public void UnloadMod()
    {
        _selectedMod = null!;
        _selectedSkinMod = null!;
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
}