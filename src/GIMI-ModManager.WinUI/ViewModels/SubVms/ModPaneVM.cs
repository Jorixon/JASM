using System.Diagnostics;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.WinUI.Models;
using Windows.Storage;
using Windows.System;
using GIMI_ModManager.Core.Services;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class ModPaneVM : ObservableRecipient
{
    private readonly ISkinManagerService _skinManagerService;
    private ISkinMod _selectedSkinMod = null!;
    private ICharacterModList _modList = null!;

    private NewModModel _backendModModel = null!;

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
        var skinEntry = _skinManagerService.GetCharacterModList(modModel.Character).Mods
            .First(x => x.Id == modModel.Id);
        _selectedSkinMod = skinEntry.Mod;

        _backendModModel = NewModModel.FromMod(skinEntry);
        SelectedModModel = modModel;
        SelectedModModel.PropertyChanged += (_, _) => SettingsPropertiesChanged();

        await ReloadModSettings(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReloadModSettings(CancellationToken cancellationToken = default)
    {
        var skinModSettings = await _selectedSkinMod.ReadSkinModSettings(cancellationToken: cancellationToken);

        _backendModModel.WithModSettings(skinModSettings);
        SelectedModModel.WithModSettings(skinModSettings);

        Debug.Assert(_backendModModel.Equals(SelectedModModel));


        if (!_selectedSkinMod.HasMergedInI)
        {
            IsReadOnlyMode = false;
            return;
        }

        var keySwaps = await _selectedSkinMod.ReadKeySwapConfiguration(cancellationToken: cancellationToken);
        _backendModModel.SetKeySwaps(keySwaps);
        SelectedModModel.SetKeySwaps(keySwaps);
        foreach (var skinModKeySwapModel in SelectedModModel.SkinModKeySwaps)
        {
            skinModKeySwapModel.PropertyChanged += (_, _) => SettingsPropertiesChanged();
        }

        IsReadOnlyMode = false;

        Debug.Assert(_backendModModel.Equals(SelectedModModel));
    }

    public void UnloadMod()
    {
        if (SelectedModModel is not null)
            // ReSharper disable once EventUnsubscriptionViaAnonymousDelegate
            SelectedModModel.PropertyChanged -= (_, _) => SettingsPropertiesChanged();
        SelectedModModel?.SkinModKeySwaps.Clear();
        IsReadOnlyMode = true;
        _selectedSkinMod = null!;
        _backendModModel = null!;
        SelectedModModel = new NewModModel();
        _modList = null!;
        SettingsPropertiesChanged();
    }

    private string[] _supportedImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    [RelayCommand]
    private async Task SetImageUriAsync()
    {
        var filePicker = new FileOpenPicker();
        foreach (var supportedImageExtension in _supportedImageExtensions)
        {
            filePicker.FileTypeFilter.Add(supportedImageExtension);
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        var file = await filePicker.PickSingleFileAsync();

        if (file == null) return;
        var imageUri = new Uri(file.Path);
        SelectedModModel.ImagePath = imageUri.ToString();
    }

    public void SetImageFromDragDrop(IReadOnlyList<IStorageItem> items)
    {
        foreach (var storageItem in items)
        {
            if (storageItem is not StorageFile file) continue;

            if (_supportedImageExtensions.Contains(Path.GetExtension(file.Name)))
            {
                SelectedModModel.ImagePath = new Uri(file.Path).ToString();
            }
        }
    }


    [RelayCommand]
    private async Task OpenModFolder() =>
        await Launcher.LaunchFolderAsync(
            await StorageFolder.GetFolderFromPathAsync(_selectedSkinMod.FullPath));

    private bool ModSettingsChanged() =>
        _backendModModel is not null && !_backendModModel.SettingsEquals(SelectedModModel);

    [RelayCommand(CanExecute = nameof(ModSettingsChanged))]
    private async Task SaveModSettingsAsync(CancellationToken cancellationToken = default)
    {
        IsReadOnlyMode = true;
        await Task.Run(async () =>
        {
            var skinModSettings = SelectedModModel.ToModSettings();
            await _selectedSkinMod.SaveSkinModSettings(skinModSettings, cancellationToken);


            if (!_selectedSkinMod.HasMergedInI || !SelectedModModel.SkinModKeySwaps.Any()) return;

            var keySwaps = SelectedModModel.SkinModKeySwaps.Select(x => x.ToKeySwapSettings()).ToList();
            await _selectedSkinMod.SaveKeySwapConfiguration(keySwaps, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

        IsReadOnlyMode = false;
        await ReloadModSettings(CancellationToken.None).ConfigureAwait(false);
    }

    private void SettingsPropertiesChanged() => SaveModSettingsCommand.NotifyCanExecuteChanged();
}