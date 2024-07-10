using GIMI_ModManager.Core.CommandService;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class DebugPage : Page
{
    public CommandService CommandService { get; } = App.GetService<CommandService>();

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
}