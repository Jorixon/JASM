// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class ModUpdateAvailableWindow : WindowEx
{
    public readonly ModUpdateVM ViewModel;
    public readonly IThemeSelectorService ThemeSelectorService = App.GetService<IThemeSelectorService>();

    public ModUpdateAvailableWindow(Guid notificationId)
    {
        ViewModel = new ModUpdateVM(notificationId, this);
        InitializeComponent();

        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = ThemeSelectorService.Theme;
        }

        ModPageBrowser.Loading += async (_, _) =>
        {
            await ModPageBrowser.EnsureCoreWebView2Async();
            ModPageBrowser.CoreWebView2.NavigationCompleted += (_, _) => ModPageLoadingRing.IsActive = false;
            ModPageBrowser.CoreWebView2.NavigationStarting += (_, _) => ModPageLoadingRing.IsActive = true;
            var theme = ThemeSelectorService.Theme;
            var webTheme = CoreWebView2PreferredColorScheme.Auto;
            switch (theme)
            {
                case ElementTheme.Light:
                    webTheme = CoreWebView2PreferredColorScheme.Light;
                    break;
                case ElementTheme.Dark:
                    webTheme = CoreWebView2PreferredColorScheme.Dark;
                    break;
                case ElementTheme.Default:
                    break;
            }

            ModPageBrowser.CoreWebView2.Profile.PreferredColorScheme = webTheme;
        };
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        ModPageBrowser.CoreWebView2.OpenDefaultDownloadDialog();
    }
}