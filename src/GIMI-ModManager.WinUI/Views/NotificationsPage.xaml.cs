using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;


namespace GIMI_ModManager.WinUI.Views;

public sealed partial class NotificationsPage : Page
{
    public NotificationsViewModel ViewModel { get; }

    public NotificationsPage()
    {
        ViewModel = App.GetService<NotificationsViewModel>();
        InitializeComponent();
    }
}