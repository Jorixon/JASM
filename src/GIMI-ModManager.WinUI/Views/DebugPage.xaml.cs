using GIMI_ModManager.Core.Services.GameBanana;
using GIMI_ModManager.Core.Services.GameBanana.Models;
using GIMI_ModManager.WinUI.Contracts.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class DebugPage : Page
{
    private readonly IApiGameBananaClient _apiGameBananaClient = App.GetService<IApiGameBananaClient>();
    private readonly ModArchiveRepository _modArchiveRepository = App.GetService<ModArchiveRepository>();
    private readonly ILocalSettingsService _localSettingsService = App.GetService<ILocalSettingsService>();
    private readonly GameBananaCoreService _gameBananaCoreService = App.GetService<GameBananaCoreService>();

    public string ModId { get; set; } = "495878";

    public DebugPage()
    {
        InitializeComponent();
        _ = Task.Run(() => _modArchiveRepository.InitializeAsync(_localSettingsService.ApplicationDataFolder));
    }

    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        var cts = new CancellationTokenSource();

        var modId = new GbModId(ModId);

        var modInfos = await _apiGameBananaClient.GetModFilesInfoAsync(modId, cts.Token);

        var mod = modInfos!.Files.First();
        var modFileIdentifier = new GbModFileIdentifier(modId, new GbModFileId(mod.FileId));

        var path = await Task.Run(() => _gameBananaCoreService.DownloadModAsync(modFileIdentifier, ct: cts.Token),
            cts.Token);
    }
}