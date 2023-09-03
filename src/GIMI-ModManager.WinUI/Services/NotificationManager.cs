using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.WinUI.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Serilog;

namespace GIMI_ModManager.WinUI.Services;

public partial class NotificationManager : ObservableObject
{
    private readonly ILogger? _logger;
    public ReadOnlyObservableCollection<Notification> Notifications => new(_notifications);

    public ReadOnlyObservableCollection<Notification> NotificationsReverse =>
        new(new ObservableCollection<Notification>(_notifications.Reverse()));

    [ObservableProperty] [NotifyPropertyChangedForAttribute(nameof(IsNotificationActive))]
    private Notification? _activeNotification = null;

    public bool IsNotificationActive => ActiveNotification != null;
    private readonly ObservableCollection<Notification> _notifications = new();

    public NotificationManager(ILogger? logger = null)
    {
        _logger = logger?.ForContext<NotificationManager>();
    }

    public void ShowNotification(string title, string message, TimeSpan? duration)
    {
        var dispatcherQueue = App.MainWindow.DispatcherQueue;
        var notification = new Notification(title, message);
        dispatcherQueue.TryEnqueue(() =>
        {
            _notifications.Add(notification);
            ActiveNotification = notification;

        });
        _logger?.Information("Title: {Title} | Body: {Message}", title, message);

        if (duration is not null)
        {
            Task.Run(async () =>
            {
                await Task.Delay(duration.Value);
                if (ActiveNotification != null && ActiveNotification.Equals(notification))
                {
                    dispatcherQueue.TryEnqueue(() => ActiveNotification = null);
                }
            });
        }
    }
}

/*public interface INotificationManager
{
    public bool IsNotificationActive { get; }
    public ReadOnlyObservableCollection<Notification> Notifications { get; }
    public Notification ActiveNotification { get; }

    public void ShowNotification(string title, string message, TimeSpan? duration);
}*/