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

    //private bool _isFirstTimeNavigation = true;

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
            ModPageBrowser.CoreWebView2.NavigationCompleted += async (_, _) =>
            {
                ModPageLoadingRing.IsActive = false;

                // This automatically opens the author side pane
                // However, it covered the update date and was annoying so I disabled it
                // TODO: Change CSS to make it not cover the update date
                //if (!_isFirstTimeNavigation) return;

                //_isFirstTimeNavigation = false;
                //await Task.Delay(1000);
                //var script = "document.getElementById('HiddenColumnToggleButton').click();";
                //await ModPageBrowser.CoreWebView2.ExecuteScriptAsync(script);
            };
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

    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        await ModPageBrowser.EnsureCoreWebView2Async();

        if (ModPageBrowser.CoreWebView2.IsDefaultDownloadDialogOpen)
            ModPageBrowser.CoreWebView2.CloseDefaultDownloadDialog();
        else
            ModPageBrowser.CoreWebView2.OpenDefaultDownloadDialog();
    }
}