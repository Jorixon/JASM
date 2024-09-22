using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.GamesService;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;
using Microsoft.UI.Dispatching;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;

public partial class CharacterDetailsViewModel : ObservableObject, INavigationAware
{
    private readonly INavigationService _navigationService = App.GetService<INavigationService>();
    private readonly IGameService _gameService = App.GetService<IGameService>();
    private readonly NotificationManager _notificationService = App.GetService<NotificationManager>();

    public Func<Task>? GridLoadedAwaiter { get; set; }

    private readonly CancellationTokenSource _navigationCancellationTokenSource = new();
    public CancellationToken CancellationToken;

    private bool IsReturning => CancellationToken.IsCancellationRequested || _isErrorNavigateBack;
    private bool _isErrorNavigateBack;
    [ObservableProperty] private bool _isNavigationFinished;


    public ModGridVM ModGridVM { get; private set; } = App.GetService<ModGridVM>();
    public ModPaneVM ModPaneVM { get; private set; } = App.GetService<ModPaneVM>();

    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            await InitAsync(parameter).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            ErrorNavigateBack(e);
            return;
        }
    }

    private async Task InitAsync(object parameter)
    {
        CancellationToken = _navigationCancellationTokenSource.Token;
        if (IsReturning)
            return;
        OnInitializingStarted?.Invoke(this, EventArgs.Empty);

        // Init character card
        InitCharacterCard(parameter);

        // Refresh UI
        await Task.Delay(100, CancellationToken);
        if (IsReturning)
            return;


        // Load mods
        await InitModGridAsync();
        if (IsReturning)
            return;

        // Init Mod Pane
        await InitModPaneAsync();
        if (IsReturning)
            return;

        await SetInitialSelectedModAsync();

        // Finished initializing
        IsNavigationFinished = true;
        OnInitializingFinished?.Invoke(this, EventArgs.Empty);
    }

    private async Task SetInitialSelectedModAsync()
    {
        if (GridLoadedAwaiter is not null)
            await GridLoadedAwaiter();
        GridLoadedAwaiter = null;

        var modToSelect = ModGridVM.GridMods.FirstOrDefault(m => m.IsEnabled) ?? ModGridVM.GridMods.FirstOrDefault();

        if (modToSelect is null)
            return;

        ModGridVM.SetSelectedMod(modToSelect.Id);
    }


    private void InitCharacterCard(object parameter)
    {
        var internalName = ParseNavigationArg(parameter);

        var moddableObject = _gameService.GetModdableObjectByIdentifier(internalName);

        if (moddableObject == null)
        {
            ErrorNavigateBack();
            return;
        }

        ShownModObject = moddableObject;
        ShownModImageUri = moddableObject.ImageUri ?? ImageHandlerService.StaticPlaceholderImageUri;


        if (ShownModObject is ICharacter character)
        {
            Character = character;
            SelectedSkin = character.Skins.First();
            ShownModImageUri = SelectedSkin.ImageUri ?? ImageHandlerService.StaticPlaceholderImageUri;
        }

        IsModObjectLoaded = true;
        OnModObjectLoaded?.Invoke(this, EventArgs.Empty);
    }

    private async Task InitModGridAsync()
    {
        await ModGridVM.InitializeAsync(new ModDetailsPageContext(ShownModObject, SelectedSkin), CancellationToken);
        ModGridVM.OnModsSelected += OnModsSelected;
        if (IsReturning)
            return;


        ModGridVM.IsBusy = false;
        OnModsLoaded?.Invoke(this, EventArgs.Empty);
    }

    private void OnModsSelected(object? sender, ModGridVM.ModRowSelectedEventArgs args)
    {
        var selectedMod = args.Mods.FirstOrDefault();

        ModPaneVM.QueueLoadMod(selectedMod?.Id);

        if (selectedMod is null)
            return;

        // TODO: Handle notification
    }

    private async Task InitModPaneAsync()
    {
        // Init Mod Pane
        await ModPaneVM.OnNavigatedToAsync(DispatcherQueue.GetForCurrentThread(), CancellationToken);
        if (IsReturning)
            return;
    }

    public void OnNavigatedFrom()
    {
        ModPaneVM.OnNavigatedFrom();
        _navigationCancellationTokenSource.Cancel();
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            try
            {
                _navigationCancellationTokenSource?.Dispose();
            }
            catch (Exception e)
            {
                // ignored
            }
        });
    }


    private InternalName? ParseNavigationArg(object parameter)
    {
        return parameter switch
        {
            CharacterGridItemModel characterGridItemModel => characterGridItemModel.Character.InternalName,
            INameable iInternalName => iInternalName.InternalName,
            string internalNameString => new InternalName(internalNameString),
            InternalName internalName1 => internalName1,
            _ => null
        };
    }

    private void ErrorNavigateBack(Exception? exception = null)
    {
        if (_isErrorNavigateBack)
            return;
        _isErrorNavigateBack = true;

        Task.Run(async () =>
        {
            await Task.Delay(500);
            if (exception is not null)
                _notificationService.ShowNotification("An error occurred while loading the character details page.",
                    exception.Message, null);
            else
                _notificationService.ShowNotification("An error occurred while loading the character details page.", "",
                    null);
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (_navigationService.CanGoBack)
                    _navigationService.GoBack();
                else
                    _navigationService.NavigateTo(typeof(CharactersViewModel).FullName!);
            });
        });
    }

    private void NotifyCommandsChanged()
    {
    }
}