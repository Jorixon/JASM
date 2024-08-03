using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Services.CommandService;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.Views.Settings;

namespace GIMI_ModManager.WinUI.ViewModels.SettingsViewModels;

public sealed partial class CommandsSettingsViewModel(
    CommandService commandService,
    IWindowManagerService windowManagerService,
    NotificationManager notificationManager)
    : ObservableRecipient, INavigationAware
{
    private readonly CommandService _commandService = commandService;
    private readonly IWindowManagerService _windowManagerService = windowManagerService;
    private readonly NotificationManager _notificationManager = notificationManager;

    public ObservableCollection<CommandDefinitionVM> CommandDefinitions { get; } = new();

    public ObservableCollection<CommandVM> RunningCommands { get; } = new();


    [RelayCommand]
    private async Task OpenCreateCommandAsync()
    {
        var window = App.MainWindow;

        var page = new CreateCommandView();

        await _windowManagerService.ShowFullScreenDialogAsync(page, window.Content.XamlRoot, window);
        await RefreshCommandDefinitionsAsync().ConfigureAwait(false);
    }


    [RelayCommand]
    private async Task KillRunningCommandAsync(CommandVM? command)
    {
        if (command is null || command.IsKilling)
            return;
        command.IsKilling = true;

        var runningCommands = await _commandService.GetRunningCommandsAsync();
        var runningCommand = runningCommands.FirstOrDefault(x => x.RunId == command.RunId);
        if (runningCommand is { IsRunning: true })
        {
            try
            {
                await runningCommand.KillAsync();
            }
            catch (Exception e)
            {
                _notificationManager.ShowNotification("Failed to kill process", e.Message, TimeSpan.FromSeconds(5));
                return;
            }
        }
        else
        {
            _notificationManager.ShowNotification("Process is not running", string.Empty, TimeSpan.FromSeconds(2));
            await RefreshRunningCommandsAsync();
            return;
        }

        _notificationManager.ShowNotification("Process killed successfully", string.Empty, TimeSpan.FromSeconds(2));
    }

    [RelayCommand]
    private async Task DeleteCommandAsync(CommandDefinitionVM? commandDefinition)
    {
        if (commandDefinition is null || commandDefinition.IsDeleting)
            return;
        commandDefinition.IsDeleting = true;

        try
        {
            await _commandService.DeleteCommandDefinitionAsync(commandDefinition.Id);
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification("Failed to delete command", e.Message, TimeSpan.FromSeconds(5));
            return;
        }
        finally
        {
            await RefreshCommandDefinitionsAsync();
        }

        _notificationManager.ShowNotification("Command deleted successfully", string.Empty, TimeSpan.FromSeconds(2));
    }

    public async void OnNavigatedTo(object parameter)
    {
        await RefreshRunningCommandsAsync();
        _commandService.RunningCommandsChanged += RunningCommandsChangedHandler;
        await RefreshCommandDefinitionsAsync().ConfigureAwait(false);
    }

    public void OnNavigatedFrom()
    {
        _commandService.RunningCommandsChanged -= RunningCommandsChangedHandler;
    }

    private async Task RefreshCommandDefinitionsAsync()
    {
        CommandDefinitions.Clear();
        var commandDefinitions = await _commandService.GetCommandDefinitionsAsync();

        foreach (var commandDefinition in commandDefinitions)
        {
            var commandDefinitionVM = new CommandDefinitionVM(commandDefinition)
            {
                DeleteCommand = DeleteCommandCommand
            };
            CommandDefinitions.Add(commandDefinitionVM);
        }
    }

    private async Task RefreshRunningCommandsAsync()
    {
        RunningCommands.Clear();
        var runningCommands = await _commandService.GetRunningCommandsAsync();
        foreach (var runningCommand in runningCommands)
        {
            RunningCommands.Add(new CommandVM(runningCommand)
            {
                KillCommand = KillRunningCommandCommand
            });
        }
    }


    private void RunningCommandsChangedHandler(object? sender,
        RunningCommandChangedEventArgs runningCommandChangedEventArgs)
    {
        switch (runningCommandChangedEventArgs.ChangeType)
        {
            case RunningCommandChangedEventArgs.CommandChangeType.Added:
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    RunningCommands.Add(new CommandVM(runningCommandChangedEventArgs.Command)
                    {
                        KillCommand = KillRunningCommandCommand
                    });
                });

                break;
            case RunningCommandChangedEventArgs.CommandChangeType.Removed:
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    var command =
                        RunningCommands.FirstOrDefault(x => x.RunId == runningCommandChangedEventArgs.Command.RunId);
                    if (command != null)
                    {
                        RunningCommands.Remove(command);
                    }
                });
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public partial class CommandVM : ObservableObject
{
    public CommandVM(ProcessCommand processCommand)
    {
        Id = processCommand.CommandDefinitionId;
        RunId = processCommand.RunId;
        CommandDisplayName = processCommand.DisplayName;
        FullCommand = processCommand.FullCommand;
        IsKilling = false;
    }

    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public string CommandDisplayName { get; set; }

    public string FullCommand { get; set; }

    public bool IsKilling { get; set; }

    public required IAsyncRelayCommand KillCommand { get; init; }
}

public partial class CommandDefinitionVM : ObservableObject
{
    public CommandDefinitionVM(CommandDefinition commandDefinition)
    {
        Id = commandDefinition.Id;
        CommandDisplayName = commandDefinition.CommandDisplayName;
        Executable = commandDefinition.ExecutionOptions.Command;
        Arguments = commandDefinition.ExecutionOptions.Arguments ?? string.Empty;
        WorkingDirectory = commandDefinition.ExecutionOptions.WorkingDirectory ?? App.ROOT_DIR;
    }

    public Guid Id { get; set; }
    public string CommandDisplayName { get; set; }

    public string Executable { get; set; }
    public string Arguments { get; set; }
    public string WorkingDirectory { get; set; }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanDelete))]
    private bool _isDeleting;

    public bool CanDelete => !IsDeleting;
    public required IAsyncRelayCommand DeleteCommand { get; init; }
}