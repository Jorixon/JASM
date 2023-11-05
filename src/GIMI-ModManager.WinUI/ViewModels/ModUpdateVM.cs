using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Services.ModHandling;
using GIMI_ModManager.WinUI.Services.Notifications;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class ModUpdateVM : ObservableRecipient
{
    private readonly GameBananaCache _gameBananaCache = App.GetService<GameBananaCache>();
    private readonly ISkinManagerService _skinManagerService = App.GetService<ISkinManagerService>();
    private readonly ModNotificationManager _modNotificationManager = App.GetService<ModNotificationManager>();

    private readonly Guid _modId;
    private readonly WindowEx _window;

    [ObservableProperty] private string _modName = string.Empty;

    [ObservableProperty] private Uri _modPage = new("https://gamebanana.com/");

    [ObservableProperty] private Uri? _modPath = null;

    [ObservableProperty] private DateTime _lastUpdateCheck = DateTime.Now;


    public ObservableCollection<UpdateCheckResult> Results = new();

    public ModUpdateVM(Guid modId, WindowEx window)
    {
        _modId = modId;
        _window = window;
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
        LastUpdateCheck = modResult.LastCheck;

        modResult.Mods.ForEach(x => Results.Add(x));
    }

    [RelayCommand]
    private async Task IgnoreAndCloseAsync()
    {
        var notification = await _modNotificationManager.GetNotificationById(_modId);
        if (notification is null)
        {
            return;
        }

        await _modNotificationManager.RemoveModNotificationAsync(notification.Id);
        _window.Close();
    }
}