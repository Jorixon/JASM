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

    public ModUpdateAvailableWindow(Guid modId)
    {
        ViewModel = new ModUpdateVM(modId, this);
        InitializeComponent();

        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = ThemeSelectorService.Theme;
        }

        ModPageBrowser.Loaded += async (_, _) =>
        {
            await ModPageBrowser.EnsureCoreWebView2Async();
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
}