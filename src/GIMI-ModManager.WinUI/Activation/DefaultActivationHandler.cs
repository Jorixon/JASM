using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;

namespace GIMI_ModManager.WinUI.Activation;

public class DefaultActivationHandler : ActivationHandler<LaunchActivatedEventArgs>
{
    private readonly INavigationService _navigationService;
    public override string ActivationName { get; } = nameof(DefaultActivationHandler);

    public DefaultActivationHandler(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    protected override bool CanHandleInternal(LaunchActivatedEventArgs args)
    {
        // None of the ActivationHandlers has handled the activation.
        return _navigationService.Frame?.Content == null;
    }

    protected override async Task HandleInternalAsync(LaunchActivatedEventArgs args)
    {
        _navigationService.NavigateTo(typeof(StartupViewModel).FullName!, args.Arguments, true);

        await Task.CompletedTask;
    }
}