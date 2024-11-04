using System.Text.Json;
using Windows.Storage.Pickers;
using GIMI_ModManager.Core.Services.CommandService;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.ModExport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
        var saveFilePicker = new FileSavePicker
        {
            SuggestedFileName = "ModManagerExport.json",
            FileTypeChoices = { { "JSON", [".json"] } },
            SettingsIdentifier = "JSON_MOD_EXPORT"
        };

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(saveFilePicker, hwnd);

        var exportInfo = await JsonExporterService.CreateExportJsonAsync();

        var json = JsonSerializer.Serialize(exportInfo, new JsonSerializerOptions()
        {
            WriteIndented = true
        });

        var file = await saveFilePicker.PickSaveFileAsync();

        if (file is null) return;

        Windows.Storage.CachedFileManager.DeferUpdates(file);

        await Windows.Storage.FileIO.WriteTextAsync(file, json);
    }
}