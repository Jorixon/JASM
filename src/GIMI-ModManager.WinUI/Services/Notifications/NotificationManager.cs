using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.WinUI.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.Notifications;

public partial class NotificationManager : ObservableObject
{
    private readonly ILogger _logger;

    private CancellationTokenSource _cts = new();

    public ReadOnlyObservableCollection<Notification> Notifications => new(_notifications);

    public ReadOnlyObservableCollection<Notification> NotificationsReverse =>
        new(new ObservableCollection<Notification>(_notifications.Reverse()));

    [ObservableProperty] private Notification? _activeNotification = null;

    [ObservableProperty] private bool _isNotificationActive;
    private readonly ObservableCollection<Notification> _notifications = new();

    public NotificationManager(ILogger logger)
    {
        _logger = logger.ForContext<NotificationManager>();
    }

    private readonly BlockingCollection<Notification>
        _newNotifications = new(new ConcurrentQueue<Notification>());

    private bool _showNowFlag;

    private bool _isInitialized;

    public void Initialize()
    {
        if (_isInitialized)
            throw new InvalidOperationException("Already initialized");
        _isInitialized = true;

        _logger?.Debug("Initialized");

        Task.Factory.StartNew(async () =>
            {
                try
                {
                    await NotificationConsumerAsync().ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                }
                catch (OperationCanceledException)
                {
                }
            }, _cts.Token, TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private void GetAndSortNotifications(List<Notification> notifications)
    {
        notifications.Add(_newNotifications.Take(_cts.Token));


        // take all notifications from the queue
        while (_newNotifications.TryTake(out var notification))
        {
            notifications.Add(notification);
        }

        _showNowFlag = false;

        var priorityNotifications = notifications.Where((n) => n.ShowNow).OrderBy(n => n.QueueTime).ToArray();
        var normalNotifications = notifications.Where((n) => !n.ShowNow).OrderBy(n => n.QueueTime).ToArray();

        notifications.Clear();
        notifications.AddRange(priorityNotifications);
        notifications.AddRange(normalNotifications);

        foreach (var notification in notifications.Where(n => !n.IsLogged).ToArray())
        {
            _logger.Information("Title: {Title} | Body: {Message}", notification.Title,
                notification.Subtitle ?? notification.LogMessage);
            notification.IsLogged = true;
        }
    }

    private void RemoveNotification(List<Notification> notifications, Notification notification)
    {
        var index = notifications.FindIndex((n) => n.Id == notification.Id);
        notifications.RemoveAt(index);
    }

    private async Task NotificationConsumerAsync()
    {
        var notifications = new List<Notification>();
        while (!_cts.IsCancellationRequested)
        {
            // take all notifications from the queue
            GetAndSortNotifications(notifications);

            while (notifications.Any())
            {
                if (_showNowFlag)
                    break;

                var notification = notifications.FirstOrDefault();
                if (notification is null)
                    break;

                var duration = notification.Duration;

                var delayTask = Task.Delay(duration, _cts.Token);
                await SetCurrentShownNotification(notification).ConfigureAwait(false);
                RemoveNotification(notifications, notification);


                while (!delayTask.IsCompleted)
                {
                    await Task.Delay(100, _cts.Token).ConfigureAwait(false);
                    if (_showNowFlag)
                        break;

                    if (!IsNotificationActive)
                        break;
                }

                await SetCurrentShownNotification(null).ConfigureAwait(false);
                await Task.Delay(700, _cts.Token).ConfigureAwait(false);
            }
        }
    }


    private Task SetCurrentShownNotification(Notification? notification)
    {
        var dispatcherQueue = App.MainWindow.DispatcherQueue;
        dispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
        {
            //if (ActiveNotification is not null)
            //    ActiveNotification.OnClosing();

            // Remove the current notification
            IsNotificationActive = false;
            ActiveNotification = null;
            if (notification is null)
                return;

            _notifications.Add(notification);
            notification.ShowTime = DateTime.Now;
            ActiveNotification = notification;
            IsNotificationActive = true;
        });

        return Task.CompletedTask;
    }


    public void ShowNotification(string title, string message, TimeSpan? duration)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);
        QueueNotification(title, message, duration: duration);
    }


    public void QueueNotification(string title, string? subtitle = null, UIElement? content = null,
        TimeSpan? duration = null, FontIcon? icon = null, bool showNow = false,
        Action? onClosing = null, string? logMessage = null)
    {
        ArgumentNullException.ThrowIfNull(title);

        duration ??= TimeSpan.FromSeconds(4);

        var newNotification = new Notification(title, duration.Value, subtitle, content, icon, showNow,
            logMessage);


        _newNotifications.Add(newNotification);

        if (showNow)
            _showNowFlag = true;
    }


    public void CancelAndStop()
    {
        if (_cts is null || _cts.IsCancellationRequested)
            return;
        var cts = _cts;
        _cts = null!;
        cts.Cancel();
        cts.Dispose();
        _logger.Debug($"{nameof(NotificationManager)} stopped");
    }
}

public enum NotificationType
{
    Information,
    Error
}

/*public interface INotificationManager
{
    public bool IsNotificationActive { get; }
    public ReadOnlyObservableCollection<Notification> Notifications { get; }
    public Notification ActiveNotification { get; }

    public void ShowNotification(string title, string message, TimeSpan? duration);
}*/