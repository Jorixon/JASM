using GIMI_ModManager.Core.Services.CommandService;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Views.Settings;
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
        var execOptions = new CommandExecutionOptions()
        {
            UseShellExecute = true,
            RunAsAdmin = true,
            Command = "E:\\Genshin Impact\\Genshin Impact game\\GenshinImpact.exe"
        };


        var command = CommandService.CreateCommand(new CommandDefinition()
        {
            CommandDisplayName = "test",
            KillOnMainAppExit = false,
            ExecutionOptions = execOptions
        });

        command.Start();

        await command.WaitForExitAsync().ConfigureAwait(false);
    }

    private async void ButtonBase_OnClickOpenDialog(object sender, RoutedEventArgs e)
    {
        var window = App.MainWindow;

        var execOptions = new CommandExecutionOptions()
        {
            CreateWindow = true,
            Command = "python",
            Arguments = "-u F:\\test.py"
        };

        var command = CommandService.CreateCommand(new CommandDefinition()
        {
            CommandDisplayName = "test",
            KillOnMainAppExit = true,
            ExecutionOptions = execOptions
        });


        command.Start();
        await command.WaitForExitAsync().ConfigureAwait(false);


        //var page = new CommandProcessViewer(command);


        //WindowManagerService.ShowFullScreenDialogAsync(page, XamlRoot, window);
    }

    private void CreateCommand_OnClick(object sender, RoutedEventArgs e)
    {
        var window = App.MainWindow;

        var page = new CreateCommandView();

        WindowManagerService.ShowFullScreenDialogAsync(page, XamlRoot, window);
    }
}