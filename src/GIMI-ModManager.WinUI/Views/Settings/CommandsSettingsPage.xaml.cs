using GIMI_ModManager.WinUI.ViewModels.SettingsViewModels;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views.Settings;

public sealed partial class CommandsSettingsPage : Page
{
    public CommandsSettingsViewModel ViewModel { get; } = App.GetService<CommandsSettingsViewModel>();

    public CommandsSettingsPage()
    {
        this.InitializeComponent();
    }
}