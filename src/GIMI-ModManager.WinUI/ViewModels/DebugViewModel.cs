using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class DebugViewModel : ObservableRecipient, INavigationAware
{
    private readonly ILogger _logger;
    private readonly NotificationManager _notificationManager;
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGenshinService _genshinService;


    public ModListVM ModListVM { get; }

    public DebugViewModel(ILogger logger, NotificationManager notificationManager,
        ISkinManagerService skinManagerService, IGenshinService genshinService)
    {
        _logger = logger;
        _notificationManager = notificationManager;
        _skinManagerService = skinManagerService;
        _genshinService = genshinService;
        ModListVM = new(skinManagerService);
    }

    public void OnNavigatedTo(object parameter)
    {
        var modList =
            _skinManagerService.GetCharacterModList(_genshinService.GetCharacter("Raiden")!);

        ModListVM.SetBackendMods(modList.Mods.Select(NewModModel.FromMod));
        ModListVM.ResetContent();
    }

    public void OnNavigatedFrom()
    {
    }
}