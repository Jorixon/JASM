using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using GIMI_ModManager.Core.Services.CommandService.JsonModels;
using GIMI_ModManager.Core.Services.CommandService.Models;
using Serilog;
using static GIMI_ModManager.Core.Services.CommandService.RunningCommandChangedEventArgs;

namespace GIMI_ModManager.Core.Services.CommandService;

public sealed class CommandService(ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<CommandService>();

    private List<CommandDefinition> _commands = new();

    private readonly ConcurrentDictionary<Guid, ProcessCommand> _runningCommands = [];

    private string _jsonCommandFilePath = "";

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public bool IsInitialized { get; private set; }

    public event EventHandler<RunningCommandChangedEventArgs> RunningCommandsChanged;

    public async Task InitializeAsync(string appDataDirectoryPath)
    {
        if (IsInitialized)
            return;

        if (!Directory.Exists(appDataDirectoryPath))
            Directory.CreateDirectory(appDataDirectoryPath);

        _jsonCommandFilePath = Path.Combine(appDataDirectoryPath, "commands.json");

        if (File.Exists(_jsonCommandFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_jsonCommandFilePath).ConfigureAwait(false);
                var jsonCommands = JsonSerializer.Deserialize<JsonCommandRoot>(json) ?? new JsonCommandRoot();

                _commands = jsonCommands.Commands.Select(j => CommandDefinition.FromJson(j)).ToList();
                if (jsonCommands.StartGameCommand is not null)
                    _commands.Add(CommandDefinition.FromJson(jsonCommands.StartGameCommand, isGameStartCommand: true));

                if (jsonCommands.StartGameModelImporter is not null)
                    _commands.Add(CommandDefinition.FromJson(jsonCommands.StartGameModelImporter,
                        isModelImporterCommand: true));
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to read command root json. Creating new one");
                File.Move(_jsonCommandFilePath, _jsonCommandFilePath + ".invalid", overwrite: true);
                await File.WriteAllTextAsync(_jsonCommandFilePath,
                        JsonSerializer.Serialize(new JsonCommandRoot(), _jsonOptions))
                    .ConfigureAwait(false);
            }
        }
        else
        {
            _logger.Information("No command root json found, creating new one");
            await File.WriteAllTextAsync(_jsonCommandFilePath,
                    JsonSerializer.Serialize(new JsonCommandRoot(), _jsonOptions))
                .ConfigureAwait(false);
        }


        Debug.Assert(File.Exists(_jsonCommandFilePath));

        IsInitialized = true;
    }


    private ProcessCommand InternalCreateCommand(InternalCreateCommandOptions options)
    {
        var commandContext = new CommandContext
        {
            SpecialVariables = options.SpecialVariablesInput
        };

        options.ExecutionOptions.IsReadOnly = true;

        var startInfo = new ProcessStartInfo
        {
            FileName = SpecialVariables.ReplaceVariables(options.ExecutionOptions.Command,
                options.SpecialVariablesInput),
            Arguments = SpecialVariables.ReplaceVariables(options.ExecutionOptions.Arguments,
                options.SpecialVariablesInput),
            WorkingDirectory = SpecialVariables.ReplaceVariables(options.ExecutionOptions.WorkingDirectory,
                options.SpecialVariablesInput),
            UseShellExecute = options.ExecutionOptions.UseShellExecute,
            CreateNoWindow = !options.ExecutionOptions.CreateWindow,
            Verb = options.ExecutionOptions.RunAsAdmin ? "runas" : null
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };


        if (options.ExecutionOptions.CanRedirectInputOutput())
        {
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;
        }

        var command = new ProcessCommand(process, options, commandContext);

        command.Started += (_, _) =>
        {
            _runningCommands.TryAdd(commandContext.RunId, command);
            RunningCommandsChanged?.Invoke(this,
                new RunningCommandChangedEventArgs(CommandChangeType.Added, command));
        };
        command.Exited += (_, _) =>
        {
            var removed = _runningCommands.TryRemove(commandContext.RunId, out _);
            if (removed)
                RunningCommandsChanged?.Invoke(this,
                    new RunningCommandChangedEventArgs(CommandChangeType.Removed, command));
        };

        return command;
    }

    public ProcessCommand CreateCommand(CommandDefinition definition, SpecialVariablesInput? specialVariables = null)
    {
        if (specialVariables is not null)
            specialVariables.IsReadOnly = true;

        return InternalCreateCommand(new InternalCreateCommandOptions
        {
            DisplayName = definition.CommandDisplayName,
            CommandDefinitionId = definition.Id,
            KillOnMainAppExit = definition.KillOnMainAppExit,
            SpecialVariablesInput = specialVariables,
            ExecutionOptions = definition.ExecutionOptions.Clone()
        });
    }

    public Task SaveCommandDefinitionAsync(CommandDefinition commandDefinition)
    {
        commandDefinition.ExecutionOptions.IsReadOnly = true;
        _commands.Add(commandDefinition);

        return SaveCommandsAsync();
    }

    public Task SetSpecialCommands(Guid commandDefinitionId, bool gameStart, bool modelImporterStart)
    {
        if (gameStart)
        {
            var existingGameStartCommand = _commands.FirstOrDefault(c => c.IsGameStartCommand);
            if (existingGameStartCommand is not null)
                existingGameStartCommand.IsGameStartCommand = false;

            var command = _commands.FirstOrDefault(c => c.Id == commandDefinitionId);
            if (command is null)
                throw new InvalidOperationException("Command does not exist");
            command.IsGameStartCommand = true;
        }

        if (modelImporterStart)
        {
            var existingModelImporterCommand = _commands.FirstOrDefault(c => c.IsModelImporterCommand);
            if (existingModelImporterCommand is not null)
                existingModelImporterCommand.IsModelImporterCommand = false;

            var command = _commands.FirstOrDefault(c => c.Id == commandDefinitionId);
            if (command is null)
                throw new InvalidOperationException("Command does not exist");
            command.IsModelImporterCommand = true;
        }

        return SaveCommandsAsync();
    }


    public Task DeleteCommandDefinitionAsync(Guid commandId)
    {
        var command = _commands.FirstOrDefault(c => c.Id == commandId);

        if (command is null)
            return Task.CompletedTask;

        _commands.Remove(command);

        return SaveCommandsAsync();
    }

    public async Task<CommandDefinition> UpdateCommandDefinitionAsync(Guid existingCommandId,
        CommandDefinition newCommandDefinition)
    {
        var existingCommand = _commands.FirstOrDefault(c => c.Id == existingCommandId);

        if (existingCommand is null)
            throw new InvalidOperationException("Command does not exist");

        newCommandDefinition.ExecutionOptions.IsReadOnly = true;
        newCommandDefinition.UpdateId(newCommandDefinition.Id);

        newCommandDefinition.IsGameStartCommand = existingCommand.IsGameStartCommand;
        newCommandDefinition.IsModelImporterCommand = existingCommand.IsModelImporterCommand;

        _commands.Remove(existingCommand);
        _commands.Add(newCommandDefinition);

        try
        {
            await SaveCommandsAsync().ConfigureAwait(false);
            return newCommandDefinition;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to update command, reverting...");
            _commands.Remove(newCommandDefinition);
            _commands.Add(existingCommand);
        }

        try
        {
            await SaveCommandsAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to revert command update");
            throw;
        }

        throw new InvalidOperationException("Failed to update command ");
    }

    public Task<IReadOnlyList<CommandDefinition>> GetCommandDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<CommandDefinition>>(_commands.ToList());
    }

    public Task<CommandDefinition?> GetCommandDefinitionAsync(Guid commandId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_commands.FirstOrDefault(c => c.Id == commandId));
    }

    public Task<IReadOnlyList<ProcessCommand>> GetRunningCommandsAsync()
    {
        return Task.FromResult<IReadOnlyList<ProcessCommand>>(_runningCommands.Values.ToList());
    }


    private async Task SaveCommandsAsync()
    {
        var jsonRoot = new JsonCommandRoot
        {
            Commands = _commands
                .Where(c => c is { IsGameStartCommand: false, IsModelImporterCommand: false })
                .Select(c => c.ToJson())
                .ToArray()
        };

        var gameStartCommand = _commands.FirstOrDefault(c => c.IsGameStartCommand);
        if (gameStartCommand is not null)
            jsonRoot.StartGameCommand = gameStartCommand.ToJson();

        var modelImporterCommand = _commands.FirstOrDefault(c => c.IsModelImporterCommand);
        if (modelImporterCommand is not null)
            jsonRoot.StartGameModelImporter = modelImporterCommand.ToJson();


        await File.WriteAllTextAsync(_jsonCommandFilePath, JsonSerializer.Serialize(jsonRoot, _jsonOptions))
                .ConfigureAwait(false);
    }


    public void Cleanup()
    {
        var runningCommands = _runningCommands.Values.ToArray();

        foreach (var command in runningCommands)
        {
            if (command is { IsRunning: true, InternalCreateOptions.KillOnMainAppExit: true })
                _ = command.KillAsync();
        }
    }
}

public class RunningCommandChangedEventArgs(
    CommandChangeType commandChangeType,
    ProcessCommand command)
    : EventArgs
{
    public CommandChangeType ChangeType { get; } = commandChangeType;

    public ProcessCommand Command { get; } = command;

    public enum CommandChangeType
    {
        Added,
        Removed
    }
}

internal class InternalCreateCommandOptions
{
    public required Guid CommandDefinitionId { get; init; }

    public required SpecialVariablesInput? SpecialVariablesInput { get; init; }

    private readonly string? _displayName;

    public string DisplayName
    {
        get =>
            _displayName ??
            SpecialVariables.ReplaceVariables(ExecutionOptions.Command + " " + ExecutionOptions.Arguments,
                SpecialVariablesInput ?? new SpecialVariablesInput());

        init => _displayName = value;
    }


    public bool KillOnMainAppExit { get; init; }

    public required CommandExecutionOptions ExecutionOptions { get; set; }
}