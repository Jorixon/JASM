using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkitWrapper;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Dispatching;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;

public sealed partial class ModPaneVM(
    ISkinManagerService skinManagerService,
    NotificationManager notificationService,
    ModSettingsService modSettingsService,
    ImageHandlerService imageHandlerService)
    : ObservableRecipient, IRecipient<ModChangedMessage>
{
    private readonly ILogger _logger = Log.ForContext<ModPaneVM>();
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly NotificationManager _notificationService = notificationService;
    private readonly ModSettingsService _modSettingsService = modSettingsService;
    private readonly ImageHandlerService _imageHandlerService = imageHandlerService;

    private readonly AsyncLock _loadModLock = new();
    private CancellationToken _cancellationToken = new();
    private DispatcherQueue _dispatcherQueue = null!;


    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotReadOnly))]
    private bool _isReadOnly = true;

    [ObservableProperty] private bool _isEditingModName;

    public bool IsNotReadOnly => !IsReadOnly;

    private Guid? _loadedModId;
    private CharacterSkinEntry? _loadedMod;

    [MemberNotNullWhen(true, nameof(_loadedModId), nameof(_loadedMod))]
    public bool IsModLoaded => _loadedModId != null && ModModel.IsLoaded && _loadedMod != null;

    [ObservableProperty] private ModPaneFieldsVm _modModel = new();


    public bool QueueLoadMod(Guid? modId, bool force = false) => _channel.Writer.TryWrite(new LoadModMessage { ModId = modId, Force = force });


    private readonly Channel<LoadModMessage> _channel = Channel.CreateBounded<LoadModMessage>(
        new BoundedChannelOptions(1)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    private async Task ModLoaderLoopAsync()
    {
        // Runs on the UI thread
        await foreach (var loadModMessage in _channel.Reader.ReadAllAsync(_cancellationToken))
        {
            using var _ = await LockAsync().ConfigureAwait(false);
            IsReadOnly = true;
            IsEditingModName = false;
            try
            {
                if (loadModMessage.ModId is null)
                {
                    await UnloadModAsync();
                    NotifyAllCommands();
                    continue;
                }

                await LoadModAsync(loadModMessage.ModId.Value, loadModMessage.Force);
                NotifyAllCommands();
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception e)
            {
                _notificationService.ShowNotification("Error loading mod", e.Message, null);
            }
        }
    }

    private async Task LoadModAsync(Guid modId, bool force)
    {
        if (modId == _loadedModId && force == false)
            return;

        var modEntry = _skinManagerService.GetModEntryById(modId);
        if (modEntry == null)
            return;

        var mod = modEntry.Mod;

        var modSettings =
            await mod.Settings.TryReadSettingsAsync(useCache: false, cancellationToken: _cancellationToken);

        if (modSettings is null)
            return;

        ICollection<KeySwapSection>? keySwaps = null;
        try
        {
            if (mod.KeySwaps is not null)
                keySwaps = (await mod.KeySwaps.ReadKeySwapConfiguration(_cancellationToken)).ToArray();
        }
        catch (Exception e)
        {
            _notificationService.ShowNotification($"Failed to load keyswaps for mod {mod.GetDisplayName()}", e.Message, null);
        }

        _loadedMod = modEntry;
        ModModel = ModPaneFieldsVm.FromModEntry(modEntry, modSettings, keySwaps ?? []);
        ModModel.PropertyChanged += ModModel_PropertyChanged;
        _loadedModId = modId;
        IsReadOnly = false;
    }

    private void ModModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) => SaveModSettingsCommand.NotifyCanExecuteChanged();

    private Task UnloadModAsync()
    {
        // Unload mod
        _loadedModId = null;
        _loadedMod = null;
        if (ModModel.IsLoaded)
            ModModel.PropertyChanged -= ModModel_PropertyChanged;
        ModModel = new ModPaneFieldsVm();
        return Task.CompletedTask;
    }


    private readonly record struct LoadModMessage
    {
        public Guid? ModId { get; init; }
        public bool Force { get; init; }
    }

    public void Receive(ModChangedMessage message)
    {
        if (!IsModLoaded)
            return;

        if (message.SkinEntry.Id != _loadedModId)
            return;

        if (message.sender == this)
            return;

        QueueLoadMod(message.SkinEntry.Id, true);
    }

    public Task OnNavigatedToAsync(DispatcherQueue dispatcherQueue, CancellationToken navigationCt)
    {
        _dispatcherQueue = dispatcherQueue;
        _cancellationToken = navigationCt;
        _dispatcherQueue.EnqueueAsync(ModLoaderLoopAsync);
        Messenger.RegisterAll(this);
        return Task.CompletedTask;
    }

    public void OnNavigatedFrom()
    {
        _channel.Writer.TryComplete();
        Messenger.UnregisterAll(this);
    }

    #region Commands

    private bool CanSetModIniFile() => IsModLoaded && IsNotReadOnly;

    [RelayCommand(CanExecute = nameof(CanSetModIniFile))]
    private async Task SetModIniFileAsync()
    {
        if (!IsModLoaded) return;
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

        var result = await Task.Run(() => _modSettingsService.SetModIniAsync(_loadedMod.Id, file.Path));


        if (result.Notification is not null)
            _notificationService.ShowNotification(result.Notification);

        _loadedMod.Mod.ClearCache();

        Messenger.Send(new ModChangedMessage(this, _loadedMod, null));
        QueueLoadMod(_loadedModId, true);
    }

    private bool CanClearSetModIniFile() => IsModLoaded && IsNotReadOnly;

    [RelayCommand(CanExecute = nameof(CanClearSetModIniFile))]
    private async Task ClearSetModIniFileAsync()
    {
        await CommandWrapper(async () =>
        {
            if (!IsModLoaded) return;

            var autoDetect = ModModel.IgnoreMergedIni;
            var result = await Task.Run(() =>
                _modSettingsService.SetModIniAsync(_loadedModId.Value, string.Empty, autoDetect), _cancellationToken);


            if (result.Notification is not null)
                _notificationService.ShowNotification(result.Notification);

            _loadedMod.Mod.ClearCache();

            Messenger.Send(new ModChangedMessage(this, _loadedMod, null));
            QueueLoadMod(_loadedModId, true);
        }).ConfigureAwait(false);
    }

    private bool CanPickImageUri() => IsModLoaded && IsNotReadOnly;

    [RelayCommand(CanExecute = nameof(CanPickImageUri))]
    private async Task PickImageUriAsync()
    {
        if (!IsModLoaded) return;
        var filePicker = new FileOpenPicker();
        foreach (var supportedImageExtension in Constants.SupportedImageExtensions)
            filePicker.FileTypeFilter.Add(supportedImageExtension);

        filePicker.CommitButtonText = "Set Image";
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        var file = await filePicker.PickSingleFileAsync();

        if (file == null) return;
        var imageUri = new Uri(file.Path);
        ModModel.ImageUri = imageUri;
    }

    private bool CanCopyImageToClipboard() => IsModLoaded && IsNotReadOnly;

    [RelayCommand(CanExecute = nameof(CanCopyImageToClipboard))]
    private async Task CopyImageToClipboardAsync()
    {
        await CommandWrapper(async () =>
        {
            if (!File.Exists(ModModel.ImageUri.LocalPath))
                return;

            var file = await StorageFile.GetFileFromPathAsync(ModModel.ImageUri.LocalPath);
            if (file is null)
                return;


            await ImageHandlerService.CopyImageToClipboardAsync(file).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }


    private bool CanClearImage() => IsModLoaded && IsNotReadOnly && ModModel.ImageUri != ImageHandlerService.StaticPlaceholderImageUri;

    [RelayCommand(CanExecute = nameof(CanClearImage))]
    private void ClearImage()
    {
        if (!IsModLoaded) return;
        ModModel.ImageUri = ImageHandlerService.StaticPlaceholderImageUri;
    }

    private bool CanSaveModSettings() => IsModLoaded && ModModel.AnyChanges && IsNotReadOnly;

    [RelayCommand(CanExecute = nameof(CanSaveModSettings))]
    private async Task SaveModSettingsAsync()
    {
        await CommandWrapper(async () =>
        {
            if (!IsModLoaded) return;


            var updateRequest = new UpdateSettingsRequest();

            if (ModModel.IsImageUriChanged)
                updateRequest.SetImagePath = ModModel.ImageUri;

            if (ModModel.IsModDisplayNameChanged)
                updateRequest.SetCustomName = ModModel.ModDisplayName;

            if (ModModel.IsModUrlChanged)
                updateRequest.SetModUrl = Uri.TryCreate(ModModel.ModUrl, UriKind.Absolute, out var url) ? url : null;

            Result<ModSettings>? result = null;
            Exception? savingKeySwapException = null;

            await Task.Run(async () =>
            {
                if (updateRequest.AnyUpdates)
                {
                    result = await _modSettingsService.SaveSettingsAsync(_loadedModId.Value, updateRequest).ConfigureAwait(false);
                }

                if (!_loadedMod.Mod.Settings.HasMergedIni && !ModModel.KeySwaps.Any() || _loadedMod.Mod.KeySwaps is null)
                    return;

                // TODO: Will need to redo keyswap handling at some point doing a quick solution here
                var keySwapSections = new List<KeySwapSection>();

                foreach (var modModelSkinModKeySwap in ModModel.KeySwaps)
                {
                    var variants = int.TryParse(modModelSkinModKeySwap.VariationsCount, out var variantsCount)
                        ? variantsCount
                        : -1;

                    var keySwapSection = new KeySwapSection()
                    {
                        SectionName = modModelSkinModKeySwap.SectionKey,
                        ForwardKey = modModelSkinModKeySwap.ForwardHotkey,
                        BackwardKey = modModelSkinModKeySwap.BackwardHotkey,
                        Variants = variants == -1 ? null : variants,
                        Type = modModelSkinModKeySwap.Type ?? "Unknown"
                    };

                    keySwapSections.Add(keySwapSection);
                }

                try
                {
                    await _loadedMod.Mod.KeySwaps.SaveKeySwapConfiguration(keySwapSections).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    savingKeySwapException = e;
                    _logger.Error(e, "An error occured trying to save keyswaps for mod {ModPath}", _loadedMod.Mod.FullPath);
                }
            });

            if (result?.Notification is not null && savingKeySwapException is null)
                _notificationService.ShowNotification(result.Notification);

            if (savingKeySwapException is not null)
                _notificationService.ShowNotification("Failed to save key swaps", savingKeySwapException.Message, null);


            Messenger.Send(new ModChangedMessage(this, _loadedMod, null));
            QueueLoadMod(_loadedModId, true);
        }).ConfigureAwait(false);
    }

    private bool CanOpenModFolder() => IsModLoaded && IsNotReadOnly;

    [RelayCommand(CanExecute = nameof(CanOpenModFolder))]
    private async Task OpenModFolderAsync()
    {
        await CommandWrapper(async () =>
        {
            if (!IsModLoaded) return;
            await Windows.System.Launcher.LaunchFolderAsync(
                await StorageFolder.GetFolderFromPathAsync(_loadedMod.Mod.FullPath));
        }).ConfigureAwait(false);
    }

    [RelayCommand]
    private void ToggleEditingModName()
    {
        IsEditingModName = !IsEditingModName;
    }

    #endregion


    private async Task CommandWrapper(Func<Task> command)
    {
        try
        {
            using var _ = await LockAsync().ConfigureAwait(false);
            await command().ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CommandWrapper(Action command)
    {
        try
        {
            using var _ = await LockAsync().ConfigureAwait(false);
            command();
        }
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<IDisposable> LockAsync() =>
        await _loadModLock.LockAsync(cancellationToken: _cancellationToken).ConfigureAwait(false);

    private IRelayCommand[]? _viewModelCommands;

    private void NotifyAllCommands()
    {
        if (_viewModelCommands is null)
        {
            var commands = new List<IRelayCommand>();
            foreach (var propertyInfo in GetType()
                         .GetProperties()
                         .Where(p => p.PropertyType.IsAssignableTo(typeof(IRelayCommand))))
            {
                var value = propertyInfo.GetValue(this);

                if (value is IRelayCommand relayCommand)
                    commands.Add(relayCommand);
            }

            _viewModelCommands = commands.ToArray();
        }

        _viewModelCommands.ForEach(c => c.NotifyCanExecuteChanged());
    }
}

public partial class ModPaneFieldsVm : ObservableObject
{
    public bool IsLoaded { get; private init; }
    public ModPaneFieldsVm? UnchangedValue { get; private init; }

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private Uri _imageUri = ImageHandlerService.StaticPlaceholderImageUri;
    public bool IsImageUriChanged => ImageUri != UnchangedValue?.ImageUri;
    [ObservableProperty] private string _modDisplayName = string.Empty;
    public bool IsModDisplayNameChanged => ModDisplayName != UnchangedValue?.ModDisplayName;
    [ObservableProperty] private string _modUrl = string.Empty;
    public bool IsModUrlChanged => ModUrl != UnchangedValue?.ModUrl;
    [ObservableProperty] private string? _modIniPath = null;
    public bool IsModIniPathChanged => ModIniPath != UnchangedValue?.ModIniPath;
    [ObservableProperty] private bool _ignoreMergedIni = true;
    public bool IsIgnoreMergedIniChanged => IgnoreMergedIni != UnchangedValue?.IgnoreMergedIni;

    public ObservableCollection<ModPaneFieldsKeySwapVm> KeySwaps { get; } = new();
    public bool IsKeySwapsChanged => AnyKeySwapChanges();

    public string IsKeySwapManagementEnabled => (!IgnoreMergedIni).ToString().ToLower();

    private ModPaneFieldsVm(CharacterSkinEntry modEntry, ModSettings modSettings, IEnumerable<KeySwapSection> keySwaps)
    {
        IsEnabled = modEntry.IsEnabled;
        ImageUri = modSettings.ImagePath ?? ImageHandlerService.StaticPlaceholderImageUri;
        ModDisplayName = modEntry.Mod.GetDisplayName();
        ModUrl = modSettings.ModUrl?.ToString() ?? "";
        ModIniPath = modSettings.MergedIniPath?.ToString();
        IgnoreMergedIni = modSettings.IgnoreMergedIni;

        foreach (var keySwap in keySwaps)
        {
            KeySwaps.Add(new ModPaneFieldsKeySwapVm()
            {
                ForwardHotkey = keySwap.ForwardKey,
                BackwardHotkey = keySwap.BackwardKey,
                SectionKey = keySwap.SectionName,
                Type = keySwap.Type,
                VariationsCount = keySwap.Variants?.ToString() ?? "Unknown"
            });
        }

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(AnyChanges))
                OnPropertyChanged(nameof(AnyChanges));
        };
    }

    public ModPaneFieldsVm()
    {
    }

    public static ModPaneFieldsVm FromModEntry(CharacterSkinEntry modEntry, ModSettings modSettings, ICollection<KeySwapSection> keySwaps)
    {
        return new ModPaneFieldsVm(modEntry, modSettings, keySwaps)
        {
            UnchangedValue = new ModPaneFieldsVm(modEntry, modSettings, keySwaps),
            IsLoaded = true
        };
    }

    public bool AnyChanges
    {
        get
        {
            var anyChanges = false;

            if (UnchangedValue is null)
                return false;

            anyChanges |= IsEnabled != UnchangedValue.IsEnabled;
            anyChanges |= ImageUri != UnchangedValue.ImageUri;
            anyChanges |= ModDisplayName != UnchangedValue.ModDisplayName;
            anyChanges |= ModUrl != UnchangedValue.ModUrl;
            anyChanges |= ModIniPath != UnchangedValue.ModIniPath;
            anyChanges |= IgnoreMergedIni != UnchangedValue.IgnoreMergedIni;

            if (KeySwaps.Count != UnchangedValue.KeySwaps.Count)
                return true;

            if (anyChanges)
                return true;

            anyChanges |= AnyKeySwapChanges();

            return anyChanges;
        }
    }

    private bool AnyKeySwapChanges()
    {
        var anyChanges = false;

        if (UnchangedValue is null)
            return false;

        if (KeySwaps.Count != UnchangedValue.KeySwaps.Count)
            return true;

        for (var i = 0; i < KeySwaps.Count; i++)
        {
            var oldKeySwap = UnchangedValue.KeySwaps[i];
            var newKeySwap = KeySwaps[i];

            anyChanges |= oldKeySwap.ForwardHotkey != newKeySwap.ForwardHotkey;
            anyChanges |= oldKeySwap.BackwardHotkey != newKeySwap.BackwardHotkey;
        }

        return anyChanges;
    }
}

public partial class ModPaneFieldsKeySwapVm : ObservableObject
{
    [ObservableProperty] private string _sectionKey = string.Empty;

    [ObservableProperty] private string? _condition;
    [ObservableProperty] private string? _forwardHotkey;
    [ObservableProperty] private string? _backwardHotkey;
    [ObservableProperty] private string? _type;
    [ObservableProperty] private string _variationsCount = "Unknown";
}