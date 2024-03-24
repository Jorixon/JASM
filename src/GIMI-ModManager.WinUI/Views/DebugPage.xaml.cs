using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class DebugPage : Page
{
    public DebugViewModel ViewModel { get; } = App.GetService<DebugViewModel>();

    public UserPreferencesService UserPreferencesService { get; } = App.GetService<UserPreferencesService>();

    public DebugPage()
    {
        InitializeComponent();
    }

    private async void ButtonBase_OnClickSave(object sender, RoutedEventArgs e)
    {
        await UserPreferencesService.SaveModPreferencesAsync().ConfigureAwait(false);
    }

    private async void ButtonBase_OnClickApply(object sender, RoutedEventArgs e)
    {
        await UserPreferencesService.SetModPreferencesAsync().ConfigureAwait(false);
    }
}