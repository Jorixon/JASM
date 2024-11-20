using Windows.Storage;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.WinUI.Services.Notifications;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class NotificationsViewModel : ObservableRecipient
{
    public readonly NotificationManager NotificationManager;

    [ObservableProperty]
    private string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "log.txt");

    public NotificationsViewModel(NotificationManager notificationManager)
    {
        NotificationManager = notificationManager;
    }

    [RelayCommand]
    private async Task CopyLogFilePathAsync()
    {
        if (!File.Exists(LogFilePath))
        {
            NotificationManager.ShowNotification("Log file not found", "", null);
            return;
        }

        var openResult = await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(LogFilePath));
        if (!openResult)
            NotificationManager.ShowNotification("Log file could not be opened", "", null);
    }
}