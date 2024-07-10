using System.Diagnostics;
using Serilog;

namespace GIMI_ModManager.Core.CommandService;

public sealed class CommandService(ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<CommandService>();

    public bool IsInitialized { get; private set; }

    public Task InitializeAsync()
    {
        IsInitialized = true;
        return Task.CompletedTask;
    }


    private ProcessCommand InternalCreateCommand(CreateCommandOptions options)
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

        return new ProcessCommand(process, options.ExecutionOptions, commandContext);
    }

    public ProcessCommand CreateCommand(string targetPath, CommandExecutionOptions options)
    {
        return InternalCreateCommand(new CreateCommandOptions
        {
            TargetPath = targetPath,
            ExecutionOptions = options
        });
    }
}

internal class CreateCommandOptions
{
    // Replace with actual path
    public required string TargetPath { get; set; }

    private string? _displayName;

    public string DisplayName => _displayName ??=
        SpecialVariables.ReplaceVariables(ExecutionOptions.Command + " " + ExecutionOptions.Arguments, TargetPath);

    public required CommandExecutionOptions ExecutionOptions { get; set; }
}

/// <summary>
/// This is returned when a command is created, is used to track and interact with the process
/// </summary>
public sealed class ProcessCommand : IDisposable
{
    private readonly Process _process;

    internal CommandContext Context { get; }
    public CommandExecutionOptions Options { get; }

    public string DisplayName => Context.DisplayName;

    internal ProcessCommand(Process process, CommandExecutionOptions options, CommandContext context)
    {
        _process = process;
        Options = options;
        Context = context;

        CanWriteInputReadOutput = options.CanRedirectInputOutput();
    }

    public bool HasBeenStarted { get; private set; }
    public bool CanWriteInputReadOutput { get; private set; }
    public bool HasExited => _process.HasExited;
    public int ExitCode => _process.ExitCode;

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
            Context.EndTime = DateTime.Now;
            _process.Refresh();
            Exited?.Invoke(this, args);
        };

        Context.StartTime = DateTime.Now;
        var result = _process.Start();

        if (CanWriteInputReadOutput)
        {
            _process.OutputDataReceived += (_, args) => OutputDataReceived?.Invoke(this, args);
            _process.ErrorDataReceived += (_, args) => ErrorDataReceived?.Invoke(this, args);

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

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


    public void WriteInput(string? input)
    {
        if (!CanWriteInputReadOutput)
            throw new InvalidOperationException("Cannot write input to a process that uses shell execute");

        if (HasExited)
            throw new InvalidOperationException("Cannot write input to a process that has exited");

        _process.StandardInput.WriteLine(input);
    }

    public void Dispose()
    {
        _process.Dispose();
    }
}