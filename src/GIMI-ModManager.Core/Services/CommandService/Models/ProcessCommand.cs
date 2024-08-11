using System.Diagnostics;
using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.Core.Services.CommandService.Models;

/// <summary>
/// This is returned when a command is created, is used to track and interact with the process
/// </summary>
public sealed class ProcessCommand
{
    private readonly Process _process;
    private readonly CommandContext _context;

    internal InternalCreateCommandOptions InternalCreateOptions { get; }
    public CommandExecutionOptions Options => InternalCreateOptions.ExecutionOptions;

    public Guid RunId => _context.RunId;

    public Guid CommandDefinitionId => InternalCreateOptions.CommandDefinitionId;

    public string DisplayName => InternalCreateOptions.DisplayName;

    public string FullCommand => _process.StartInfo.FileName + " " + _process.StartInfo.Arguments;

    internal ProcessCommand(Process process, InternalCreateCommandOptions options,
        CommandContext context)
    {
        _process = process;
        InternalCreateOptions = options;
        _context = context;

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

        _process.Exited += ExitHandler;


        var currentWorkingDirectory = Environment.CurrentDirectory;

        if ((Options.UseShellExecute || Options.RunAsAdmin) && !_process.StartInfo.WorkingDirectory.IsNullOrEmpty())
        {
            if (Directory.Exists(_process.StartInfo.WorkingDirectory))
            {
                Environment.CurrentDirectory = _process.StartInfo.WorkingDirectory;
            }
            else
            {
                throw new InvalidOperationException(
                    "Working directory does not exist for a command using shell execute");
            }
        }

        bool result;
        try
        {
            result = _process.Start();
        }
        catch (Exception e)
        {
            _process.Exited -= ExitHandler;
            throw;
        }
        finally
        {
            Environment.CurrentDirectory = currentWorkingDirectory;
        }

        HasBeenStarted = true;
        _context.StartTime = DateTime.Now;

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

    private void ExitHandler(object? sender, EventArgs args)
    {
        HasExited = true;
        ExitCode = _process.ExitCode;
        _context.EndTime = DateTime.Now;
        _process.Dispose();
        Exited?.Invoke(this, args);
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