using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GIMI_ModManager.Core.Services.ModPresetService.JsonModels;

[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "This is a json model")]
internal class JsonModPresetEntry
{
    public required Guid ModId { get; set; }
    public required string FullPath { get; set; }
    public bool IsMissing { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CustomName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceUrl { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Preferences { get; set; }


    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AddedAt { get; set; }
}