using System.Text.Json.Serialization;

namespace GIMI_ModManager.Core.Services.CommandService.JsonModels;

internal class JsonCommandRoot
{
    public JsonCommandDefinition[] Commands { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonCommandDefinition? StartGameCommand { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonCommandDefinition? StartGameModelImporter { get; set; }
}