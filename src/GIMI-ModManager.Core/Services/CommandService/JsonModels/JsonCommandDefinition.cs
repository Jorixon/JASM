using System.Text.Json.Serialization;

namespace GIMI_ModManager.Core.Services.CommandService.JsonModels;

internal class JsonCommandDefinition
{
    public required Guid Id { get; set; }
    public required DateTime CreateTime { get; set; }
    public required string DisplayName { get; set; }
    public required string Command { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Arguments { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkingDirectory { get; set; }

    public required bool UseShellExecute { get; set; }
    public required bool CreateWindow { get; set; }
    public required bool RunAsAdmin { get; set; }

    public required bool KillOnMainAppExit { get; set; }
}