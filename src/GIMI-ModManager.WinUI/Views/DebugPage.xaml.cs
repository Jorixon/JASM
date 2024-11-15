using GIMI_ModManager.Core.Services.CommandService;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.ModExport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

//using NativeFileDialogs.Net;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class DebugPage : Page
{
    public CommandService CommandService { get; } = App.GetService<CommandService>();

    public IWindowManagerService WindowManagerService { get; } = App.GetService<IWindowManagerService>();

    public JsonExporterService JsonExporterService { get; } = App.GetService<JsonExporterService>();

    public DebugPage()
    {
        InitializeComponent();
    }


    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        //NfdStatus result = Nfd.OpenDialog(out string? outPath, new Dictionary<string, string>()
        //{
        //    { "python", "py" }
        //}, defaultPath: "F:\\");

        //if (result == NfdStatus.Ok && outPath is { } path)
        //{
        //    Console.WriteLine("Success!");
        //    Console.WriteLine(path);
        //}
        //else
        //{
        //    Console.WriteLine("User pressed Cancel.");
        //}
    }
}