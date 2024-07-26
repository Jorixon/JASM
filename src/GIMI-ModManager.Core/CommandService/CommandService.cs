using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace GIMI_ModManager.Core.CommandService;

public sealed class CommandService(ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<CommandService>();

    private List<CommandDefinition> _commands = new();

    private readonly ConcurrentDictionary<Guid, ProcessCommand> _runningCommands = [];

    private string _jsonCommandFile = "";

    public bool IsInitialized { get; private set; }

    public async Task InitializeAsync(string appDataDirectoryPath)
    {
        if (IsInitialized)
            return;

        if (!Directory.Exists(appDataDirectoryPath))
            Directory.CreateDirectory(appDataDirectoryPath);

        _jsonCommandFile = Path.Combine(appDataDirectoryPath, "commands.json");

        if (File.Exists(_jsonCommandFile))
        {
            var json = await File.ReadAllTextAsync(_jsonCommandFile).ConfigureAwait(false);
            var jsonCommands = JsonSerializer.Deserialize<CommandRootJson>(json)?.Commands ?? [];

            _commands = jsonCommands.Select(CommandDefinition.FromJson).ToList();
        }
        else
        {
            _logger.Information("No command root json found, creating new one");
            await File.WriteAllTextAsync(_jsonCommandFile, JsonSerializer.Serialize(new CommandRootJson()))
                .ConfigureAwait(false);
        }


        Debug.Assert(File.Exists(_jsonCommandFile));

        IsInitialized = true;
    }


    private ProcessCommand InternalCreateCommand(InternalCreateCommandOptions options)
    {
        var commandContext = new CommandContext
        {
            TargetPath = options.TargetPath,
            DisplayName = options.DisplayName
        };

        options.ExecutionOptions.IsReadOnly = true;

        var startInfo = new ProcessStartInfo
        {
            FileName = SpecialVariables.ReplaceVariables(options.ExecutionOptions.Command, options.TargetPath),
            Arguments = SpecialVariables.ReplaceVariables(options.ExecutionOptions.Arguments, options.TargetPath),
            WorkingDirectory = options.ExecutionOptions.WorkingDirectory,
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

        command.Started += (_, _) => _runningCommands.TryAdd(commandContext.Id, command);
        command.Exited += (_, _) => _runningCommands.TryRemove(commandContext.Id, out _);

        return command;
    }

    public ProcessCommand CreateCommand(string targetPath, CommandDefinition definition)
    {
        return InternalCreateCommand(new InternalCreateCommandOptions
        {
            KillOnMainAppExit = definition.KillOnMainAppExit,
            TargetPath = targetPath,
            ExecutionOptions = definition.ExecutionOptions.Clone()
        });
    }

    // TODO: TMP Storage
    private readonly CommandRootJson _commandRootJson = new();

    public Task SaveCommandDefinitionAsync(CommandDefinition commandDefinition)
    {
        var jsonCommand = new JsonCommandDefinition
        {
            Id = commandDefinition.Id,
            CreateTime = DateTime.Now,
            DisplayName = commandDefinition.ExecutionOptions.Command,
            Command = commandDefinition.ExecutionOptions.Command,
            Arguments = commandDefinition.ExecutionOptions.Arguments,
            WorkingDirectory = commandDefinition.ExecutionOptions.WorkingDirectory,
            KillOnMainAppExit = commandDefinition.KillOnMainAppExit,
            UseShellExecute = commandDefinition.ExecutionOptions.UseShellExecute,
            CreateWindow = commandDefinition.ExecutionOptions.CreateWindow,
            RunAsAdmin = commandDefinition.ExecutionOptions.RunAsAdmin
        };

        _commandRootJson.Commands = _commandRootJson.Commands.Append(jsonCommand).ToArray();

        return Task.CompletedTask;
    }


    public Task<IReadOnlyList<CommandDefinition>> ReadCommandDefinitionsAsync()
    {
        return Task.FromResult<IReadOnlyList<CommandDefinition>>(_commandRootJson.Commands
            .Select(CommandDefinition.FromJson).ToList());
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

public class CommandDefinition
{
    public Guid Id { get; private init; } = Guid.NewGuid();

    public required string CommandDisplayName { get; init; }

    // If true, the process will be killed when the application closes
    public required bool KillOnMainAppExit { get; init; }
    public required CommandExecutionOptions ExecutionOptions { get; init; }


    internal static CommandDefinition FromJson(JsonCommandDefinition jsonCommandDefinition)
    {
        return new CommandDefinition()
        {
            Id = jsonCommandDefinition.Id,
            CommandDisplayName = jsonCommandDefinition.DisplayName,
            KillOnMainAppExit = jsonCommandDefinition.KillOnMainAppExit,
            ExecutionOptions = new CommandExecutionOptions()
            {
                Command = jsonCommandDefinition.Command,
                WorkingDirectory = jsonCommandDefinition.WorkingDirectory,
                UseShellExecute = jsonCommandDefinition.UseShellExecute,
                Arguments = jsonCommandDefinition.Arguments,
                CreateWindow = jsonCommandDefinition.CreateWindow,
                RunAsAdmin = jsonCommandDefinition.RunAsAdmin
            }
        };
    }
}

internal class InternalCreateCommandOptions
{
    // Replace with actual path
    public required string TargetPath { get; set; }

    private string? _displayName;

    public string DisplayName => _displayName ??=
        SpecialVariables.ReplaceVariables(ExecutionOptions.Command + " " + ExecutionOptions.Arguments, TargetPath);

    public bool KillOnMainAppExit { get; init; }

    public required CommandExecutionOptions ExecutionOptions { get; set; }
}

/// <summary>
/// This is returned when a command is created, is used to track and interact with the process
/// </summary>
public sealed class ProcessCommand
{
    private readonly Process _process;
    private CommandContext Context { get; }

    internal InternalCreateCommandOptions InternalCreateOptions { get; }
    public CommandExecutionOptions Options => InternalCreateOptions.ExecutionOptions;

    public string DisplayName => Context.DisplayName;

    internal ProcessCommand(Process process, InternalCreateCommandOptions options,
        CommandContext context)
    {
        _process = process;
        InternalCreateOptions = options;
        Context = context;

        CanWriteInputReadOutput = InternalCreateOptions.ExecutionOptions.CanRedirectInputOutput();
    }

    public bool HasBeenStarted { get; private set; }
    public bool CanWriteInputReadOutput { get; private set; }

    public bool HasExited { get; private set; }

    public bool IsRunning => !HasExited;

    public int ExitCode { get; private set; } = 1337;

    public event EventHandler? Started;
    public event EventHandler? Exited;
    public event EventHandler<DataReceivedEventArgs>? OutputDataReceived;
    public event EventHandler<DataReceivedEventArgs>? ErrorDataReceived;


    public bool Start()
    {
        if (HasBeenStarted)
            throw new InvalidOperationException("Cannot start a command that has already been started");

        HasBeenStarted = true;
        _process.Exited += (_, args) =>
        {
            HasExited = true;
            ExitCode = _process.ExitCode;
            Context.EndTime = DateTime.Now;
            _process.Dispose();
            Exited?.Invoke(this, args);
        };


        var result = _process.Start();
        Context.StartTime = DateTime.Now;

        if (CanWriteInputReadOutput)
        {
            _process.OutputDataReceived += (_, args) => OutputDataReceived?.Invoke(this, args);
            _process.ErrorDataReceived += (_, args) => ErrorDataReceived?.Invoke(this, args);

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        Started?.Invoke(this, EventArgs.Empty);

        _process.Refresh();
        return result;
    }

    public async Task WaitForExitAsync()
    {
        if (HasExited)
            return;

        // TODO: Use task completion source to wait for the process to exit
        await Task.Run(() => _process.WaitForExit()).ConfigureAwait(false);
    }


    public async Task WriteInputAsync(string? input, bool appendNewLine = true)
    {
        if (!CanWriteInputReadOutput)
            throw new InvalidOperationException("Cannot write input to a process that uses shell execute");

        if (HasExited)
            throw new InvalidOperationException("Cannot write input to a process that has exited");

        var inputToWrite = input + (appendNewLine ? Environment.NewLine : string.Empty);

        await _process.StandardInput.WriteAsync(inputToWrite).ConfigureAwait(false);
    }


    public async Task<int> KillAsync()
    {
        if (HasExited)
            return ExitCode;

        _process.Kill();
        await WaitForExitAsync().ConfigureAwait(false);
        return ExitCode;
    }
}

internal class CommandRootJson
{
    public JsonCommandDefinition[] Commands { get; set; } = [];
}

internal class JsonCommandDefinition
{
    public required Guid Id { get; set; }
    public required DateTime CreateTime { get; set; }
    public required string DisplayName { get; set; }
    public required string Command { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required string? Arguments { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required string? WorkingDirectory { get; set; }

    public required bool UseShellExecute { get; set; }
    public required bool CreateWindow { get; set; }
    public required bool RunAsAdmin { get; set; }

    public required bool KillOnMainAppExit { get; set; }
}