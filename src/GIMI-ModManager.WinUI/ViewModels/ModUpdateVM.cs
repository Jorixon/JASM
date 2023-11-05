using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Services.ModHandling;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class ModUpdateVM : ObservableRecipient
{
    private readonly GameBananaCache _gameBananaCache = App.GetService<GameBananaCache>();
    private readonly ISkinManagerService _skinManagerService = App.GetService<ISkinManagerService>();

    private readonly Guid _modId;

    [ObservableProperty] private string _modName = string.Empty;

    [ObservableProperty] private Uri _modPage = new("https://gamebanana.com/");

    [ObservableProperty] private Uri? _modPath = null;


    public ObservableCollection<UpdateCheckResult> Results = new();

    public ModUpdateVM(Guid modId)
    {
        _modId = modId;
        Initialize();
    }


    private async void Initialize()
    {
        var modResult = await _gameBananaCache.GetAvailableMods(_modId);
        if (modResult == null)
        {
            throw new NotImplementedException();
        }

        var mod = _skinManagerService.GetModById(_modId);
        if (mod is null)
        {
            throw new NotImplementedException();
        }

        var modSettings = mod.Settings.GetSettings().TryPickT0(out var settings, out _) ? settings : null;

        if (modSettings is null)
        {
            throw new NotImplementedException();
        }

        ModName = modSettings.CustomName ?? mod.Name;
        ModPage = modResult.SitePageUrl;
        ModPath = new Uri(mod.FullPath);

        modResult.Mods.ForEach(x => Results.Add(x));
    }
}