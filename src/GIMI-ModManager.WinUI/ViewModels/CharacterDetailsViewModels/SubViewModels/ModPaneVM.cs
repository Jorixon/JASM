using System.Threading.Channels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;

public sealed partial class ModPaneVM : ObservableRecipient, IRecipient<ModChangedMessage>
{
    private readonly AsyncLock _loadModLock = new();


    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotReadOnly))]
    private bool _isReadOnly = true;

    public bool IsNotReadOnly => !IsReadOnly;

    [ObservableProperty] private Uri _shownModImageUri = ImageHandlerService.StaticPlaceholderImageUri;

    private readonly Channel<LoadModMessage> _channel = Channel.CreateBounded<LoadModMessage>(
        new BoundedChannelOptions(1)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });


    private readonly record struct LoadModMessage
    {
        public Guid? ModId { get; init; }
    }

    public void Receive(ModChangedMessage message)
    {
        throw new NotImplementedException();
    }

    public Task OnNavigatedToAsync()
    {
        throw new NotImplementedException();
    }

    public void OnNavigatedFrom()
    {
        _channel.Writer.TryComplete();
    }
}