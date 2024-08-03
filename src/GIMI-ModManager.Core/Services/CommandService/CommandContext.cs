namespace GIMI_ModManager.Core.Services.CommandService;

public class CommandContext
{
    public Guid Id { get; } = Guid.NewGuid();

    public DateTime CreateTime { get; } = DateTime.Now;

    public DateTime? StartTime { get; internal set; }

    public DateTime? EndTime { get; internal set; }

    public SpecialVariablesInput? SpecialVariables { get; init; }
}