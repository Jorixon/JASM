using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;

namespace GIMI_ModManager.WinUI.Activation;

/// <summary>
/// FirstTimeStartupActivationHandler is completely wrong name for this class. This is the default startup handler.
/// </summary>
public class FirstTimeStartupActivationHandler : ActivationHandler<LaunchActivatedEventArgs>
{
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ISkinManagerService _skinManagerService;
    public override string ActivationName { get; } = "RegularStartup";

    public FirstTimeStartupActivationHandler(INavigationService navigationService,
        ILocalSettingsService localSettingsService,
        ISkinManagerService skinManagerService)
    {
        _navigationService = navigationService;
        _localSettingsService = localSettingsService;
        _skinManagerService = skinManagerService;
    }

    protected override bool CanHandleInternal(LaunchActivatedEventArgs args)
    {
        var options = Task
            .Run(async () => await _localSettingsService.ReadSettingAsync<ModManagerOptions>(ModManagerOptions.Section))
            .GetAwaiter().GetResult();

        return !string.IsNullOrEmpty(options?.GimiRootFolderPath) &&
               !string.IsNullOrEmpty(options?.ModsFolderPath);
    }

    protected override async Task HandleInternalAsync(LaunchActivatedEventArgs args)
    {
        var modManagerOptions =
            await _localSettingsService.ReadSettingAsync<ModManagerOptions>(ModManagerOptions.Section);

        await _skinManagerService.Initialize(modManagerOptions!.ModsFolderPath!, null, modManagerOptions.GimiRootFolderPath);
        _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!, args.Arguments, true);
    }
}