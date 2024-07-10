using GIMI_ModManager.Core.CommandService;
using GIMI_ModManager.WinUI.Services.AppManagement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class DebugPage : Page
{
    public CommandService CommandService { get; } = App.GetService<CommandService>();

    public IWindowManagerService WindowManagerService { get; } = App.GetService<IWindowManagerService>();

    public DebugPage()
    {
        InitializeComponent();
    }


    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        var options = new CommandExecutionOptions()
        {
            CreateWindow = true,
            Command = "powershell.exe",
            Arguments =
                "dir {{TargetPath}};$null = read-host"
        };

        using var command = CommandService.CreateCommand("C:\\Users", options);

        command.Start();

        await command.WaitForExitAsync().ConfigureAwait(false);
    }

    private async void ButtonBase_OnClickOpenDialog(object sender, RoutedEventArgs e)
    {
        var window = App.MainWindow;

        var options = new CommandExecutionOptions()
        {
            CreateWindow = false,
            Command = "python",
            WorkingDirectory = "C:\\Users\\reee\\Projects\\JASM\\Testing\\Mods",
            Arguments = "C:\\Users\\reee\\Projects\\JASM\\Testing\\Mods\\genshin_update_mods_45.py"
        };

        var command = CommandService.CreateCommand("C:\\Users", options);

        var page = new CommandProcessViewer(command);


        WindowManagerService.ShowFullScreenDialogAsync(page, XamlRoot, window);
    }
}