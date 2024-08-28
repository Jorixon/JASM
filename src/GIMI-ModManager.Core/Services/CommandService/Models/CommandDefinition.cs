using GIMI_ModManager.Core.Services.CommandService.JsonModels;

namespace GIMI_ModManager.Core.Services.CommandService.Models;

public sealed class CommandDefinition
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public DateTime CreateTime { get; private init; } = DateTime.Now;

    public required string CommandDisplayName { get; init; }

    // If true, the process will be killed when the application closes
    public required bool KillOnMainAppExit { get; init; }
    public required CommandExecutionOptions ExecutionOptions { get; init; }

    public bool IsGameStartCommand { get; internal set; }

    public bool IsModelImporterCommand { get; internal set; }

    /// <summary>
    /// This will return the full command with all variables replaced
    /// </summary>
    public (string FullCommand, string? WorkingDirectory) GetFullCommand(
        SpecialVariablesInput? specialVariablesInput)
    {
        var fullCommand = SpecialVariables.ReplaceVariables(
            ExecutionOptions.Command + " " + ExecutionOptions.Arguments,
            specialVariablesInput);

        var workingDirectory = SpecialVariables.ReplaceVariables(ExecutionOptions.WorkingDirectory,
            specialVariablesInput);

        return (fullCommand, workingDirectory);
    }

    internal void UpdateId(Guid newId) => Id = newId;


    internal static CommandDefinition FromJson(JsonCommandDefinition jsonCommandDefinition,
        bool isGameStartCommand = false, bool isModelImporterCommand = false)
    {
        return new CommandDefinition()
        {
            Id = jsonCommandDefinition.Id,
            CreateTime = jsonCommandDefinition.CreateTime,
            CommandDisplayName = jsonCommandDefinition.DisplayName,
            KillOnMainAppExit = jsonCommandDefinition.KillOnMainAppExit,
            IsGameStartCommand = isGameStartCommand,
            IsModelImporterCommand = isModelImporterCommand,
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

    internal JsonCommandDefinition ToJson()
    {
        return new JsonCommandDefinition
        {
            Id = Id,
            CreateTime = CreateTime,
            DisplayName = CommandDisplayName,
            Command = ExecutionOptions.Command,
            Arguments = ExecutionOptions.Arguments,
            WorkingDirectory = ExecutionOptions.WorkingDirectory,
            UseShellExecute = ExecutionOptions.UseShellExecute,
            CreateWindow = ExecutionOptions.CreateWindow,
            RunAsAdmin = ExecutionOptions.RunAsAdmin,
            KillOnMainAppExit = KillOnMainAppExit
        };
    }
}