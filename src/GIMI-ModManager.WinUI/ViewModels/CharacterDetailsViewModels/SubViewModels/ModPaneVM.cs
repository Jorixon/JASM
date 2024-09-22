using System.Threading.Channels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkitWrapper;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Dispatching;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;

public sealed partial class ModPaneVM(ISkinManagerService skinManagerService, NotificationManager notificationService)
    : ObservableRecipient, IRecipient<ModChangedMessage>
{
    private readonly ISkinManagerService _skinManagerService = skinManagerService;
    private readonly NotificationManager _notificationService = notificationService;

    private readonly AsyncLock _loadModLock = new();
    private CancellationToken _cancellationToken = new();
    private DispatcherQueue _dispatcherQueue = null!;


    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotReadOnly))]
    private bool _isReadOnly = true;

    public bool IsNotReadOnly => !IsReadOnly;

    [ObservableProperty] private Uri _shownModImageUri = ImageHandlerService.StaticPlaceholderImageUri;


    public bool QueueLoadMod(Guid? modId) => _channel.Writer.TryWrite(new LoadModMessage { ModId = modId });


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
        await foreach (var loadModMessage in _channel.Reader.ReadAllAsync().WithCancellation(_cancellationToken))
        {
            using var _ = await LockAsync().ConfigureAwait(false);
            try
            {
                if (loadModMessage.ModId is null)
                {
                    await UnloadModAsync();
                    continue;
                }

                await LoadModAsync(loadModMessage.ModId.Value);
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception e)
            {
                // Log error
                _notificationService.ShowNotification("Error loading mod", e.Message, null);
            }
        }
    }

    private async Task LoadModAsync(Guid modId)
    {
        var mod = _skinManagerService.GetModById(modId);
        if (mod == null)
            return;

        var modSettings =
            await mod.Settings.TryReadSettingsAsync(useCache: false, cancellationToken: _cancellationToken);

        if (modSettings is null)
            return;

        ShownModImageUri = modSettings.ImagePath ?? ImageHandlerService.StaticPlaceholderImageUri;
    }

    private Task UnloadModAsync()
    {
        // Unload mod
        ShownModImageUri = ImageHandlerService.StaticPlaceholderImageUri;
        return Task.CompletedTask;
    }


    private readonly record struct LoadModMessage
    {
        public Guid? ModId { get; init; }
    }

    public void Receive(ModChangedMessage message)
    {
        throw new NotImplementedException();
    }

    public Task OnNavigatedToAsync(DispatcherQueue dispatcherQueue, CancellationToken navigationCt)
    {
        _dispatcherQueue = dispatcherQueue;
        _cancellationToken = navigationCt;
        _dispatcherQueue.EnqueueAsync(ModLoaderLoopAsync);
        return Task.CompletedTask;
    }

    public void OnNavigatedFrom()
    {
        _channel.Writer.TryComplete();
    }

    private async Task<IDisposable> LockAsync() =>
        await _loadModLock.LockAsync(cancellationToken: _cancellationToken).ConfigureAwait(false);
}