using System.Diagnostics;
using System.Text.Json.Serialization;

namespace GIMI_ModManager.Core.Services.CommandService;

public class CommandExecutionOptions
{
    private bool _useShellExecute;
    private bool _createWindow;
    private bool _runAsAdmin;
    private string? _workingDirectory;
    private string? _arguments;
    private string _command = "";

    [JsonIgnore] public bool IsReadOnly { get; internal set; }

    private void ThrowIfReadOnly<T>(ref T value, T valueToSet)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("The object is read-only");

        value = valueToSet;
    }


    /// <inheritdoc cref="ProcessStartInfo.UseShellExecute"/>
    public bool UseShellExecute
    {
        get => _useShellExecute;
        set => ThrowIfReadOnly(ref _useShellExecute, value);
    }

    /// <inheritdoc cref="ProcessStartInfo.CreateNoWindow"/>
    public bool CreateWindow
    {
        get => _createWindow;
        set => ThrowIfReadOnly(ref _createWindow, value);
    }

    /// <summary>
    /// If true, the command will be executed as an administrator. This requires UseShellExecute to be true.
    /// </summary>
    public bool RunAsAdmin
    {
        get => _runAsAdmin;
        set => ThrowIfReadOnly(ref _runAsAdmin, value);
    }


    /// <summary>
    /// If null, use JASM working directory
    /// </summary>
    public string? WorkingDirectory
    {
        get => _workingDirectory;
        set => ThrowIfReadOnly(ref _workingDirectory, value);
    }

    /// <summary>
    /// Variable substitution is done when the command is executed
    /// </summary>
    public string? Arguments
    {
        get => _arguments;
        set => ThrowIfReadOnly(ref _arguments, value);
    }

    /// <summary>
    /// Should be the full path to the executable or in the users path
    /// </summary>
    public required string Command
    {
        get => _command;
        set
        {
            if (value is null)
                throw new ArgumentNullException(nameof(Command));

            ThrowIfReadOnly(ref _command, value);
        }
    }

    public bool CanRedirectInputOutput()
    {
        return !UseShellExecute && !CreateWindow && !RunAsAdmin;
    }


    public CommandExecutionOptions Clone()
    {
        return new CommandExecutionOptions()
        {
            Command = Command,
            Arguments = Arguments,
            WorkingDirectory = WorkingDirectory,
            CreateWindow = CreateWindow,
            RunAsAdmin = RunAsAdmin,
            UseShellExecute = UseShellExecute
        };
    }
}