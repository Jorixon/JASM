using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class ModPaneVM : ObservableRecipient
{
    private readonly ISkinManagerService _skinManagerService = App.GetService<ISkinManagerService>();
    private readonly NotificationManager _notificationManager = App.GetService<NotificationManager>();
    private readonly ILogger _logger = Log.ForContext<ModPaneVM>();

    private readonly KeySwapService _keySwapService = App.GetService<KeySwapService>();
    private readonly ModSettingsService _modSettingsService = App.GetService<ModSettingsService>();

    private ISkinMod _selectedSkinMod = null!;

    private ModModel _backendModModel = null!;

    [ObservableProperty] private ModModel _selectedModModel = null!;
    [ObservableProperty] private bool _isReadOnlyMode = true;

    [ObservableProperty] private bool _isEditingModName = false;

    [ObservableProperty] private string _showKeySwapSection = "true";


    public async Task LoadMod(ModModel modModel, CancellationToken cancellationToken = default)
    {
        if (modModel.Id == SelectedModModel?.Id) return;
        UnloadMod();

        var mod = await Task.Run(() => _skinManagerService.GetModById(modModel.Id), cancellationToken);
        if (mod == null)
        {
            UnloadMod();
            return;
        }

        _selectedSkinMod = mod;

        _backendModModel = ModModel.FromMod(mod, modModel.Character, modModel.IsEnabled);
        SelectedModModel = modModel;
        SelectedModModel.PropertyChanged += (_, _) => SettingsPropertiesChanged();
        await ReloadModSettings(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReloadModSettings(CancellationToken cancellationToken = default)
    {
        if (_selectedSkinMod is null || _backendModModel is null || SelectedModModel is null) return;


        var readSettingsResult =
            await Task.Run(() => _modSettingsService.GetSettingsAsync(_backendModModel.Id),
                cancellationToken);


        if (!readSettingsResult.TryPickT0(out var modSettings, out _))
        {
            UnloadMod();
            return;
        }

        SelectedModModel.WithModSettings(modSettings);
        _backendModModel.WithModSettings(modSettings);


        Debug.Assert(_backendModModel.Equals(SelectedModModel));

        ShowKeySwapSection = modSettings.IgnoreMergedIni ? "false" : "true";


        if (!_selectedSkinMod.Settings.HasMergedIni)
        {
            IsReadOnlyMode = false;
            SelectedModModel.SetKeySwaps(Array.Empty<KeySwapSection>());
            _backendModModel.SetKeySwaps(Array.Empty<KeySwapSection>());
            return;
        }

        var readKeySwapResult =
            await Task.Run(() => _keySwapService.GetKeySwapsAsync(_backendModModel.Id), cancellationToken);

        if (!readKeySwapResult.TryPickT0(out var keySwaps, out _))
        {
            IsReadOnlyMode = false;
            SelectedModModel.SetKeySwaps(Array.Empty<KeySwapSection>());
            _backendModModel.SetKeySwaps(Array.Empty<KeySwapSection>());
            return;
        }

        SelectedModModel.SetKeySwaps(keySwaps);
        _backendModModel.SetKeySwaps(keySwaps);


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
        SelectedModModel = new ModModel();
        SettingsPropertiesChanged();
    }

    private string[] _supportedImageExtensions =
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".svg", ".webp" };

    [RelayCommand]
    private async Task SetImageUriAsync()
    {
        var filePicker = new FileOpenPicker();
        foreach (var supportedImageExtension in _supportedImageExtensions)
            filePicker.FileTypeFilter.Add(supportedImageExtension);

        filePicker.CommitButtonText = "Set Image";
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
            using var stream = await accessStreamReference.OpenReadAsync();
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

    [RelayCommand]
    private async Task SetModIniFileAsync()
    {
        var filePicker = new FileOpenPicker();
        filePicker.FileTypeFilter.Add(".ini");
        filePicker.CommitButtonText = "Set";
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);
        var file = await filePicker.PickSingleFileAsync();

        if (file is null)
        {
            _logger.Debug("User cancelled file picker.");
            return;
        }

        var result = await Task.Run(() => _modSettingsService.SetModIniAsync(_selectedSkinMod.Id, file.Path));


        if (result.Notification is not null)
            _notificationManager.ShowNotification(result.Notification);

        _selectedSkinMod.ClearCache();
        await ReloadModSettings(CancellationToken.None).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ClearSetModIniFileAsync()
    {
        var autoDetect = SelectedModModel.IgnoreMergedIni;

        var result = await Task.Run(() =>
            _modSettingsService.SetModIniAsync(_selectedSkinMod.Id, string.Empty, autoDetect));


        if (result.Notification is not null)
            _notificationManager.ShowNotification(result.Notification);

        _selectedSkinMod.ClearCache();
        await ReloadModSettings(CancellationToken.None).ConfigureAwait(false);
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


        var saveResult =
            await Task.Run(() => _modSettingsService.SaveSettingsAsync(SelectedModModel), cancellationToken);

        if (saveResult.TryPickT2(out var error, out var notFoundOrSuccess))
        {
            errored = true;
        }
        else if (notFoundOrSuccess.TryPickT1(out var modNotFound, out _))
        {
            _notificationManager.ShowNotification("Error saving mod settings",
                $"Could not find mod with id {modNotFound.ModId}",
                TimeSpan.FromSeconds(5));
        }

        if (_selectedSkinMod.Settings.HasMergedIni || SelectedModModel.SkinModKeySwaps.Any())
        {
            var saveKeySwapResult = await Task.Run(() => _keySwapService.SaveKeySwapsAsync(SelectedModModel),
                cancellationToken);

            saveKeySwapResult.Switch(
                success => { },
                missingIni =>
                {
                    errored = true;
                    _notificationManager.ShowNotification("Error saving keyswap",
                        $"Could not find ini file for mod {SelectedModModel.Name}",
                        TimeSpan.FromSeconds(5));
                },
                modNotFound =>
                {
                    errored = true;
                    _notificationManager.ShowNotification("Error saving mod settings",
                        $"Could not find mod with id {modNotFound.ModId}",
                        TimeSpan.FromSeconds(5));
                },
                _ => { }
            );
        }


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
        SelectedModModel.ImagePath = ModModel.PlaceholderImagePath;
    }
}