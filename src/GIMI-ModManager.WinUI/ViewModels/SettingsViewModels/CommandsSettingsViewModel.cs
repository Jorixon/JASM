using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Services.CommandService;
using GIMI_ModManager.Core.Services.CommandService.Models;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.Views.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.ViewModels.SettingsViewModels;

public sealed partial class CommandsSettingsViewModel(
    CommandService commandService,
    IWindowManagerService windowManagerService,
    NotificationManager notificationManager,
    ILocalSettingsService localSettingsService,
    CommandHandlerService commandHandlerService)
    : ObservableRecipient, INavigationAware
{
    private readonly CommandService _commandService = commandService;
    private readonly IWindowManagerService _windowManagerService = windowManagerService;
    private readonly NotificationManager _notificationManager = notificationManager;
    private readonly ILocalSettingsService _localSettingsService = localSettingsService;
    private readonly CommandHandlerService _commandHandlerService = commandHandlerService;

    public ObservableCollection<CommandDefinitionVM> CommandDefinitions { get; } = new();

    public ObservableCollection<CommandVM> RunningCommands { get; } = new();


    [RelayCommand]
    private async Task OpenCreateCommandAsync()
    {
        var key = "ShowCommandWarningDialogKey";

        var showCommandWarningDialog = await _localSettingsService.ReadSettingAsync<bool?>(key);

        if (showCommandWarningDialog is null or true)
        {
            var commandWarningDialog = new ContentDialog
            {
                Title = "Friendly Warning",

                Content = new TextBlock()
                {
                    Text = "Please be careful when creating commands. " +
                           "Commands can be used to run any executable on your system. " +
                           "Only create commands from trusted sources.\n" +
                           "JASM isn't perfect and can't protect you from malicious scripts or JASM bugs/glitches.\n\n" +
                           "By clicking 'I understand' you acknowledge that you understand the risks.",
                    IsTextSelectionEnabled = true,
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                PrimaryButtonText = "I understand",
                CloseButtonText = "Cancel"
            };

            var result = await _windowManagerService.ShowDialogAsync(commandWarningDialog);

            if (result == ContentDialogResult.Primary)
            {
                await _localSettingsService.SaveSettingAsync(key, false);
            }
            else
            {
                return;
            }
        }

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

    private bool CanEditCommand(CommandDefinitionVM? commandVM)
    {
        return commandVM is { IsDeleting: false, HasTargetPathVariable: false } &&
               RunningCommands.ToArray().All(r => r.Id != commandVM.Id);
    }

    [RelayCommand(CanExecute = nameof(CanEditCommand))]
    private async Task EditAsync(CommandDefinitionVM? commandDefinition)
    {
        if (commandDefinition is null)
            return;

        var window = App.MainWindow;

        var existingCommand = await _commandService.GetCommandDefinitionAsync(commandDefinition.Id);

        if (existingCommand is null)
        {
            _notificationManager.ShowNotification("Failed to get command", "Command not found",
                TimeSpan.FromSeconds(5));
            return;
        }

        var options = CreateCommandOptions.EditCommand(existingCommand);
        var page = new CreateCommandView(options: options);

        await _windowManagerService.ShowFullScreenDialogAsync(page, window.Content.XamlRoot, window);
        await RefreshCommandDefinitionsAsync().ConfigureAwait(false);
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

        _notificationManager.ShowNotification($"Command '{commandDefinition.CommandDisplayName}' deleted successfully",
            string.Empty, TimeSpan.FromSeconds(2));
    }

    private bool CanRunCommand(CommandDefinitionVM? commandVM)
    {
        return commandVM is { IsDeleting: false, HasTargetPathVariable: false } &&
               RunningCommands.ToArray().All(r => r.Id != commandVM.Id);
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    private async Task RunAsync(CommandDefinitionVM? commandVM)
    {
        if (commandVM is null || !CanRunCommand(commandVM))
            return;

        var result = await Task.Run(() => _commandHandlerService.RunCommandAsync(commandVM.Id, null));

        if (result.HasNotification)
        {
            _notificationManager.ShowNotification(result.Notification);
        }
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

        foreach (var commandDefinition in commandDefinitions.Reverse())
        {
            var commandDefinitionVM = new CommandDefinitionVM(commandDefinition)
            {
                DeleteCommand = DeleteCommandCommand,
                RunCommand = RunCommand,
                EditCommand = EditCommand
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
                    RunningCommands.Insert(0, new CommandVM(runningCommandChangedEventArgs.Command)
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

        RunCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
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
        HasTargetPathVariable =
            commandDefinition.ExecutionOptions.HasAnySpecialVariables([SpecialVariables.TargetPath]);


        var attributes = new List<string>();

        const string separator = " | ";

        if (commandDefinition.KillOnMainAppExit)
            attributes.Add(nameof(CommandDefinition.KillOnMainAppExit) + separator);

        if (commandDefinition.ExecutionOptions.RunAsAdmin)
            attributes.Add(nameof(CommandExecutionOptions.RunAsAdmin) + separator);

        if (commandDefinition.ExecutionOptions.UseShellExecute)
            attributes.Add(nameof(CommandExecutionOptions.UseShellExecute) + separator);

        if (attributes.Count != 0)
        {
            attributes.Insert(0, "Options: ");
            var lastItem = attributes.Last().TrimEnd('|', ' ');
            attributes[^1] = lastItem;
        }

        Attributes = attributes;
    }

    public Guid Id { get; set; }
    public string CommandDisplayName { get; set; }

    public string Executable { get; set; }
    public string Arguments { get; set; }
    public string WorkingDirectory { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    private bool _isDeleting;

    public bool CanDelete => !IsDeleting;
    public required IAsyncRelayCommand DeleteCommand { get; init; }

    public List<string> Attributes { get; }

    public bool HasTargetPathVariable { get; }

    public bool HasNoTargetPathVariable => !HasTargetPathVariable;


    public required IAsyncRelayCommand RunCommand { get; init; }


    public required IAsyncRelayCommand EditCommand { get; init; }
}