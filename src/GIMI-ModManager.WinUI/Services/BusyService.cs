using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GIMI_ModManager.WinUI.Services;

public sealed class BusyService
{
    private readonly ConcurrentDictionary<string, bool> _busyIndicators = new();
    public const string MainWindowKey = "MainWindow";


    public EventHandler<BusyChangedEventArgs>? BusyChanged;


    public bool IsPageBusy<T>(T recipient) where T : ObservableRecipient
    {
        var type = recipient.GetType();

        return _busyIndicators.TryGetValue(type.FullName!, out var isBusy) && isBusy;
    }

    public BusyReleaser SetPageBusy<T>(T recipient) where T : ObservableRecipient
    {
        var type = recipient.GetType();

        _busyIndicators.AddOrUpdate(type.FullName!, true, (key, oldValue) => true);
        BusyChanged?.Invoke(this, new BusyChangedEventArgs(type.FullName!, true));

        return new BusyReleaser(() =>
        {
            _busyIndicators.AddOrUpdate(type.FullName!, false, (key, oldValue) => false);
            BusyChanged?.Invoke(this, new BusyChangedEventArgs(type.FullName!, false));
        });
    }


    public bool IsMainWindowBusy()
    {
        return _busyIndicators.TryGetValue(MainWindowKey, out var isBusy) && isBusy;
    }

    public BusyReleaser SetMainWindowBusy()
    {
        _busyIndicators.AddOrUpdate(MainWindowKey, true, (key, oldValue) => true);
        BusyChanged?.Invoke(this, new BusyChangedEventArgs(MainWindowKey, true));

        return new BusyReleaser(() =>
        {
            _busyIndicators.AddOrUpdate(MainWindowKey, false, (key, oldValue) => false);
            BusyChanged?.Invoke(this, new BusyChangedEventArgs(MainWindowKey, false));
        });
    }
}

public class BusyChangedEventArgs : EventArgs
{
    public string Key { get; }
    public bool IsBusy { get; }

    public BusyChangedEventArgs(string key, bool isBusy)
    {
        Key = key;
        IsBusy = isBusy;
    }
}

public readonly struct BusyReleaser(Action releaseAction) : IDisposable
{
    public void Dispose()
    {
        releaseAction();
    }
}