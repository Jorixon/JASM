using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class DebugPage : Page
{
    public DebugViewModel ViewModel { get; } = App.GetService<DebugViewModel>();

    public NotificationManager NotificationManager { get; } = App.GetService<NotificationManager>();

    public DebugPage()
    {
        InitializeComponent();
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        NotificationManager.QueueNotification("This should show NOW", showNow: true);
    }

    private void ButtonBase_OnClick1(object sender, RoutedEventArgs e)
    {
        NotificationManager.QueueNotification("This should show later");
    }
}