using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models.CustomControlTemplates;
using GIMI_ModManager.WinUI.Models.ViewModels;
using GIMI_ModManager.WinUI.Services;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class DebugViewModel : ObservableRecipient, INavigationAware
{
    private readonly ILogger _logger;
    private readonly NotificationManager _notificationManager;
    private readonly ISkinManagerService _skinManagerService;
    private readonly IGenshinService _genshinService;
    private readonly ModCrawlerService _modCrawlerService;


    public DebugViewModel(ILogger logger, NotificationManager notificationManager,
        ISkinManagerService skinManagerService, IGenshinService genshinService, ModCrawlerService modCrawlerService)
    {
        _logger = logger;
        _notificationManager = notificationManager;
        _skinManagerService = skinManagerService;
        _genshinService = genshinService;
        _modCrawlerService = modCrawlerService;
    }

    [ObservableProperty] private string _path = string.Empty;

    public ObservableCollection<SkinVM> InGameSkins = new();

    [RelayCommand]
    private Task TestCrawlerAsync()
    {
        foreach (var subSkin in _modCrawlerService.GetSubSkinsRecursive(Path))
            InGameSkins.Add(SkinVM.FromSkin(subSkin));

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ItemClicked(object item)
    {
        await Task.Delay(2000);
        Log.Information("Item clicked: {item}", item);
    }


    public ObservableCollection<SelectCharacterTemplate> Items { get; } = new();

    public void OnNavigatedTo(object parameter)
    {
        var characters = _genshinService.GetCharacters().Take(5);
        foreach (var character in characters)
        {
            var vm = new SelectCharacterTemplate
            {
                DisplayName = character.DisplayName,
                ImagePath = character.ImageUri ?? " ",
                IsSelected = false
            };
            Items.Add(vm);
        }
    }

    public void OnNavigatedFrom()
    {
    }
}