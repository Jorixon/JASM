using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Models;

public class Notification : ObservableObject
{
    public Notification(string title, TimeSpan duration, string? subtitle = null, UIElement? content = null,
        FontIcon? icon = null, bool showNow = false,
        string? logMessage = null, NotificationType type = NotificationType.Information)
    {
        Title = title;
        Subtitle = subtitle;
        Content = content;
        Duration = duration;
        Icon = icon;
        ShowNow = showNow;
        LogMessage = logMessage;
        Type = type;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public DateTime QueueTime { get; } = DateTime.Now;

    public DateTime? ShowTime { get; set; }

    public string Title { get; }
    public string? Subtitle { get; }
    public UIElement? Content { get; }
    public TimeSpan Duration { get; }
    public FontIcon? Icon { get; }
    public bool ShowNow { get; }
    public string? LogMessage { get; set; }

    public NotificationType Type { get; }

    public bool IsLogged { get; set; }


    public override string ToString()
    {
        return LogMessage ?? $"Title: {Title} | Subtitle: {Subtitle} | Duration: {Duration} | " +
            $"Icon: {Icon} | ShowNow: {ShowNow} | ShowTime: {ShowTime}";
    }
}