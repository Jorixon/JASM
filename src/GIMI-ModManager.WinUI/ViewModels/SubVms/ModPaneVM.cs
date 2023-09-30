using System.Diagnostics;
using System.Text.Json;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class ModPaneVM : ObservableRecipient
{
    private readonly ISkinManagerService _skinManagerService;
    private readonly NotificationManager _notificationManager;
    private readonly ILogger _logger = Log.ForContext<ModPaneVM>();
    private ISkinMod _selectedSkinMod = null!;
    private ICharacterModList _modList = null!;

    private NewModModel _backendModModel = null!;

    [ObservableProperty] private NewModModel _selectedModModel = null!;
    [ObservableProperty] private bool _isReadOnlyMode = true;

    [ObservableProperty] private bool _isEditingModName = false;


    public ModPaneVM(ISkinManagerService skinManagerService, NotificationManager notificationManager)
    {
        _skinManagerService = skinManagerService;
        _notificationManager = notificationManager;
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
        if (_selectedSkinMod is null || _backendModModel is null || SelectedModModel is null) return;
        try
        {
            var skinModSettings =
                await _selectedSkinMod.ReadSkinModSettings(true, cancellationToken: cancellationToken);

            _backendModModel.WithModSettings(skinModSettings);
            SelectedModModel.WithModSettings(skinModSettings);
        }
        catch (JsonException e)
        {
            _logger.Error(e, "Error while reading mod settings for {ModName}", _backendModModel.FolderName);
            _notificationManager.ShowNotification("Error while reading mod settings.",
                $"An error occurred while reading the mod settings for {_backendModModel.FolderName}, See logs for details.\n{e.Message}",
                TimeSpan.FromSeconds(10));
        }

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
            skinModKeySwapModel.PropertyChanged += (_, _) => SettingsPropertiesChanged();

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
        IsEditingModName = false;
        SelectedModModel = new NewModModel();
        _modList = null!;
        SettingsPropertiesChanged();
    }

    private string[] _supportedImageExtensions =
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".svg" };

    [RelayCommand]
    private async Task SetImageUriAsync()
    {
        var filePicker = new FileOpenPicker();
        foreach (var supportedImageExtension in _supportedImageExtensions)
            filePicker.FileTypeFilter.Add(supportedImageExtension);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        var file = await filePicker.PickSingleFileAsync();

        if (file == null) return;
        var imageUri = new Uri(file.Path);
        SelectedModModel!.ImagePath = imageUri;
    }

    public Task SetImageFromDragDropFile(IReadOnlyList<IStorageItem> items)
    {
        foreach (var storageItem in items)
        {
            if (storageItem is not StorageFile file) continue;

            if (_supportedImageExtensions.Contains(Path.GetExtension(file.Name)))
            {
                Uri.TryCreate(file.Path, UriKind.Absolute, out var imageUri);

                if (imageUri is null)
                {
                    _notificationManager.ShowNotification("Error setting image",
                        "Could not set image, invalid Uri. Drag and drop can be unreliable in certain situations",
                        TimeSpan.FromSeconds(5));
                    return Task.CompletedTask;
                }

                SelectedModModel!.ImagePath = imageUri;
            }
        }

        return Task.CompletedTask;
    }

    public async Task SetImageFromDragDropWeb(Uri? url)
    {
        if (url is null || !url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            _notificationManager.ShowNotification("Error setting image",
                "Could not set image, invalid Uri. Drag and drop can be unreliable in certain situations",
                TimeSpan.FromSeconds(5));
            return;
        }

        if (!_supportedImageExtensions.Contains(Path.GetExtension(url.AbsolutePath)))
        {
            var invalidExtension = Path.GetExtension(url.AbsolutePath);

            invalidExtension = string.IsNullOrWhiteSpace(invalidExtension)
                ? "Could not get extension"
                : invalidExtension;

            _notificationManager.ShowNotification("Error setting image",
                $"Could not set image, invalid extenstion: {invalidExtension}",
                TimeSpan.FromSeconds(5));
            return;
        }

        var tmpDir = App.TMP_DIR;

        var tmpFile = Path.Combine(tmpDir, $"WEB_DROP_{Guid.NewGuid():N}{Path.GetExtension(url.ToString())}");

        await Task.Run(async () =>
        {
            if (!Directory.Exists(tmpDir))
                Directory.CreateDirectory(tmpDir);

            var client = new HttpClient();
            var responseStream = await client.GetStreamAsync(url);
            await using var fileStream = File.Create(tmpFile);
            await responseStream.CopyToAsync(fileStream);
        });


        var imageUri = new Uri(tmpFile);
        SelectedModModel.ImagePath = imageUri;
    }

    public async Task SetImageFromBitmapStreamAsync(RandomAccessStreamReference accessStreamReference,
        IReadOnlyCollection<string> formats)
    {
        var tmpDir = App.TMP_DIR;

        if (!Directory.Exists(tmpDir))
            Directory.CreateDirectory(tmpDir);

        var tmpFile = Path.Combine(tmpDir, $"CLIPBOARD_PASTE_{Guid.NewGuid():N}");

        var fileExtension = formats.FirstOrDefault(format =>
            _supportedImageExtensions.Append("bitmap").Any(supportedFormat =>
                supportedFormat.Trim('.').Equals(format, StringComparison.OrdinalIgnoreCase)));

        if (fileExtension is null)
        {
            _notificationManager.ShowNotification("Error setting image",
                "Could not set image, invalid extenstion",
                TimeSpan.FromSeconds(5));
            return;
        }

        tmpFile += "." + fileExtension;


        await Task.Run(async () =>
        {
            var stream = await accessStreamReference.OpenReadAsync();
            await using var fileStream = File.Create(tmpFile);
            await stream.AsStreamForRead().CopyToAsync(fileStream);
        });

        SelectedModModel.ImagePath = new Uri(tmpFile);
    }

    [RelayCommand]
    private void ToggleEditingModName()
    {
        IsEditingModName = !IsEditingModName;
    }


    [RelayCommand]
    private async Task OpenModFolder()
    {
        await Launcher.LaunchFolderAsync(
            await StorageFolder.GetFolderFromPathAsync(_selectedSkinMod.FullPath));
    }

    private bool ModSettingsChanged()
    {
        return _backendModModel is not null && !_backendModModel.SettingsEquals(SelectedModModel);
    }

    [RelayCommand(CanExecute = nameof(ModSettingsChanged))]
    private async Task SaveModSettingsAsync(CancellationToken cancellationToken = default)
    {
        IsReadOnlyMode = true;
        var errored = false;
        await Task.Run(async () =>
        {
            var skinModSettings = SelectedModModel.ToModSettings();

            try
            {
                await _selectedSkinMod.SaveSkinModSettings(skinModSettings, cancellationToken);
            }
            catch (Exception e)
            {
                errored = true;
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    _notificationManager.ShowNotification("Error saving mod settings",
                        "An error occurred while saving the mod settings. Please check the log for more details.",
                        TimeSpan.FromSeconds(10));
                    App.GetService<ILogger>().Error(e, "Error saving mod settings");
                });
            }


            if (!_selectedSkinMod.HasMergedInI || !SelectedModModel.SkinModKeySwaps.Any()) return;

            var keySwaps = SelectedModModel.SkinModKeySwaps.Select(x => x.ToKeySwapSettings()).ToList();
            try
            {
                await _selectedSkinMod.SaveKeySwapConfiguration(keySwaps, cancellationToken);
            }
            catch (Exception e)
            {
                errored = true;
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    _notificationManager.ShowNotification("Error saving key swap configuration",
                        "An error occured while saving the key swap configuration. Please check the log for more details.",
                        TimeSpan.FromSeconds(10));
                    App.GetService<ILogger>().Error(e, "Error saving key swap configuration");
                });
            }
        }, cancellationToken);

        IsReadOnlyMode = false;

        await ReloadModSettings(CancellationToken.None);
        IsEditingModName = false;

        if (!errored)
            _notificationManager.ShowNotification("Mod settings saved",
                $"Settings saved for {SelectedModModel.Name}", TimeSpan.FromSeconds(2));
    }

    private void SettingsPropertiesChanged()
    {
        SaveModSettingsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearImage()
    {
        SelectedModModel.ImagePath = NewModModel.PlaceholderImagePath;
    }
}