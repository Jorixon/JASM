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
using System.Threading;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class ModPaneVM : ObservableRecipient
{
    private readonly ISkinManagerService _skinManagerService;
    private ISkinMod _selectedSkinMod = null!;
    private ICharacterModList _modList = null!;


    [ObservableProperty] private NewModModel _selectedModModel = null!;
    [ObservableProperty] private bool _isReadOnlyMode = true;


    public ModPaneVM(ISkinManagerService skinManagerService)
    {
        _skinManagerService = skinManagerService;
    }

    public async Task LoadMod(NewModModel modModel, CancellationToken cancellationToken = default)
    {
        if (modModel.Id == SelectedModModel?.Id) return;
        UnloadMod();
        _selectedSkinMod = _skinManagerService.GetCharacterModList(modModel.Character).Mods
            .First(x => x.Id == modModel.Id).Mod;

        SelectedModModel = modModel;

        await ReloadModSettings(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReloadModSettings(CancellationToken cancellationToken = default)
    {
        var skinModSettings = await _selectedSkinMod.ReadSkinModSettings(cancellationToken);

        SelectedModModel.WithModSettings(skinModSettings);


        IsReadOnlyMode = false;


        if (!_selectedSkinMod.HasMergedInI) return;

        var keySwaps = await _selectedSkinMod.ReadKeySwapConfiguration(cancellationToken);

        SelectedModModel.SetKeySwaps(keySwaps);
    }

    public void UnloadMod()
    {
        IsReadOnlyMode = true;
        _selectedSkinMod = null!;
        SelectedModModel = null!;
        _modList = null!;
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
        SelectedModModel.ImagePath = imageUri.ToString();
    }


    [RelayCommand]
    private async Task OpenModFolder() =>
        await Launcher.LaunchFolderAsync(
            await StorageFolder.GetFolderFromPathAsync(_selectedSkinMod.FullPath));

    private bool ModSettingsChanged() => false;

    [RelayCommand(CanExecute = nameof(ModSettingsChanged))]
    private async Task SaveModSettingsAsync()
    {
    }

    private void SettingsPropertiesChanged() => SaveModSettingsCommand.NotifyCanExecuteChanged();
}