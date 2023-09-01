using Windows.ApplicationModel.DataTransfer;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.WinUI.Services;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class NotificationsViewModel : ObservableRecipient
{
    public readonly NotificationManager NotificationManager;

    [ObservableProperty] private string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "log.txt");

    public NotificationsViewModel(NotificationManager notificationManager)
    {
        NotificationManager = notificationManager;
    }

    [RelayCommand]
    private void CopyLogFilePath()
    {
        var package = new DataPackage();
        package.SetText(LogFilePath);
        Clipboard.SetContent(package);
    }
}